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

            Ibtws = new IBTWSSimulator(Utility.SymbolFilePathGetter,
                new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2017, 12, 10, 0, 0, 0, TimeSpan.Zero));
            //new DateTimeOffset(2017, 01, 31, 0, 0, 0, TimeSpan.Zero));
            //Ibtws = new IBTWSSimulator((cxPair, dt) => Utility.FxIBDATAPathGetter(cxPair), new DateTimeOffset(2018, 04, 24, 0, 0, 0, TimeSpan.Zero));
            IbtwsViewModel = new IBTWSViewModel(Ibtws);

            var interestedSymbols = new[] { Index.DAX};

            _contextStream = new Subject<CandleStickPatternContext>();

            Strategy = new Strategy("CandleStick Pattern");

            var nextCondition = CandleStickPatternTrade.Strategy;

            var minuteFeed = Ibtws.RealTimeBarStream.Select(msg => MessageExtensions.ToCandle(msg, TimeSpan.FromMinutes(1)));
            var hourlyFeed = new AggreagateCandleFeed(minuteFeed, TimeSpan.FromHours(1)).Stream;
            var dailyfeed = new AggreagateCandleFeed(minuteFeed, TimeSpan.FromDays(1)).Stream;
            var haCandleFeed = new HeikinAshiCandleFeed(dailyfeed);

            var candleFeed = hourlyFeed;
            var context = new CandleStickPatternContext(Strategy, new List<IIndicator> { haCandleFeed.HACandle },
                new Lookback(60, new ConcurrentQueue<Candle>()),
                Enumerable.Empty<IContextInfo>());

            //TODO: for manual feed, change the candle span
            candleFeed
                .WithLatestFrom(haCandleFeed.Stream, Tuple.Create)
                .Subscribe(tup =>
                {
                    var candle = tup.Item1;
                    Status = $"Executing {candle.TimeStamp}";
                    var newcontext = new CandleStickPatternContext(context.Strategy,
                        new List<IIndicator>() { tup.Item2 }, context.LookbackCandles.Add(candle), context.ContextInfos);
                    (nextCondition, context) = nextCondition().Evaluate(newcontext, candle);
                    _contextStream.OnNext(context);
                });

            StartCommand = new RelayCommand(_ =>
            {
                Ibtws.AddRealtimeDataRequests(interestedSymbols
                    .Select(symbol => Tuple.Create<int, Contract>(symbol.UniqueId, symbol.GetContract()))
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
                (candle, tup) => ((Candle)candle,
                    tup.Indicators
                        .Where(i => i.Key != Indicators.CandleBodySma5)
                        .Select(i => i.Value)
                        .OfType<MovingAverage>()
                        .Select(i => ((IIndicator)i, i.Value))
                        .Append((tup.Indicators[Indicators.HeikinAshi], ((Candle)tup.Indicators[Indicators.HeikinAshi]).High))
                        .Append((CustomIndicator.Get("HA Low"), ((Candle)tup.Indicators[Indicators.HeikinAshi]).Low))));

            var equityCurveFeed =
                Strategy.CloseddOrders
                    .Select(o => 
                        (CustomIndicator.Get(CustomIndicators.EquityCurveIndicator), o.OrderInfo.TimeStamp.DateTime, Strategy.ProfitLoss.Value));
            
            TraderChart = new CreateMultiPaneStockChartsViewModel(priceChart,
                 new List<Dictionary<IIndicator, IObservable<(IIndicator, DateTime, double)>>>()
                 {
                     new Dictionary<IIndicator, IObservable<(IIndicator, DateTime, double)>>()
                     {
                         {
                             CustomIndicator.Get(CustomIndicators.EquityCurveIndicator),
                             candleFeed.Select(c =>
                             {
                                 //if(Strategy.ProfitLoss?.Value == 0)
                                 return (CustomIndicator.Get(CustomIndicators.EquityCurveIndicator),
                                         c.TimeStamp.DateTime, (Strategy.ProfitLoss?.Value).GetValueOrDefault());
                             })
                         }
                     }
                 }, eventsFeed);
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
        private static Candle previousPreviousShadow;
        private static Candle previousShadow;
        private static Candle currentShadow;
        private static int candlesAfterPerfectOrder = 0;

        private static Func<FuncCondition<CandleStickPatternContext>> contextReadyCondition = () =>
            new FuncCondition<CandleStickPatternContext>(
                onSuccess: didPriceHitSR,
                onFailure: contextReadyCondition,
                predicate: context => context.IsReady().ToPredicateResult());

        private static Func<FuncCondition<CandleStickPatternContext>> didPriceHitSR = () =>
            new FuncCondition<CandleStickPatternContext>(
                onSuccess: exitCondition,
                onFailure: didPriceHitSR,
                predicates: new List<Func<CandleStickPatternContext, PredicateResult>>()
                {
                    {
                        ctx => 
                        {
                            var shadowCandle = ((Candle) ctx.Indicators[Indicators.HeikinAshi]);

                            if (currentShadow == null || currentShadow != shadowCandle)
                            {
                                previousPreviousShadow = previousShadow;
                                previousShadow = currentShadow;
                                currentShadow = shadowCandle;
                            }

                            var alreadyTradedThisHour =
                                ctx.Strategy.RecentClosedOrder?.OrderInfo.TimeStamp.DateTime.DayOfYear ==
                                ctx.LastCandle.TimeStamp.DateTime.DayOfYear;

                            if(alreadyTradedThisHour)
                                return PredicateResult.Fail;


                            var isBuy = ctx.LastCandle.PassedThroughPrice(shadowCandle.Low)
                                        //&& ctx.LookbackCandles.Candles.Last()
                                        //    .ClosedBelowLow(ctx.LookbackCandles.Candles.First())
                                        //&& previousShadow?.Low < shadowCandle.Low;
                                        && previousPreviousShadow?.Low < previousShadow?.Low
                                        && previousPreviousShadow?.Low < shadowCandle.Low;
                            //var isBuy = 
                            //    ctx.LookbackCandles.LastCandle.Close > shadowCandle.Low
                            //            && ctx.LookbackCandles.LastCandle.Low < shadowCandle.Low
                            //            && ctx.LookbackCandles.LastCandle.Open > shadowCandle.Low
                            //            //&& ctx.LookbackCandles.Candles.ToDailyCandles().Last().High < shadowCandle.High
                            //            && shadowCandle.IsGreen();

                            //var isSell = ctx.LookbackCandles.LastCandle.Close < shadowCandle.High
                            //             && ctx.LookbackCandles.LastCandle.Open > shadowCandle.High;

                            var isSell = false;

                            return (isBuy || isSell).ToPredicateResult();

                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    var shadowCandle = ((Candle)ctx.Indicators[Indicators.HeikinAshi]);



                    var isBuy = ctx.LastCandle.PassedThroughPrice(shadowCandle.Low)
                                //&& ctx.LookbackCandles.Candles.Last()
                                //    .ClosedBelowLow(ctx.LookbackCandles.Candles.First())
                                //&& previousShadow?.Low < shadowCandle.Low;
                                && previousPreviousShadow?.Low < previousShadow?.Low
                                && previousPreviousShadow?.Low < shadowCandle.Low;

                    //ctx.LookbackCandles.LastCandle.Close > shadowCandle.Low
                    //&& ctx.LookbackCandles.LastCandle.Low < shadowCandle.Low
                    //&& ctx.LookbackCandles.LastCandle.Open > shadowCandle.Low
                    ////&& ctx.LookbackCandles.Candles.ToDailyCandles().Last().High < shadowCandle.High
                    //&& shadowCandle.IsGreen();

                    //var isSell = ctx.LookbackCandles.LastCandle.Close < shadowCandle.High
                    //             && ctx.LookbackCandles.LastCandle.Open > shadowCandle.High;

                    var isSell = false;


                    var side = isBuy ? Side.Buy : Side.ShortSell;

                    return ctx
                        //.PlaceOrder(ctx.LastCandle.TimeStamp, (HeikinAshiCandle)ctx.Indicators[Indicators.HeikinAshi], Side.Buy)
                        .PlaceOrder(ctx.LastCandle.TimeStamp, ctx.LastCandle, shadowCandle.Low, side)
                        .AddContextInfo(new TPSLInfo(
                            shadowCandle.CandleLength() * 0.75,
                            //shadowCandle.CandleLength()/2,
                            shadowCandle.Low -  previousShadow.Low,
                            //0.00100, 0.00100,
                            shadowCandle));
                });

        public static int exitCandleCount = 0;

        private static Func<FuncCondition<CandleStickPatternContext>> exitCondition = () =>
            new FuncCondition<CandleStickPatternContext>(
                onSuccess: didPriceHitSR,
                onFailure: exitCondition,
                predicates: new List<Func<CandleStickPatternContext, PredicateResult>>()
                {
                    {
                        ctx =>
                        {
                            //exitCandleCount++;

                            //if (exitCandleCount < 5)
                            //    return false;

                            var currentShadow = ((Candle) ctx.Indicators[Indicators.HeikinAshi]);
                            var openTimeShadow = ctx.ContextInfos.OfType<TPSLInfo>().Single().ShadowCandle;

                            if (openTimeShadow != currentShadow)
                                return PredicateResult.Success;




                            //if (ctx.LastCandle.ClosedAboveHigh(openTimeShadow)
                            //|| ctx.LastCandle.ClosedAboveHigh(currentShadow))
                            //    return true;

                            //if (ctx.LastCandle.Close > currentShadow.High || ctx.LastCandle.Close < currentShadow.Low)
                            //    return PredicateResult.Success;
                            

                            //if (ctx.LastCandle.ClosedAboveHigh(((Candle) ctx.Indicators[Indicators.HeikinAshi]))
                            //        || ctx.LastCandle.ClosedBelowLow(((Candle) ctx.Indicators[Indicators.HeikinAshi])))
                            //    return true;

                            //if (ctx.Strategy.OpenOrder is BuyOrder)
                            {
                                var pl = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle, 1);

                                var tpslInfo = ctx.ContextInfos.OfType<TPSLInfo>().Single();
                                if(pl >= tpslInfo.Tp || pl <= -1 * tpslInfo.Sl)
                                    return PredicateResult.Success;

                                return PredicateResult.Fail;
                            }

                            throw new Exception("unknown");
                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    exitCandleCount = 0;
                    if (ctx.Strategy.OpenOrder is BuyOrder)
                    {
                        var openTimeShadow = ctx.ContextInfos.OfType<TPSLInfo>().Single().ShadowCandle;


                        var candle = ctx.LastCandle;
                        //var closePrice = ctx.LastCandle.ClosedAboveHigh(openTimeShadow) ? openTimeShadow.High : candle.Close;
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

        public static CandleStickPatternContext PlaceOrder(this CandleStickPatternContext context, DateTimeOffset timeStamp, Candle candle, double entryPrice,
            Side side)
        {
            if (side == Side.ShortSell)
            {
                var shortSellOrder = new ShortSellOrder(
                    new OrderInfo(timeStamp, CurrencyPair.EURGBP, context.Strategy, entryPrice.USD(),
                        100000));
                context.Strategy.Open(shortSellOrder);
                return new CandleStickPatternContext(context.Strategy, context.Indicators.Values,
                    context.LookbackCandles, context.ContextInfos);
            }

            if (side == Side.Buy)
            {
                context.Strategy.Open(new BuyOrder(
                    new OrderInfo(timeStamp, CurrencyPair.EURGBP, context.Strategy, entryPrice.USD(),
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

    public class TPSLInfo : IContextInfo
    {
        public double Tp { get; }
        public double Sl { get; }
        public Candle ShadowCandle { get; }

        public TPSLInfo(double tp, double sl, Candle shadowCandle)
        {
            Tp = tp;
            Sl = sl;
            ShadowCandle = shadowCandle;
        }
    }
}
