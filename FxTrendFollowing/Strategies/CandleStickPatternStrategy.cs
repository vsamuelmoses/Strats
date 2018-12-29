using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml.Schema;
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
        public Symbol Instrument { get; }
        private string _status;
        private Subject<CandleStickPatternContext> _contextStream;
        private TraderViewModel _chartVm;
        private MultiTraderViewModel _multiTraderVm;
        private CreateMultiPaneStockChartsViewModel _traderChart;

        public CandleStickPatternStrategy(Symbol instrument)
        {
            Instrument = instrument;

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
            var shadowCandleFeed = new ShadowCandleFeed(dailyfeed, 2);

            var candleFeed = hourlyFeed;
            var context = new CandleStickPatternContext(Strategy, interestedSymbols, new List<IIndicatorFeed> { shadowCandleFeed },
                new Lookback(2, new ConcurrentQueue<Candle>()),
                interestedSymbols.Select(symbol => new TargetProfitInfo(symbol, new AvgCalculator(7), new AvgCalculator(7))).ToList());

            //TODO: for manual feed, change the candle span
            candleFeed
                .WithLatestFrom(shadowCandleFeed.Stream, Tuple.Create)
                .Subscribe(tup =>
                {
                    var candle = tup.Item1;
                    Status = $"Executing {candle.TimeStamp}";
                    var newcontext = new CandleStickPatternContext(context.Strategy, context.Symbols, context.IndicatorsFeeds, context.LookbackCandles.Add(candle), context.ContextInfos);
                    (nextCondition, context) = nextCondition().Evaluate(newcontext, candle);
                    _contextStream.OnNext(context);
                });

            StartCommand = new RelayCommand(_ =>
            {
                Ibtws.AddRealtimeDataRequests(interestedSymbols
                    .Select(symbol => Tuple.Create<int, Contract>(symbol.UniqueId, symbol.GetContract()))
                    .ToList());
            });

            StopCommand = new RelayCommand(_ =>
            {
                Strategy.Stop();
            });


            var logReport = new StrategyLogReport(new[] { Strategy }, logName: "MoBo");
            var chartReport = new StrategyChartReport(new[] { Strategy }, Dispatcher.CurrentDispatcher);
            var summaryReport = new StrategySummaryReport(new[] { Strategy });
            Reporters = new Carvers.Infra.ViewModels.Reporters(logReport, chartReport, summaryReport);

            var eventsFeed = Strategy.OpenOrders
                .Select(order => (IEvent)new OrderExecutedEvent(order.OrderInfo.TimeStamp, order))
                .Merge(Strategy.CloseddOrders
                    .Select(order => (IEvent)new OrderExecutedEvent(order.OrderInfo.TimeStamp, order)));

            var priceChart = candleFeed.Zip(_contextStream,
                (candle, tup) => ((Candle) candle,
                    tup.IndicatorsFeeds
                        .OfType<ShadowCandleFeed>()
                        .Select(i => i.ShadowCandle)
                        .Select(shadowCandle => new List<(IIndicator, double)>() {
                            (CustomIndicator.Get("Shadow Candle High"), shadowCandle.High),
                            (CustomIndicator.Get("Shadow Candle Low"), shadowCandle.Low) })
                        .SelectMany(collection => collection))
                );

            
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

    public class AvgCalculator
    {
        private readonly int _maxCount;
        private readonly List<double> values;

        public AvgCalculator(int maxCount)
        {
            values = new List<double>();
            _maxCount = maxCount;
        }
        public AvgCalculator Add(double value)
        {
            values.Add(value);
            if (values.Count > _maxCount)
            {
                values.RemoveAt(0);
                Average = values.Average();
            }

            

            return this;
        }

        public double? Average { get; private set; }
    }


    public class TargetProfitInfo : IContextInfo
    {
        public Symbol Symbol { get; }
        public AvgCalculator BuyTpCalculator { get; }
        public AvgCalculator SellTpCalculator { get; }

        public TargetProfitInfo(Symbol symbol, AvgCalculator buyTpCalculator, AvgCalculator sellTpCalculator)
        {
            Symbol = symbol;
            BuyTpCalculator = buyTpCalculator;
            SellTpCalculator = sellTpCalculator;
        }
    }


    public static class CandleStickPatternTrade
    {
        public static List<double> mrSellMaxProfit = new List<double>();
        private static List<double> mrSellMaxLoss = new List<double>();
        public static List<double> mrBuyMaxProfit = new List<double>();
        private static List<double> mrBuyMaxLoss = new List<double>();

        private static int mrSellCount;
        private static int mrBuyCount;

        private static int buyCount;
        private static double buyTp;

        private static int ssCount;
        private static double ssTp;

        //private static double takeProfit = 18d;// DAX
        //private static double takeProfit = 5d;// FTSE
        private static double takeProfit = 0.0010d;// gbpusd
        //private static double takeProfit = 0.00060d;// aud.usd

        //private static Candle previousPreviousShadow;
        //private static Candle previousShadow;
        //private static Candle currentShadow;
        //private static int candlesAfterPerfectOrder = 0;

        private static Func<FuncCondition<CandleStickPatternContext>> contextReadyCondition = () =>
            new FuncCondition<CandleStickPatternContext>(
                onSuccess: didPriceHitSR,
                onFailure: contextReadyCondition,
                predicate: context => context.IsReady().ToPredicateResult());

        private static Func<FuncCondition<CandleStickPatternContext>> didPriceHitSR = () =>
            new FuncCondition<CandleStickPatternContext>(
                onSuccess: didPriceHitSR,
                onFailure: didPriceHitSR,
                predicates: new List<Func<CandleStickPatternContext, PredicateResult>>()
                {
                    {
                        ctx => 
                        {
                            //if(ctx.LastCandle.TimeStamp.Hour > 6)
                            //    return PredicateResult.Fail;

                            if (ctx.LastCandle.TimeStamp.DateTime.Date == new DateTime(2017, 01, 16)
                                && ctx.LastCandle.TimeStamp.Hour == 06)
                            {
                                var breakpoint = true;
                            }

                            var shadowCandle = ctx.IndicatorsFeeds.OfType<ShadowCandleFeed>().Single().ShadowCandle;

                            var alreadyTradedThisHour =
                                ctx.Strategy.RecentClosedOrder?.OrderInfo.TimeStamp.DateTime.DayOfYear ==
                                ctx.LastCandle.TimeStamp.DateTime.DayOfYear;

                            if(alreadyTradedThisHour)
                                return PredicateResult.Fail;


                            var isBuy = ctx.LastCandle.PassedThroughPrice(shadowCandle.High);
                            var isSell = ctx.LastCandle.PassedThroughPrice(shadowCandle.Low);

                            return (isBuy || isSell).ToPredicateResult();

                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    var shadowCandle = ctx.IndicatorsFeeds.OfType<ShadowCandleFeed>().Single().ShadowCandle;

                    var isBuy = ctx.LastCandle.PassedThroughPrice(shadowCandle.High);
                    var isSell = ctx.LastCandle.PassedThroughPrice(shadowCandle.Low);

                    var side = isBuy ? Side.Buy : Side.ShortSell;
                    var entryPrice = isBuy ? shadowCandle.High : shadowCandle.Low;


                    var targetProfitInfo = ctx.ContextInfos.OfType<TargetProfitInfo>()
                        .Single(info => ctx.Symbols.Contains(info.Symbol));



                    if (isBuy && targetProfitInfo.BuyTpCalculator.Average.HasValue)
                    {
                        var candle = ctx.LastCandle;
                        ctx = ctx.PlaceOrder(ctx.LastCandle.TimeStamp, ctx.LastCandle, shadowCandle.High, Side.Buy);

                        var exitPrice = ctx.LastCandle.Close;

                        if (ctx.LastCandle.High - shadowCandle.High > targetProfitInfo.BuyTpCalculator.Average.Value)
                            exitPrice = (shadowCandle.High + targetProfitInfo.BuyTpCalculator.Average.Value);

                        ctx.Strategy.Close(
                            new SellOrder((BuyOrder)ctx.Strategy.OpenOrder,
                                new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, ctx.Strategy, exitPrice.USD(),
                                    100000, candle)));

                    }


                    if (isSell && targetProfitInfo.SellTpCalculator.Average.HasValue)
                    {
                        var candle = ctx.LastCandle;
                        ctx = ctx.PlaceOrder(ctx.LastCandle.TimeStamp, ctx.LastCandle, shadowCandle.Low, Side.ShortSell);

                        var exitPrice = ctx.LastCandle.Close;

                        if (shadowCandle.Low - ctx.LastCandle.Low > targetProfitInfo.SellTpCalculator.Average.Value)
                            exitPrice = (shadowCandle.Low - targetProfitInfo.SellTpCalculator.Average.Value);

                        ctx.Strategy.Close(
                            new BuyToCoverOrder((ShortSellOrder)ctx.Strategy.OpenOrder,
                                new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, ctx.Strategy,
                                    exitPrice.USD(),
                                    100000, candle)));
                    }

                    if (isBuy)
                    {
                        var profit = ctx.LastCandle.High - entryPrice;

                        if (profit > 0)
                        {
                            targetProfitInfo.BuyTpCalculator.Add(profit);
                        }
                    }

                    if (isSell)
                    {
                        var profit = entryPrice - ctx.LastCandle.Low;

                        if (profit > 0)
                        {
                            targetProfitInfo.SellTpCalculator.Add(profit);
                        }
                    }
                    
                    return ctx;
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
                            //exitCandleCount++;

                            //if (exitCandleCount < 1)
                            //    return PredicateResult.Fail;

                            var tpslInfo = ctx.ContextInfos.OfType<TPSLInfo>().Single();

                            if (ctx.Strategy.OpenOrder is BuyOrder)
                            {
                                var loss = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.Low, 1);

                                if(loss <= -1 * tpslInfo.Sl)
                                    return PredicateResult.Success;
                             
                                var profit = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.High, 1);

                                if(profit >= 0.0010)
                                    return PredicateResult.Success;
                            }

                            if (ctx.Strategy.OpenOrder is ShortSellOrder)
                            {
                                var loss = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.High, 1);

                                if(loss <= -1 * tpslInfo.Sl)
                                    return PredicateResult.Success;

                                var profit = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.Low, 1);

                                if(profit >= 0.0010)
                                    return PredicateResult.Success;
                            }

                            if(ctx.LastCandle.TimeStamp.Hour != ctx.Strategy.OpenOrder.OrderInfo.TimeStamp.Hour)
                                return PredicateResult.Success;

                            return PredicateResult.Fail;
                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    exitCandleCount = 0;

                    var pl = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle, 1);
                    var entryCandle = ctx.ContextInfos.OfType<TPSLInfo>().Single().ShadowCandle;

                    Price exitPrice = ctx.LastCandle.Ohlc.Close;


                    var tpslInfo = ctx.ContextInfos.OfType<TPSLInfo>().Single();

                    if (ctx.Strategy.OpenOrder is BuyOrder)
                    {
                        var loss = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.Low, 1);

                        if (loss <= -1 * tpslInfo.Sl)
                            exitPrice = (ctx.Strategy.OpenOrder.OrderInfo.Price.Value - tpslInfo.Sl).USD();

                        var profit = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.High, 1);

                        if (profit >= 0.0010)
                            exitPrice = (ctx.Strategy.OpenOrder.OrderInfo.Price.Value + 0.0010).USD();
                    }

                    else if (ctx.Strategy.OpenOrder is ShortSellOrder)
                    {
                        var loss = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.High, 1);

                        if (loss <= -1 * tpslInfo.Sl)
                            exitPrice = (ctx.Strategy.OpenOrder.OrderInfo.Price.Value + tpslInfo.Sl).USD();


                        var profit = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.Low, 1);

                        if (profit >= 0.0010)
                            exitPrice = (ctx.Strategy.OpenOrder.OrderInfo.Price.Value - 0.0010).USD();

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
                                new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, ctx.Strategy, exitPrice,
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
        public IEnumerable<Symbol> Symbols { get; }
        public IEnumerable<IIndicatorFeed> IndicatorsFeeds { get; }
        public Lookback LookbackCandles { get; }
        public IEnumerable<IContextInfo> ContextInfos { get; }

        public CandleStickPatternContext(Strategy strategy,
            IEnumerable<Symbol> symbols, 
            IEnumerable<IIndicatorFeed> indicators,
            Lookback lookbackCandles,
            IEnumerable<IContextInfo> contextInfos)
        {
            Strategy = strategy;
            Symbols = symbols;
            //Indicators = indicators.ToDictionary(ma => ma.Description, ma => ma);
            IndicatorsFeeds = indicators;
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
                return new CandleStickPatternContext(context.Strategy, context.Symbols, context.IndicatorsFeeds,
                    context.LookbackCandles, context.ContextInfos);
            }

            if (side == Side.Buy)
            {
                context.Strategy.Open(new BuyOrder(
                    new OrderInfo(lastCandle.TimeStamp, CurrencyPair.EURGBP, context.Strategy, lastCandle.Ohlc.Close,
                        100000)));
                return new CandleStickPatternContext(context.Strategy, context.Symbols, context.IndicatorsFeeds,
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
                return new CandleStickPatternContext(context.Strategy, context.Symbols, context.IndicatorsFeeds,
                    context.LookbackCandles, context.ContextInfos);
            }

            if (side == Side.Buy)
            {
                context.Strategy.Open(new BuyOrder(
                    new OrderInfo(timeStamp, CurrencyPair.EURGBP, context.Strategy, entryPrice.USD(),
                        100000)));
                return new CandleStickPatternContext(context.Strategy, context.Symbols, context.IndicatorsFeeds,
                    context.LookbackCandles, context.ContextInfos);
            }

            throw new Exception("unexpected error");
        }

        public static CandleStickPatternContext AddContextInfo(this CandleStickPatternContext context, IContextInfo info)
            => new CandleStickPatternContext(context.Strategy, context.Symbols, context.IndicatorsFeeds, context.LookbackCandles,
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
