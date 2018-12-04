using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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
using Carvers.Models.Indicators;
using FxTrendFollowing.Breakout.ViewModels;
using IBApi;

namespace FxTrendFollowing.Strategies
{
    public class Day20BreakoutTradeViewModel : ViewModel
    {
        private string _status;
        private Subject<Day20BeakoutTradeContext> _contextStream;
        private TraderViewModel _chartVm;
        private MultiTraderViewModel _multiTraderVm;
        private CreateMultiPaneStockChartsViewModel _traderChart;

        public Day20BreakoutTradeViewModel()
        {

            Ibtws = new IBTWSSimulator(Utility.FxFilePathGetter,
                new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero));
            //new DateTimeOffset(2017, 07, 30, 0, 0, 0, TimeSpan.Zero));
            //new DateTimeOffset(2017, 01, 31, 0, 0, 0, TimeSpan.Zero));
            //Ibtws = new IBTWSSimulator((cxPair, dt) => Utility.FxIBDATAPathGetter(cxPair), new DateTimeOffset(2018, 04, 24, 0, 0, 0, TimeSpan.Zero));
            IbtwsViewModel = new IBTWSViewModel(Ibtws);

            var interestedPairs = new[] { CurrencyPair.GBPUSD };

            _contextStream = new Subject<Day20BeakoutTradeContext>();

            Strategy = new Strategy("Day 20 Breakout");

            var nextCondition = Day20BreakoutTrade.Strategy;

            var minuteFeed = Ibtws.RealTimeBarStream.Select(msg => MessageExtensions.ToCandle(msg, TimeSpan.FromMinutes(1)));
            var candleFeed =new AggreagateCandleFeed(minuteFeed, TimeSpan.FromHours(1)).Stream;

            var context = new Day20BeakoutTradeContext(Strategy, Enumerable.Empty<IIndicator>(), 
                new Lookback(25, new ConcurrentQueue<Candle>()),
                Enumerable.Empty<IContextInfo>());

            //TODO: for manual feed, change the candle span
            candleFeed
                .Subscribe(candle =>
                {
                    Status = $"Executing {candle.TimeStamp}";
                    var newcontext = new Day20BeakoutTradeContext(context.Strategy,
                        Enumerable.Empty<IIndicator>(), context.LookbackCandles.Add(candle), context.ContextInfos);
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

            var priceChart = candleFeed.Zip(_contextStream,
                (candle, ctx) => (candle,
                    ctx.Indicators
                        .Where(i => i.Key != Indicators.CandleBodySma5)
                        .Select(i => i.Value)
                        .OfType<MovingAverage>()
                        .Select(i => ((IIndicator)i, i.Value))));

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


    public static class Day20BreakoutTrade
    {
        private static int candlesAfterPerfectOrder = 0;

        private static Func<FuncCondition<Day20BeakoutTradeContext>> contextReadyCondition = () =>
            new FuncCondition<Day20BeakoutTradeContext>(
                onSuccess: is20DayBreakout,
                onFailure: contextReadyCondition,
                predicate: context => context.IsReady());

        private static Func<FuncCondition<Day20BeakoutTradeContext>> is20DayBreakout = () =>
            new FuncCondition<Day20BeakoutTradeContext>(
                onSuccess: exitCondition,
                onFailure: is20DayBreakout,
                predicates: new List<Func<Day20BeakoutTradeContext, bool>>()
                {
                    {
                        ctx =>
                        {
                            var breakoutCandle = ctx.LookbackCandles.Candles.Skip(19).First();
                            var pullbackCandles = ctx.LookbackCandles.Candles.Skip(20).Take(2);

                            return ctx.LookbackCandles.Candles.Take(20).Maximum(c => c.High) == breakoutCandle
                                   && pullbackCandles.First().Low > pullbackCandles.Last().Close
                                   && ctx.LastCandle.Close > breakoutCandle.High;
                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    return ctx.PlaceOrder(ctx.LastCandle, Side.Buy)
                        .AddContextInfo(new PullbackCandle(ctx.LookbackCandles.Candles.Skip(20).Take(2).Last()));
                });

        private static Func<FuncCondition<Day20BeakoutTradeContext>> exitCondition = () =>
            new FuncCondition<Day20BeakoutTradeContext>(
                onSuccess: is20DayBreakout,
                onFailure: exitCondition,
                predicates: new List<Func<Day20BeakoutTradeContext, bool>>()
                {
                    {
                        /* When Moving averages cross */
                        ctx =>
                        {
                            var stopLoss = ctx.Strategy.OpenOrder.OrderInfo.Price.Value - 
                                           ctx.ContextInfos.OfType<PullbackCandle>().Single().Candle.Low;
                            var takeProfit = 2 * stopLoss;

                            var pl = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle, 1);

                            return (pl > takeProfit) || pl < -1 * stopLoss;
                        }
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


        public static Func<FuncCondition<Day20BeakoutTradeContext>> Strategy = contextReadyCondition;
    }

    public class Day20BeakoutTradeContext : IContext
    {
        public Strategy Strategy { get; }
        public Dictionary<string, IIndicator> Indicators { get; }
        public Lookback LookbackCandles { get; }
        public IEnumerable<IContextInfo> ContextInfos { get; }

        public Day20BeakoutTradeContext(Strategy strategy,
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



    public static class Day20BreakoutTradeContextExtensions
    {
        public static Day20BeakoutTradeContext PlaceOrder(this Day20BeakoutTradeContext context, Candle lastCandle,
            Side side)
        {
            if (side == Side.ShortSell)
            {
                var shortSellOrder = new ShortSellOrder(
                    new OrderInfo(lastCandle.TimeStamp, CurrencyPair.EURGBP, context.Strategy, lastCandle.Ohlc.Close,
                        100000));
                context.Strategy.Open(shortSellOrder);
                return new Day20BeakoutTradeContext(context.Strategy, context.Indicators.Values,
                    context.LookbackCandles, context.ContextInfos);
            }

            if (side == Side.Buy)
            {
                context.Strategy.Open(new BuyOrder(
                    new OrderInfo(lastCandle.TimeStamp, CurrencyPair.EURGBP, context.Strategy, lastCandle.Ohlc.Close,
                        100000)));
                return new Day20BeakoutTradeContext(context.Strategy, context.Indicators.Values,
                    context.LookbackCandles, context.ContextInfos);
            }

            throw new Exception("unexpected error");
        }

        public static Day20BeakoutTradeContext AddContextInfo(this Day20BeakoutTradeContext context, IContextInfo info)
            => new Day20BeakoutTradeContext(context.Strategy, context.Indicators.Values, context.LookbackCandles, 
                new List<IContextInfo> {info});

    }

    public class Day20Candle : IContextInfo
    {
        public Candle Candle { get; }

        public Day20Candle(Candle candle)
        {
            Candle = candle;
        }
    }

    public class PullbackCandle : IContextInfo
    {
        public Candle Candle { get; }

        public PullbackCandle(Candle candle)
        {
            Candle = candle;
        }
    }

}
