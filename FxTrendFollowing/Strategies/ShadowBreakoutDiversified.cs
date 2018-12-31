using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.Remoting.Messaging;
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
    public class ShadowBreakoutDiversified : ViewModel
    {
        private string _status;
        private TraderViewModel _chartVm;
        private MultiTraderViewModel _multiTraderVm;
        private CreateMultiPaneStockChartsViewModel _traderChart;
        private readonly Dictionary<Symbol, IObservable<Tuple<Symbol, Candle, ShadowCandle>>> symbolFeeds = new Dictionary<Symbol, IObservable<Tuple<Symbol, Candle, ShadowCandle>>>();
        private readonly Dictionary<Symbol, Tuple<ShadowBreakoutDiversifiedContext, Subject<ShadowBreakoutDiversifiedContext>, Func<FuncCondition<ShadowBreakoutDiversifiedContext>>>> symbolContexts 
            = new Dictionary<Symbol, Tuple<ShadowBreakoutDiversifiedContext, Subject<ShadowBreakoutDiversifiedContext>, Func<FuncCondition<ShadowBreakoutDiversifiedContext>>>>();

        public ObservableCollection<CreateMultiPaneStockChartsViewModel> InstrumentCharts { get; private set; }
        public ShadowBreakoutDiversified(IEnumerable<Symbol> instruments)
        {
            Instruments = instruments;

            Ibtws = new IBTWSSimulator(Utility.SymbolFilePathGetter,
                new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2017, 12, 10, 0, 0, 0, TimeSpan.Zero));
            //new DateTimeOffset(2017, 01, 31, 0, 0, 0, TimeSpan.Zero));
            //Ibtws = new IBTWSSimulator((cxPair, dt) => Utility.FxIBDATAPathGetter(cxPair), new DateTimeOffset(2018, 04, 24, 0, 0, 0, TimeSpan.Zero));
            IbtwsViewModel = new IBTWSViewModel(Ibtws);

            Strategy = new Strategy("CandleStick Pattern");

            var nextCondition = ShadowBreakoutLogic.Strategy;

            InstrumentCharts = new ObservableCollection<CreateMultiPaneStockChartsViewModel>();

            foreach (var instrument in Instruments)
            {
                var minuteFeed = Ibtws.RealTimeBarStream
                    .Where(msg => msg.IsForCurrencyPair(instrument))
                    .Select(msg => msg.ToCandle(TimeSpan.FromMinutes(1)));
                var hourlyFeed = new AggreagateCandleFeed(minuteFeed, TimeSpan.FromHours(1)).Stream;
                var dailyfeed = new AggreagateCandleFeed(minuteFeed, TimeSpan.FromDays(1)).Stream;
                var shadowCandleFeed = new ShadowCandleFeed(dailyfeed, 2);

                var candleFeed = hourlyFeed;

                symbolFeeds.Add(instrument, candleFeed.WithLatestFrom(shadowCandleFeed.Stream, (c, sh) => Tuple.Create(instrument, c, sh)));

                var context = new ShadowBreakoutDiversifiedContext(Strategy, instrument,
                    new List<IIndicatorFeed> {shadowCandleFeed},
                    new Lookback(2, new ConcurrentQueue<Candle>()),
                    new List<IContextInfo>()
                    {
                        new TargetProfitInfo(instrument, new AvgCalculator(7), new AvgCalculator(7))
                    });

                var contextStream = new Subject<ShadowBreakoutDiversifiedContext>();
                symbolContexts.Add(instrument, Tuple.Create(context, contextStream, ShadowBreakoutDiversifiedLogic.Strategy));

                var eventsFeed = Strategy.OpenOrders
                    .Where(order => order.OrderInfo.Symbol == instrument)
                    .Select(order => (IEvent)new OrderExecutedEvent(order.OrderInfo.TimeStamp, order))
                    .Merge(Strategy.CloseddOrders
                        .Where(order => order.OrderInfo.Symbol == instrument)
                        .Select(order => (IEvent)new OrderExecutedEvent(order.OrderInfo.TimeStamp, order)));
                
                var priceChart = candleFeed.Zip(contextStream,
                    (candle, tup) => ((Candle)candle,
                        tup.Indicators
                            .OfType<ShadowCandleFeed>()
                            .Select(i => i.ShadowCandle)
                            .Select(shadowCandle => new List<(IIndicator, double)>() {
                                (CustomIndicator.Get("Shadow Candle High"), shadowCandle.High),
                                (CustomIndicator.Get("Shadow Candle Low"), shadowCandle.Low) })
                            .SelectMany(collection => collection))
                );


                var symbolChart = new CreateMultiPaneStockChartsViewModel(instrument, priceChart,
                    new List<Dictionary<IIndicator, IObservable<(IIndicator, DateTime, double)>>>()
                    {
                        new Dictionary<IIndicator, IObservable<(IIndicator, DateTime, double)>>()
                        {
                            {
                                CustomIndicator.Get(CustomIndicators.EquityCurveIndicator),
                                candleFeed.Select(c =>
                                {
                                    return (CustomIndicator.Get(CustomIndicators.EquityCurveIndicator),
                                        c.TimeStamp.DateTime, (Strategy.PL(closedOrder => closedOrder.OrderInfo.Symbol== instrument)?.Value).GetValueOrDefault());
                                })
                            }
                        }
                    }, eventsFeed);

                InstrumentCharts.Add(symbolChart);
            }

            symbolFeeds.Values.ToList()
                .Merge()
                .Subscribe(tup =>
                {
                    var instrument = tup.Item1;
                    var candle = tup.Item2;
                    var shadow = tup.Item3;

                    Status = $"Executing {candle.TimeStamp} {instrument}";
                    
                    var context = symbolContexts[instrument].Item1;
                    var ctxStream = symbolContexts[instrument].Item2;
                    var condition = symbolContexts[instrument].Item3;

                    context = new ShadowBreakoutDiversifiedContext(context.Strategy, instrument, context.Indicators, context.LookbackCandles.Add(candle), context.ContextInfos);
                    var tuple = condition().Evaluate(context, candle);
                    symbolContexts[instrument] = Tuple.Create(tuple.Item2, ctxStream, tuple.Item1);
                    ctxStream.OnNext(tuple.Item2);
                });

            StartCommand = new RelayCommand(_ =>
            {
                Ibtws.AddRealtimeDataRequests(Instruments
                    .Select(symbol => Tuple.Create(symbol.UniqueId, symbol.GetContract()))
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

            

        }



        public IEnumerable<Symbol> Instruments { get; private set; }

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

    public class ShadowBreakoutDiversifiedContext : IContext
    {
        public ShadowBreakoutDiversifiedContext(Strategy strategy,
            Symbol instrument,
            IEnumerable<IIndicatorFeed> indicators,
            Lookback lookbackCandles,
            IEnumerable<IContextInfo> contextInfos)
        {
            Strategy = strategy;
            Instrument = instrument;
            Indicators = indicators;
            LookbackCandles = lookbackCandles;
            ContextInfos = contextInfos;
            
            LastCandle = LookbackCandles.LastCandle;
        }

        public Candle LastCandle { get; }
        public Strategy Strategy { get; }
        public Symbol Instrument { get; }
        public IEnumerable<IIndicatorFeed> Indicators { get; }
        public Lookback LookbackCandles { get; }
        public IEnumerable<IContextInfo> ContextInfos { get; }

        public bool IsReady() => true;
    }

    public static class ShadowBreakoutDiversifiedContextExtensions
    {
        public static ShadowBreakoutDiversifiedContext PlaceOrder(this ShadowBreakoutDiversifiedContext context, DateTimeOffset timeStamp, Candle candle, double entryPrice,
            Side side)
        {
            if (side == Side.ShortSell)
            {
                var shortSellOrder = new ShortSellOrder(
                    new OrderInfo(timeStamp, context.Instrument, context.Strategy, entryPrice.USD(),
                        100000));
                context.Strategy.Open(shortSellOrder);
                return new ShadowBreakoutDiversifiedContext(context.Strategy, context.Instrument, context.Indicators,
                    context.LookbackCandles, context.ContextInfos);
            }

            if (side == Side.Buy)
            {
                context.Strategy.Open(new BuyOrder(
                    new OrderInfo(timeStamp, context.Instrument, context.Strategy, entryPrice.USD(),
                        100000)));
                return new ShadowBreakoutDiversifiedContext(context.Strategy, context.Instrument, context.Indicators,
                    context.LookbackCandles, context.ContextInfos);
            }

            throw new Exception("unexpected error");
        }

        public static ShadowBreakoutDiversifiedContext AddContextInfo(this ShadowBreakoutDiversifiedContext context, IContextInfo info)
            => new ShadowBreakoutDiversifiedContext(context.Strategy, context.Instrument, context.Indicators, context.LookbackCandles,
                context.ContextInfos.ToList().Append(info).ToList());

        public static ShadowBreakoutDiversifiedContext ReplaceContextInfo(this ShadowBreakoutDiversifiedContext context,
            IContextInfo newInfo)
        {

            var contextInfos = context.ContextInfos.ToList();

            var oldInfo = contextInfos.SingleOrDefault(info => info.GetType() == newInfo.GetType());

            if (oldInfo != null)
                contextInfos.Remove(oldInfo);

            return new ShadowBreakoutDiversifiedContext(context.Strategy, context.Instrument, context.Indicators,
                context.LookbackCandles,
                contextInfos.Append(newInfo).ToList());
        }

    }

    public static class ShadowBreakoutDiversifiedLogic
    {
        private static Func<FuncCondition<ShadowBreakoutDiversifiedContext>> contextReadyCondition = () =>
            new FuncCondition<ShadowBreakoutDiversifiedContext>(
                onSuccess: didPriceHitSR,
                onFailure: contextReadyCondition,
                predicate: context => context.IsReady().ToPredicateResult());

        private static Func<FuncCondition<ShadowBreakoutDiversifiedContext>> didPriceHitSR = () =>
            new FuncCondition<ShadowBreakoutDiversifiedContext>(
                onSuccess: didPriceHitSR,
                onFailure: didPriceHitSR,
                predicates: new List<Func<ShadowBreakoutDiversifiedContext, PredicateResult>>()
                {
                    {
                        ctx =>
                        {
                            if (ctx.LastCandle.TimeStamp.DateTime.Date == new DateTime(2017, 01, 16)
                                && ctx.LastCandle.TimeStamp.Hour == 06)
                            {
                                var breakpoint = true;
                            }

                            var shadowCandle = ctx.Indicators.OfType<ShadowCandleFeed>().Single().ShadowCandle;

                            if (ctx.Strategy.RecentClosedOrder?.OrderInfo.TimeStamp.DayOfYear ==
                                ctx.LastCandle.TimeStamp.DateTime.DayOfYear &&
                                ctx.Strategy.RecentClosedOrder?.OrderInfo.TimeStamp.Hour ==
                                ctx.LastCandle.TimeStamp.Hour)
                                return PredicateResult.Fail;

                            var recentClosedOrder = ctx.ContextInfos.OfType<RecentOrderInfo>().SingleOrDefault()?.ClosedOrder;
                            
                            var alreadyTradedThisDay = 
                                recentClosedOrder?.OrderInfo.TimeStamp.DateTime.DayOfYear ==
                                ctx.LastCandle.TimeStamp.DateTime.DayOfYear;

                            if(alreadyTradedThisDay)
                                return PredicateResult.Fail;
                            
                            var isBuy = ctx.LastCandle.PassedThroughPrice(shadowCandle.High);
                            var isSell = ctx.LastCandle.PassedThroughPrice(shadowCandle.Low);

                            return (isBuy || isSell).ToPredicateResult();

                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    {
                        if (ctx.Strategy.OpenOrder != null)
                            return ctx;

                        var shadowCandle = ctx.Indicators.OfType<ShadowCandleFeed>().Single().ShadowCandle;

                        var isBuy = ctx.LastCandle.PassedThroughPrice(shadowCandle.High);
                        var isSell = ctx.LastCandle.PassedThroughPrice(shadowCandle.Low);

                        var side = isBuy ? Side.Buy : Side.ShortSell;
                        var entryPrice = isBuy ? shadowCandle.High : shadowCandle.Low;
                        
                        var targetProfitInfo = ctx.ContextInfos.OfType<TargetProfitInfo>()
                            .Single(info => ctx.Instrument  ==  info.Symbol);

                        if (isBuy && targetProfitInfo.BuyTpCalculator.Average.HasValue)
                        {
                            var candle = ctx.LastCandle;
                            ctx = ctx.PlaceOrder(ctx.LastCandle.TimeStamp, ctx.LastCandle, shadowCandle.High, Side.Buy);

                            var exitPrice = ctx.LastCandle.Close;

                            if (ctx.LastCandle.High - shadowCandle.High >
                                targetProfitInfo.BuyTpCalculator.Average.Value)
                                exitPrice = (shadowCandle.High + targetProfitInfo.BuyTpCalculator.Average.Value);

                            var closeOrder =
                                new SellOrder((BuyOrder) ctx.Strategy.OpenOrder,
                                    new OrderInfo(candle.TimeStamp, ctx.Instrument, ctx.Strategy, exitPrice.USD(),
                                        100000, candle));
                            ctx.Strategy.Close(closeOrder);

                            ctx = ctx.ReplaceContextInfo(new RecentOrderInfo(closeOrder));

                        }

                        else if (isSell && targetProfitInfo.SellTpCalculator.Average.HasValue)
                        {
                            var candle = ctx.LastCandle;
                            ctx = ctx.PlaceOrder(ctx.LastCandle.TimeStamp, ctx.LastCandle, shadowCandle.Low,
                                Side.ShortSell);

                            var exitPrice = ctx.LastCandle.Close;

                            if (shadowCandle.Low - ctx.LastCandle.Low > targetProfitInfo.SellTpCalculator.Average.Value)
                                exitPrice = (shadowCandle.Low - targetProfitInfo.SellTpCalculator.Average.Value);


                            var closeOrder = new BuyToCoverOrder((ShortSellOrder) ctx.Strategy.OpenOrder,
                                new OrderInfo(candle.TimeStamp, ctx.Instrument, ctx.Strategy,
                                    exitPrice.USD(),
                                    100000, candle));
                            ctx.Strategy.Close(closeOrder);

                            ctx = ctx.ReplaceContextInfo(new RecentOrderInfo(closeOrder));
                        }

                        if (isBuy)
                        {
                            var profit = ctx.LastCandle.High - entryPrice;

                            if (profit > 0)
                            {
                                targetProfitInfo.BuyTpCalculator.Add(profit);
                            }
                        }
                        else if (isSell)
                        {
                            var profit = entryPrice - ctx.LastCandle.Low;

                            if (profit > 0)
                            {
                                targetProfitInfo.SellTpCalculator.Add(profit);
                            }
                        }
                    }

                    return ctx;
                });

        //public static int exitCandleCount = 0;

        //public static int count = 0;

        //private static Func<FuncCondition<ShadowBreakoutContext>> exitCondition = () =>
        //    new FuncCondition<ShadowBreakoutContext>(
        //        onSuccess: didPriceHitSR,
        //        onFailure: exitCondition,
        //        predicates: new List<Func<ShadowBreakoutContext, PredicateResult>>()
        //        {
        //            {
        //                ctx =>
        //                {
        //                    //exitCandleCount++;

        //                    //if (exitCandleCount < 1)
        //                    //    return PredicateResult.Fail;

        //                    var tpslInfo = ctx.ContextInfos.OfType<TPSLInfo>().Single();

        //                    if (ctx.Strategy.OpenOrder is BuyOrder)
        //                    {
        //                        var loss = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.Low, 1);

        //                        if(loss <= -1 * tpslInfo.Sl)
        //                            return PredicateResult.Success;

        //                        var profit = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.High, 1);

        //                        if(profit >= 0.0010)
        //                            return PredicateResult.Success;
        //                    }

        //                    if (ctx.Strategy.OpenOrder is ShortSellOrder)
        //                    {
        //                        var loss = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.High, 1);

        //                        if(loss <= -1 * tpslInfo.Sl)
        //                            return PredicateResult.Success;

        //                        var profit = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.Low, 1);

        //                        if(profit >= 0.0010)
        //                            return PredicateResult.Success;
        //                    }

        //                    if(ctx.LastCandle.TimeStamp.Hour != ctx.Strategy.OpenOrder.OrderInfo.TimeStamp.Hour)
        //                        return PredicateResult.Success;

        //                    return PredicateResult.Fail;
        //                }
        //            }
        //        },
        //        onSuccessAction: ctx =>
        //        {
        //            exitCandleCount = 0;

        //            var pl = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle, 1);
        //            var entryCandle = ctx.ContextInfos.OfType<TPSLInfo>().Single().ShadowCandle;

        //            Price exitPrice = ctx.LastCandle.Ohlc.Close;


        //            var tpslInfo = ctx.ContextInfos.OfType<TPSLInfo>().Single();

        //            if (ctx.Strategy.OpenOrder is BuyOrder)
        //            {
        //                var loss = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.Low, 1);

        //                if (loss <= -1 * tpslInfo.Sl)
        //                    exitPrice = (ctx.Strategy.OpenOrder.OrderInfo.Price.Value - tpslInfo.Sl).USD();

        //                var profit = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.High, 1);

        //                if (profit >= 0.0010)
        //                    exitPrice = (ctx.Strategy.OpenOrder.OrderInfo.Price.Value + 0.0010).USD();
        //            }

        //            else if (ctx.Strategy.OpenOrder is ShortSellOrder)
        //            {
        //                var loss = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.High, 1);

        //                if (loss <= -1 * tpslInfo.Sl)
        //                    exitPrice = (ctx.Strategy.OpenOrder.OrderInfo.Price.Value + tpslInfo.Sl).USD();


        //                var profit = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.Low, 1);

        //                if (profit >= 0.0010)
        //                    exitPrice = (ctx.Strategy.OpenOrder.OrderInfo.Price.Value - 0.0010).USD();

        //            }

        //            if (ctx.Strategy.OpenOrder is BuyOrder)
        //            {

        //                var candle = ctx.LastCandle;
        //                //var closePrice = ctx.LastCandle.ClosedAboveHigh(openTimeShadow) ? openTimeShadow.High : candle.Close;
        //                ctx.Strategy.Close(
        //                    new SellOrder((BuyOrder)ctx.Strategy.OpenOrder,
        //                        new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, ctx.Strategy, exitPrice,
        //                            100000, candle)));
        //                return ctx;
        //            }

        //            if (ctx.Strategy.OpenOrder is ShortSellOrder)
        //            {
        //                var candle = ctx.LastCandle;
        //                ctx.Strategy.Close(
        //                    new BuyToCoverOrder((ShortSellOrder)ctx.Strategy.OpenOrder,
        //                        new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, ctx.Strategy, exitPrice,
        //                            100000, candle)));
        //                return ctx;
        //            }

        //            throw new Exception("Unexpected error");
        //        });

        public static Func<FuncCondition<ShadowBreakoutDiversifiedContext>> Strategy = contextReadyCondition;
    }

    public class RecentOrderInfo : IContextInfo
    {
        public IClosedOrder ClosedOrder { get; }

        public RecentOrderInfo(IClosedOrder closedOrder)
        {
            ClosedOrder = closedOrder;
        }
    }

    
}
