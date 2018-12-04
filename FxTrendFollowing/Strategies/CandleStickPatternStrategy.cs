using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Carvers.Charting.MultiPane;
using Carvers.Charting.ViewModels;
using Carvers.IB.App;
using Carvers.IBApi;
using Carvers.IBApi.Extensions;
using Carvers.Infra.Extensions;
using Carvers.Infra.ViewModels;
using Carvers.Models;
using Carvers.Models.DataReaders;
using Carvers.Models.Events;
using Carvers.Models.Extensions;
using Carvers.Models.Indicators;
using IBApi;

namespace FxTrendFollowing.Strategies
{
    public class CandleStickPatternStrategy : ViewModel
    {
        private string _status;
        private Subject<CandleStickPatternContext> _contextStream;
        private TraderViewModel _chartVm;
        private MultiTraderViewModel _multiTraderVm;
        private CreateMultiPaneStockChartsViewModel _traderChart;

        public CandleStickPatternStrategy()
        {

            Ibtws = new IBTWSSimulator(Utility.FxFilePathGetter,
                new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero));
            //new DateTimeOffset(2017, 07, 30, 0, 0, 0, TimeSpan.Zero));
            //new DateTimeOffset(2017, 01, 31, 0, 0, 0, TimeSpan.Zero));
            //Ibtws = new IBTWSSimulator((cxPair, dt) => Utility.FxIBDATAPathGetter(cxPair), new DateTimeOffset(2018, 04, 24, 0, 0, 0, TimeSpan.Zero));
            IbtwsViewModel = new IBTWSViewModel(Ibtws);

            var interestedPairs = new[] { CurrencyPair.EURUSD };

            _contextStream = new Subject<CandleStickPatternContext>();

            Strategy = new Strategy("CandleStick Pattern");

            var nextCondition = CandleStickPatternTrade.Strategy;

            var minuteFeed = Ibtws.RealTimeBarStream.Select(msg => MessageExtensions.ToCandle(msg, TimeSpan.FromMinutes(1)));
            var candleFeed = new AggreagateCandleFeed(minuteFeed, TimeSpan.FromHours(1)).Stream;

            var haCandleFeed = new HeikinAshiCandleFeed(candleFeed);
            var ma1Day = new MovingAverageStreamingService(Indicators.SMA20, candleFeed, candle => candle.High, 1);
            var maLDay = new MovingAverageStreamingService(Indicators.SMA10, candleFeed, candle => candle.Low, 1);


            var context = new CandleStickPatternContext(Strategy, new List<IIndicator> { ma1Day.MovingAverage, maLDay.MovingAverage },
                new Lookback(1, new ConcurrentQueue<Candle>()),
                Enumerable.Empty<IContextInfo>());

            //TODO: for manual feed, change the candle span
            candleFeed.Zip(ma1Day.Stream, maLDay.Stream, Tuple.Create)
                .Subscribe(tup =>
                {
                    var candle = tup.Item1;
                    var ma = tup.Item2;
                    var maL = tup.Item3;
                    Status = $"Executing {candle.TimeStamp}";
                    var newcontext = new CandleStickPatternContext(context.Strategy,
                        new List<IIndicator>() {ma, maL}, context.LookbackCandles.Add(candle), context.ContextInfos);
                    (nextCondition, context) = nextCondition().Evaluate(newcontext, candle);
                    _contextStream.OnNext(context);
                });

            StartCommand = new RelayCommand(_ =>
            {
                Ibtws.AddRealtimeDataRequests(interestedPairs
                    .Select(pair => Tuple.Create<int, Contract>(pair.UniqueId, ContractCreator.GetCurrencyPairContract(pair)))
                    .ToList());
            });

            StopCommand = new RelayCommand(_ => { Strategy.Stop(); });


            var logReport = new StrategyLogReport(new[] { Strategy }, logName: "MoBo");
            var chartReport = new StrategyChartReport(new[] { Strategy }, Dispatcher.CurrentDispatcher);
            var summaryReport = new StrategySummaryReport(new[] { Strategy });
            Reporters = new Carvers.Infra.ViewModels.Reporters(logReport, chartReport, summaryReport);

            var eventsFeed = Strategy.OpenOrders
                .Select(order => (IEvent)new OrderExecutedEvent(order.OrderInfo.TimeStamp, order))
                .Merge(Strategy.CloseddOrders
                    .Select(order => (IEvent)new OrderExecutedEvent(order.OrderInfo.TimeStamp, order)));

            var priceChart = candleFeed.Zip(_contextStream, haCandleFeed.Stream,
                (candle, ctx, haCandle) => ((Candle)candle,
                    ctx.Indicators
                        .Where(i => i.Key != Indicators.CandleBodySma5)
                        .Select(i => i.Value)
                        .OfType<MovingAverage>()
                        .Select(i => ((IIndicator)i, i.Value))
                        .Append((haCandle, haCandle.High))));

            TraderChart = new CreateMultiPaneStockChartsViewModel(priceChart,
                 new List<Dictionary<IIndicator, IObservable<(IIndicator, DateTime, double)>>>() { }, eventsFeed);
        }

        public CreateMultiPaneStockChartsViewModel TraderChart
        {
            get => _traderChart;
            set
            {
                _traderChart = value;
                OnPropertyChanged();
            }
        }

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public IBTWSSimulator Ibtws { get; }
        public IBTWSViewModel IbtwsViewModel { get; }
        public Strategy Strategy { get; }

        public string Status
        {
            get { return _status; }
            private set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public Carvers.Infra.ViewModels.Reporters Reporters { get; }

        public TraderViewModel ChartVm
        {
            get
            {
                return _chartVm;
            }

            private set
            {
                _chartVm = value;
                OnPropertyChanged();
            }
        }
    }


    public static class CandleStickPatternTrade
    {
        private static int candlesAfterPerfectOrder = 0;

        private static Func<FuncCondition<CandleStickPatternContext>> contextReadyCondition = () =>
            new FuncCondition<CandleStickPatternContext>(
                onSuccess: isInvertedHammer,
                onFailure: contextReadyCondition,
                predicate: context => context.IsReady());

        private static Func<FuncCondition<CandleStickPatternContext>> isInvertedHammer = () =>
            new FuncCondition<CandleStickPatternContext>(
                onSuccess: exitCondition,
                onFailure: isInvertedHammer,
                predicates: new List<Func<CandleStickPatternContext, bool>>()
                {
                    {
                        ctx => ctx.LookbackCandles.LastCandle.IsInverterHammer()
                    }
                },
                onSuccessAction: ctx => ctx.PlaceOrder(ctx.LastCandle, Side.ShortSell));

        private static Func<FuncCondition<CandleStickPatternContext>> exitCondition = () =>
            new FuncCondition<CandleStickPatternContext>(
                onSuccess: isInvertedHammer,
                onFailure: exitCondition,
                predicates: new List<Func<CandleStickPatternContext, bool>>()
                {
                    {
                        ctx => true
                    }
                },
                onSuccessAction: ctx =>
                {
                    if (ctx.Strategy.OpenOrder is BuyOrder)
                    {
                        var candle = ctx.LastCandle;
                        ctx.Strategy.Close(
                            new SellOrder((BuyOrder)ctx.Strategy.OpenOrder,
                                new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, ctx.Strategy, candle.Ohlc.Close,
                                    100000, candle)));
                        return ctx;
                    }

                    if (ctx.Strategy.OpenOrder is ShortSellOrder)
                    {
                        var candle = ctx.LastCandle;
                        ctx.Strategy.Close(
                            new BuyToCoverOrder((ShortSellOrder)ctx.Strategy.OpenOrder,
                                new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, ctx.Strategy, candle.Ohlc.Close,
                                    100000, candle)));
                        return ctx;
                    }

                    throw new Exception("Unexpected error");
                });


        public static Func<FuncCondition<CandleStickPatternContext>> Strategy = contextReadyCondition;
    }

    public class CandleStickPatternContext : IContext
    {
        public Strategy Strategy { get; }
        public Dictionary<string, IIndicator> Indicators { get; }
        public Lookback LookbackCandles { get; }
        public IEnumerable<IContextInfo> ContextInfos { get; }

        public CandleStickPatternContext(Strategy strategy,
            IEnumerable<IIndicator> indicators,
            Lookback lookbackCandles,
            IEnumerable<IContextInfo> contextInfos)
        {
            Strategy = strategy;
            Indicators = indicators.ToDictionary(ma => ma.Description, ma => ma);
            LookbackCandles = lookbackCandles;
            ContextInfos = contextInfos;
            LastCandle = LookbackCandles.LastCandle;
        }

        public Candle LastCandle { get; }

        public bool IsReady()
            => LookbackCandles.IsComplete();
    }

    public static class CandleStickPatternContextExtensions
    {
        public static CandleStickPatternContext PlaceOrder(this CandleStickPatternContext context, Candle lastCandle,
            Side side)
        {
            if (side == Side.ShortSell)
            {
                var shortSellOrder = new ShortSellOrder(
                    new OrderInfo(lastCandle.TimeStamp, CurrencyPair.EURGBP, context.Strategy, lastCandle.Ohlc.Close,
                        100000));
                context.Strategy.Open(shortSellOrder);
                return new CandleStickPatternContext(context.Strategy, context.Indicators.Values,
                    context.LookbackCandles, context.ContextInfos);
            }

            if (side == Side.Buy)
            {
                context.Strategy.Open(new BuyOrder(
                    new OrderInfo(lastCandle.TimeStamp, CurrencyPair.EURGBP, context.Strategy, lastCandle.Ohlc.Close,
                        100000)));
                return new CandleStickPatternContext(context.Strategy, context.Indicators.Values,
                    context.LookbackCandles, context.ContextInfos);
            }

            throw new Exception("unexpected error");
        }

        public static CandleStickPatternContext AddContextInfo(this CandleStickPatternContext context, IContextInfo info)
            => new CandleStickPatternContext(context.Strategy, context.Indicators.Values, context.LookbackCandles,
                new List<IContextInfo> { info });

    }
}
