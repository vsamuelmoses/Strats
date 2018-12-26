using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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

        public CandleStickPatternStrategy(Symbol instrument)
        {

            Ibtws = new IBTWSSimulator(Utility.SymbolFilePathGetter,
                new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2017, 12, 10, 0, 0, 0, TimeSpan.Zero));
            //new DateTimeOffset(2017, 01, 31, 0, 0, 0, TimeSpan.Zero));
            //Ibtws = new IBTWSSimulator((cxPair, dt) => Utility.FxIBDATAPathGetter(cxPair), new DateTimeOffset(2018, 04, 24, 0, 0, 0, TimeSpan.Zero));
            IbtwsViewModel = new IBTWSViewModel(Ibtws);

            var interestedSymbols = new[] { instrument };

            _contextStream = new Subject<CandleStickPatternContext>();

            Strategy = new Strategy("CandleStick Pattern");

            var nextCondition = CandleStickPatternTrade.Strategy;

            var minuteFeed = Ibtws.RealTimeBarStream.Select(msg => MessageExtensions.ToCandle(msg, TimeSpan.FromMinutes(1)));
            var hourlyFeed = new AggreagateCandleFeed(minuteFeed, TimeSpan.FromHours(1)).Stream;
            var dailyfeed = new AggreagateCandleFeed(minuteFeed, TimeSpan.FromDays(1)).Stream;
            var haCandleFeed = new HeikinAshiCandleFeed(dailyfeed);

            var candleFeed = hourlyFeed;
            var context = new CandleStickPatternContext(Strategy, new List<IIndicator> { haCandleFeed.HACandle },
                new Lookback(1, new ConcurrentQueue<Candle>()),
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

            //var equityCurveFeed =
            //    Strategy.CloseddOrders
            //        .Select(o => 
            //            (CustomIndicator.Get(CustomIndicators.EquityCurveIndicator), o.OrderInfo.TimeStamp.DateTime, Strategy.ProfitLoss.Value));
            
            TraderChart = new CreateMultiPaneStockChartsViewModel(priceChart,
                 new List<Dictionary<IIndicator, IObservable<(IIndicator, DateTime, double)>>>()
                 {
                     new Dictionary<IIndicator, IObservable<(IIndicator, DateTime, double)>>()
                     {
                         {
                             CustomIndicator.Get(CustomIndicators.EquityCurveIndicator),
                             candleFeed.Select(c =>
                             {
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
        private static List<double> mrSellMaxProfit = new List<double>();
        private static List<double> mrSellMaxLoss = new List<double>();
        private static List<double> mrBuyMaxProfit = new List<double>();
        private static List<double> mrBuyMaxLoss = new List<double>();

        private static int mrSellCount;
        private static int mrBuyCount;

        //private static double takeProfit = 18d;// DAX
        //private static double takeProfit = 5d;// FTSE
        //private static double takeProfit = 0.0010d;// gbpusd
        //private static double takeProfit = 0.00060d;// aud.usd

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
                            if (ctx.LastCandle.TimeStamp.DateTime.Date == new DateTime(2017, 01, 16)
                                && ctx.LastCandle.TimeStamp.Hour == 06)
                            {
                                var breakpoint = true;
                            }

                            var shadowCandle = ((Candle) ctx.Indicators[Indicators.HeikinAshi]);

                            if (currentShadow == null || currentShadow != shadowCandle)
                            {
                                previousPreviousShadow = previousShadow;

                                if (currentShadow == null)
                                {
                                    previousShadow = shadowCandle;
                                }
                                else
                                {
                                    previousShadow = currentShadow;
                                }

                                currentShadow = shadowCandle;
                            }

                            var alreadyTradedThisHour =
                                ctx.Strategy.RecentClosedOrder?.OrderInfo.TimeStamp.DateTime.DayOfYear ==
                                ctx.LastCandle.TimeStamp.DateTime.DayOfYear;

                            if(alreadyTradedThisHour)
                                return PredicateResult.Fail;


                            var isBuy = ctx.LastCandle.PassedThroughPrice(shadowCandle.High);
                                        //&& shadowCandle.High - ctx.LastCandle.Open < shadowCandle.CandleLength() / 4
                                        //&& ctx.LastCandle.Open < shadowCandle.High;

                            var isSell = ctx.LastCandle.PassedThroughPrice(shadowCandle.Low);
                                         //&& ctx.LastCandle.Open - shadowCandle.Low < shadowCandle.CandleLength() / 4 &&
                                         //ctx.LastCandle.Open > shadowCandle.Low;

                            var entryPrice = isBuy ? shadowCandle.High : shadowCandle.Low;

                            double? buyTakeProfit = null;
                            if (isBuy)
                            {
                                buyTp +=  ctx.LastCandle.High - entryPrice;
                                buyCount++;
                                if(buyCount > 30)
                                    buyTakeProfit = (buyTp - (ctx.LastCandle.High - entryPrice))/(buyCount-1) * 0.75;
                                Console.WriteLine("BUY: " + buyTp/buyCount);
                            }

                            double? sellTakeProfit = null;
                            if(isSell)
                            {
                                ssTp += entryPrice - ctx.LastCandle.Low;
                                ssCount++;
                                if(ssCount > 30)
                                    sellTakeProfit = (ssTp - (entryPrice - ctx.LastCandle.Low))/(ssCount - 1) * 0.75;
                                Console.WriteLine("SS: " + ssTp/ ssCount);
                            }




                            if (isBuy && buyTakeProfit.HasValue)
                            {
                                var candle = ctx.LastCandle;
                                ctx.PlaceOrder(ctx.LastCandle.TimeStamp, ctx.LastCandle, shadowCandle.High, Side.Buy);

                                var exitPrice = ctx.LastCandle.Close;
                                if (ctx.LastCandle.High - shadowCandle.High > buyTakeProfit.Value)
                                    exitPrice = (shadowCandle.High + buyTakeProfit.Value);

                                ctx.Strategy.Close(
                                    new SellOrder((BuyOrder)ctx.Strategy.OpenOrder,
                                        new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, ctx.Strategy, exitPrice.USD() ,
                                            100000, candle)));

                            }


                            if (isSell && sellTakeProfit.HasValue)
                            {
                                var candle = ctx.LastCandle;
                                ctx.PlaceOrder(ctx.LastCandle.TimeStamp, ctx.LastCandle, shadowCandle.Low, Side.ShortSell);

                                var exitPrice = ctx.LastCandle.Close;
                                if (shadowCandle.Low - ctx.LastCandle.Low > sellTakeProfit.Value)
                                    exitPrice = (shadowCandle.Low  - sellTakeProfit.Value);

                                ctx.Strategy.Close(
                                    new BuyToCoverOrder((ShortSellOrder)ctx.Strategy.OpenOrder,
                                        new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, ctx.Strategy,
                                            exitPrice.USD(),
                                            100000, candle)));


                            }

                            return PredicateResult.Fail;
                            
                            //if (isBuy && ctx.LastCandle.High - shadowCandle.High > takeProfit)
                            //{
                            //    var candle = ctx.LastCandle;
                            //    ctx.PlaceOrder(ctx.LastCandle.TimeStamp, ctx.LastCandle, shadowCandle.High, Side.Buy);

                            //    ctx.Strategy.Close(
                            //        new SellOrder((BuyOrder)ctx.Strategy.OpenOrder,
                            //            new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, ctx.Strategy, ( shadowCandle.High + takeProfit).USD(),
                            //                100000, candle)));

                            //    return PredicateResult.Fail;

                            //}

                            //if (isSell && shadowCandle.Low - ctx.LastCandle.Low > takeProfit)
                            //{
                            //    var candle = ctx.LastCandle;
                            //    ctx.PlaceOrder(ctx.LastCandle.TimeStamp, ctx.LastCandle, shadowCandle.Low, Side.ShortSell);
                            //    ctx.Strategy.Close(
                            //        new BuyToCoverOrder((ShortSellOrder)ctx.Strategy.OpenOrder,
                            //            new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, ctx.Strategy,
                            //                (shadowCandle.Low  - takeProfit).USD(),
                            //                100000, candle)));

                            //    return PredicateResult.Fail;

                            //}

                            return (isBuy || isSell).ToPredicateResult();

                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    var shadowCandle = ((Candle)ctx.Indicators[Indicators.HeikinAshi]);

                    var isBuy = ctx.LastCandle.PassedThroughPrice(shadowCandle.High);
                                //&& shadowCandle.High - ctx.LastCandle.Open < shadowCandle.CandleLength() / 4
                                //&& ctx.LastCandle.Open < shadowCandle.High;

                    var isSell = ctx.LastCandle.PassedThroughPrice(shadowCandle.Low);
                                 //&& ctx.LastCandle.Open - shadowCandle.Low < shadowCandle.CandleLength() / 4 &&
                                 //ctx.LastCandle.Open > shadowCandle.Low;


                    var side = isBuy ? Side.Buy : Side.ShortSell;

                    var entryPrice = isBuy ? shadowCandle.High : shadowCandle.Low;


                    var stopLoss = isBuy
                        ? shadowCandle.High - ctx.LastCandle.Low
                        : ctx.LastCandle.High - shadowCandle.Low;


                    //if (isBuy)
                    //{
                    //   buyTp +=  ctx.LastCandle.High - entryPrice;
                    //    buyCount++;
                    //    Console.WriteLine("BUY: " + buyTp/buyCount);
                    //}
                    //else
                    //{
                    //    ssTp += entryPrice - ctx.LastCandle.Low;
                    //    ssCount++;
                    //    Console.WriteLine("SS: " + ssTp/ ssCount);
                    //}

                    //var entryPrice = ctx.LastCandle.Close;
                    return ctx
                        //.PlaceOrder(ctx.LastCandle.TimeStamp, (HeikinAshiCandle)ctx.Indicators[Indicators.HeikinAshi], Side.Buy)
                        .PlaceOrder(ctx.LastCandle.TimeStamp, ctx.LastCandle, entryPrice, side)
                        .AddContextInfo(new TPSLInfo(
                            shadowCandle.CandleLength(),
                            //shadowCandle.CandleLength()/2,
                            stopLoss,
                            //0.00100, 0.00100,
                            ctx.LastCandle));
                });

        public static int exitCandleCount = 0;

        public static int count = 0;

        private static Func<FuncCondition<CandleStickPatternContext>> exitCondition = () =>
            new FuncCondition<CandleStickPatternContext>(
                onSuccess: didPriceHitSR,
                onFailure: exitCondition,
                predicates: new List<Func<CandleStickPatternContext, PredicateResult>>()
                {
                    {
                        ctx =>
                        {
                            exitCandleCount++;

                            if (exitCandleCount < 1)
                                return PredicateResult.Fail;


                            if (ctx.Strategy.OpenOrder is BuyOrder)
                            {
                                mrSellCount++;
                                var mrMaxProfit = ctx.Strategy.OpenOrder.OrderInfo.Price.Value - ctx.LastCandle.Close;
                                if (mrMaxProfit > 0)
                                {
                                    mrSellMaxProfit.Add(mrMaxProfit);
                                    Console.WriteLine("Avg MR Sell Profit: " + mrSellMaxProfit.Average());
                                    Console.WriteLine("MR Sell Win percentage: " + (mrSellMaxProfit.Count() * 100)/mrSellCount);
                                }
                                else
                                {
                                    mrMaxProfit = ctx.LastCandle.Close - ctx.Strategy.OpenOrder.OrderInfo.Price.Value;

                                    if (mrMaxProfit > 0)
                                    {
                                        mrSellMaxLoss.Add(mrMaxProfit);
                                        Console.WriteLine("Avg MR Sell Loss: " + mrSellMaxLoss.Average());
                                        Console.WriteLine("MR Sell Loss percentage: " +
                                                          (mrSellMaxLoss.Count() * 100) / mrSellCount);
                                    }
                                }
                            }


                            if (ctx.Strategy.OpenOrder is ShortSellOrder)
                            {
                                mrBuyCount++;
                                var mrMaxProfit = ctx.LastCandle.High - ctx.Strategy.OpenOrder.OrderInfo.Price.Value;
                                if (mrMaxProfit > 0)
                                {
                                    mrBuyMaxProfit.Add(mrMaxProfit);
                                    Console.WriteLine("Avg MR Buy Profit: " + mrBuyMaxProfit.Average());
                                    Console.WriteLine("MR Buy Win percentage: " + (mrBuyMaxProfit.Count() * 100)/mrBuyCount);
                                }
                                else
                                {
                                    mrMaxProfit = ctx.Strategy.OpenOrder.OrderInfo.Price.Value - ctx.LastCandle.Low;

                                    if (mrMaxProfit > 0)
                                    {
                                        mrBuyMaxLoss.Add(mrMaxProfit);
                                        Console.WriteLine("Avg MR Buy Loss: " + mrBuyMaxLoss.Average());
                                        Console.WriteLine("MR Buy Loss percentage: " +
                                                          (mrBuyMaxLoss.Count() * 100) / mrBuyCount);
                                    }
                                }
                            }

                            return PredicateResult.Success;
                            
                            //if (ctx.Strategy.OpenOrder is BuyOrder)
                            //{
                            //    if (ctx.LastCandle.High - ctx.Strategy.OpenOrder.OrderInfo.Price.Value > 100)
                            //    {
                            //        return PredicateResult.Success;
                            //    }
                            //}


                            var pl = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle, 1);

                            //if(pl > 0)
                            //    return PredicateResult.Success;

                            var tpslInfo = ctx.ContextInfos.OfType<TPSLInfo>().Single();

                            //if(pl < -1 * tpslInfo.Sl)
                            //    return PredicateResult.Success;



                            //return PredicateResult.Fail;
                            //return (ctx.LastCandle.TimeStamp.Hour == 21).ToPredicateResult();


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
                                //var pl = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle, 1);

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

                    var pl = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle, 1);
                    var entryCandle = ctx.ContextInfos.OfType<TPSLInfo>().Single().ShadowCandle;

                    Price exitPrice = ctx.LastCandle.Ohlc.Close;
                    if (ctx.Strategy.OpenOrder is BuyOrder)
                    {
                        if (ctx.LastCandle.High - ctx.Strategy.OpenOrder.OrderInfo.Price.Value > 100)
                        {
                            exitPrice = ctx.Strategy.OpenOrder.OrderInfo.Price + 100.00.USD();
                        }
                    }


                    if (ctx.Strategy.OpenOrder is BuyOrder)
                    {
                       

                        var candle = ctx.LastCandle;
                        //var closePrice = ctx.LastCandle.ClosedAboveHigh(openTimeShadow) ? openTimeShadow.High : candle.Close;
                        ctx.Strategy.Close(
                            new SellOrder((BuyOrder)ctx.Strategy.OpenOrder,
                                new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, ctx.Strategy, exitPrice,
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

        public static double buyTp = 0;
        public static double ssTp = 0;
        public static int buyCount = 0;
        public static int ssCount = 0;

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
