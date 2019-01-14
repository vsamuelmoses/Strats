using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
    public class SymbolChartsViewModel : ViewModel
    {
        private string _summary;

        public SymbolChartsViewModel(Symbol instrument, CreateMultiPaneStockChartsViewModel chart, StrategyInstrumentSummaryReport summaryReport)
        {
            Instrument = instrument;
            Chart = chart;
            SummaryReport = summaryReport;
        }

        public Symbol Instrument { get; }
        public CreateMultiPaneStockChartsViewModel Chart { get; private set; }
        public StrategyInstrumentSummaryReport SummaryReport { get; }

        public string Summary
        {
            get => _summary;
            set
            {
                _summary = value;
                OnPropertyChanged();
            }
        }
    }

    public class ShadowBreakoutDiversified : ViewModel
    {
        private static List<double> prevShStrengthHigh = new List<double>();


        private string _status;
        private TraderViewModel _chartVm;
        private CreateMultiPaneStockChartsViewModel _traderChart;
        private readonly Dictionary<Symbol, IObservable<Tuple<Symbol, Candle, ShadowCandle>>> symbolFeeds = new Dictionary<Symbol, IObservable<Tuple<Symbol, Candle, ShadowCandle>>>();
        private readonly Dictionary<Symbol, Tuple<ShadowBreakoutDiversifiedContext, Subject<ShadowBreakoutDiversifiedContext>, Func<FuncCondition<ShadowBreakoutDiversifiedContext>>>> symbolContexts 
            = new Dictionary<Symbol, Tuple<ShadowBreakoutDiversifiedContext, Subject<ShadowBreakoutDiversifiedContext>, Func<FuncCondition<ShadowBreakoutDiversifiedContext>>>>();

        public ObservableCollection<SymbolChartsViewModel> InstrumentCharts { get; private set; }
        public ShadowBreakoutDiversified(IEnumerable<Symbol> instruments)
        {
            Instruments = instruments;

            //Ibtws = new IBTWS(); //var barspan = TimeSpan.FromSeconds(5);

            Ibtws = new IBTWSSimulator(Utility.SymbolFilePathGetter,
                new DateTimeOffset(2018, 01, 01, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2018, 12, 10, 0, 0, 0, TimeSpan.Zero));

            //new DateTimeOffset(2017, 01, 31, 0, 0, 0, TimeSpan.Zero));
            var barspan = TimeSpan.FromMinutes(1);
            //var lookback = 60 * 24;
            var lookback = 24;

            //Ibtws = new IBTWSSimulator((cxPair, dt) => Utility.FxIBDATAPathGetter(cxPair), new DateTimeOffset(2018, 04, 24, 0, 0, 0, TimeSpan.Zero));
            IbtwsViewModel = new IBTWSViewModel(Ibtws);

            Strategy = new Strategy("CandleStick Pattern");

            InstrumentCharts = new ObservableCollection<SymbolChartsViewModel>();

            foreach (var instrument in Instruments)
            {
                var originalFeed = Ibtws.RealTimeBarStream
                    .Where(msg => msg.IsForCurrencyPair(instrument))
                    .Select(msg => msg.ToCandle(barspan))
                    .Select(c => new Timestamped<Candle>(c.TimeStamp, c));

                var minuteFeed = originalFeed;
                if (barspan < TimeSpan.FromMinutes(1))
                {
                    minuteFeed = new AggreagateCandleFeed(originalFeed, TimeSpan.FromMinutes(1)).Stream;
                    //var minuteCandleFile = new FileWriter(Paths.IBDataCandlesFor(instrument, "1M").FullName, 60 * 24);
                    //minuteFeed.Subscribe(candle => minuteCandleFile.Write(candle.ToCsv()));
                }

                var hourlyFeed = new AggreagateCandleFeed(originalFeed, TimeSpan.FromHours(1)).Stream;
                //var fourHourlyFeed = new AggreagateCandleFeed(hourlyFeed, TimeSpan.FromHours(4));
                var dailyfeed = new AggreagateCandleFeed(hourlyFeed, TimeSpan.FromDays(1)).Stream;
                var shadowCandleFeed = new ShadowCandleFeed(Paths.ShadowCandlesFor(instrument, "1D"), dailyfeed, 3);

                //var hourlyCandleFile = new FileWriter(Paths.IBDataCandlesFor(instrument, "1H").FullName, 1);
                //hourlyFeed.Subscribe(candle => hourlyCandleFile.Write(candle.ToCsv()));

                //var dailyCandleFile = new FileWriter(Paths.IBDataCandlesFor(instrument, "1D").FullName, 1);
                //dailyfeed.Subscribe(candle => dailyCandleFile.Write(candle.ToCsv()));
                
                //var shadowCandleFile = new FileWriter(Paths.ShadowCandlesFor(instrument, "1D").FullName, 1);
                //shadowCandleFeed.Stream.Subscribe(candle => shadowCandleFile.Write(candle.ToCsv()));

                var shadowStrength = new ShadowStrengthFeed(shadowCandleFeed.Stream, hourlyFeed);

                var candleFeed = hourlyFeed;

                symbolFeeds.Add(instrument, 
                    candleFeed
                        .Zip(shadowCandleFeed.Stream, shadowStrength.Stream, (c, sh, strength) =>
                    {
                        Debug.Assert(c.Timestamp == sh.Timestamp);

                        //if(c.Val != null & sh.Val != null)
                        //    Debug.WriteLine($"FEED - {c.Timestamp}, {c.Val.TimeStamp}, {sh.Timestamp}, {sh.Val.TimeStamp}");
                        return Tuple.Create(instrument, c.Val, sh.Val);
                    }));

                var context = new ShadowBreakoutDiversifiedContext(Strategy, 
                    new FileWriter(Paths.StrategySummaryFile(Strategy, instrument).FullName),
                    instrument,
                    new List<IIndicatorFeed> {shadowCandleFeed, shadowStrength},
                    new Lookback(lookback, new List<Candle>()),
                    new List<IContextInfo>()
                    {
                        new TargetProfitInfo(instrument, new AvgCalculator(7), new AvgCalculator(7))
                    });

                var contextStream = new Subject<ShadowBreakoutDiversifiedContext>();
                symbolContexts.Add(instrument, Tuple.Create(context, contextStream, ShadowBreakoutDiversifiedLogic1.Strategy));

                var eventsFeed = Strategy.OpenOrders
                    .Where(order => order.OrderInfo.Symbol == instrument)
                    .Select(order => (IEvent)new OrderExecutedEvent(order.OrderInfo.TimeStamp, order))
                    .Merge(Strategy.CloseddOrders
                        .Where(order => order.OrderInfo.Symbol == instrument)
                        .Select(order => (IEvent)new OrderExecutedEvent(order.OrderInfo.TimeStamp, order)));

                var priceChart = candleFeed
                    .Zip(shadowCandleFeed.Stream, shadowStrength.Stream, Tuple.Create)
                    .Select(tup =>
                    {
                        Debug.Assert(tup.Item1.Timestamp == tup.Item2.Timestamp);
                        Debug.Assert(tup.Item1.Timestamp == tup.Item3.Timestamp);
                        return Tuple.Create(tup.Item1.Val, tup.Item2.Val, tup.Item3.Val);
                    })
                    .DistinctUntilChanged()
                    .Select(
                    (tup) =>
                    {
                        var candle = tup.Item1;
                        var shadowCandle = tup.Item2;
                        var shadowSrtength = tup.Item3;

                        var sStrength = shadowCandle.Low + shadowCandle.CandleLength() / 2 +
                                        (shadowCandle.CandleLength() * shadowSrtength.Value.Val * 0.01);

                        prevShStrengthHigh.Add(sStrength);


                        if (prevShStrengthHigh.Count > 3)
                        {
                            prevShStrengthHigh.RemoveAt(0);
                        }

                        if (shadowCandle.High > prevShStrengthHigh.First()
                            && shadowCandle.High < prevShStrengthHigh.Take(2).Last()
                            && prevShStrengthHigh.Take(2).IsInAscending()
                            && prevShStrengthHigh.TakeLast(2).IsInDescending())
                        {
                            Debug.Write((prevShStrengthHigh.Take(2).Last() - shadowCandle.High).PercentageOf(shadowCandle.CandleLength()) + ",");
                        }


                        return (candle,
                            (IEnumerable<(IIndicator,double)>)new List<(IIndicator, double)>()
                            {
                                (CustomIndicator.Get("Shadow Candle High"), shadowCandle.High),
                                (CustomIndicator.Get("Shadow Candle Middle"),
                                    shadowCandle.Low + shadowCandle.CandleLength() / 2),
                                (CustomIndicator.Get("Shadow Candle Low"), shadowCandle.Low),
                                (CustomIndicator.Get("Shadow Strength"), sStrength)
                            });
                                    //SelectMany(collection => collection)
                                    //.Concat(ctx.Indicators
                                    //    .OfType<ShadowStrengthFeed>()
                                    //    .Select(s =>
                                    //    {
                                    //        //var sStrength = s.PrevShadowCandle.Low + s.PrevShadowCandle.CandleLength() / 2 +
                                    //        //                (s.PrevShadowCandle.CandleLength() * s.PrevIndicator.Value * 0.01);
                                    //        Debug.WriteLine(ctx.LastCandle.TimeStamp + " " +
                                    //                        (s.ShadowStrength.Value - s.PrevShadowCandle.High)
                                    //                        .PercentageOf(s.PrevShadowCandle.CandleLength()));
                                    //        return 
                                    //    })));
                    });


                //var priceChart = candleFeed
                //    .Zip(contextStream, Tuple.Create)
                //    //.DistinctUntilChanged()
                //    .Select(
                //    (tup) =>
                //    {
                //        var candle = tup.Item1.Val;
                //        var ctx = tup.Item2;

                //        return (candle,
                //                ctx.Indicators
                //                    .OfType<ShadowCandleFeed>()
                //                    .Select(i => i.ShadowCandle)
                //                    .Select(shadowCandle => new List<(IIndicator, double)>()
                //                    {
                //                        (CustomIndicator.Get("Shadow Candle High"), shadowCandle.High),
                //                        (CustomIndicator.Get("Shadow Candle Middle"),
                //                            shadowCandle.Low + shadowCandle.CandleLength() / 2),
                //                        (CustomIndicator.Get("Shadow Candle Low"), shadowCandle.Low)
                //                    })
                //                    .SelectMany(collection => collection)
                //                    .Concat(ctx.Indicators
                //                        .OfType<ShadowStrengthFeed>()
                //                        .Select(s =>
                //                        {
                //                            //var sStrength = s.PrevShadowCandle.Low + s.PrevShadowCandle.CandleLength() / 2 +
                //                            //                (s.PrevShadowCandle.CandleLength() * s.PrevIndicator.Value * 0.01);
                //                            Debug.WriteLine(ctx.LastCandle.TimeStamp + " " +
                //                                            (s.ShadowStrength.Value - s.PrevShadowCandle.High)
                //                                            .PercentageOf(s.PrevShadowCandle.CandleLength()));
                //                            return (CustomIndicator.Get("Shadow Strength"), s.ShadowStrength.Value);
                //                        })));
                //    });


                var symbolChart = new CreateMultiPaneStockChartsViewModel(instrument, priceChart,
                    new List<Dictionary<IIndicator, IObservable<(IIndicator, DateTime, double)>>>()
                    {
                        new Dictionary<IIndicator, IObservable<(IIndicator, DateTime, double)>>()
                        {
                            {
                                CustomIndicator.Get(CustomIndicators.EquityCurveIndicator),
                                candleFeed
                                    .Select(c => c.Val)
                                    .DistinctUntilChanged()
                                    .Select(c =>
                                {
                                    return (CustomIndicator.Get(CustomIndicators.EquityCurveIndicator),
                                        c.TimeStamp.DateTime, (Strategy.PL(closedOrder => closedOrder.OrderInfo.Symbol== instrument)?.Value).GetValueOrDefault());
                                })
                            }
                        }
                    }, eventsFeed);

                InstrumentCharts.Add(new SymbolChartsViewModel(instrument, symbolChart, new StrategyInstrumentSummaryReport(Strategy, instrument)));
            }

            symbolFeeds.Values.ToList()
                .Merge()
                .Where(tup => tup.Item2 != null && tup.Item3 != null)
                .Select(tup => Tuple.Create(tup.Item1, tup.Item2, tup.Item3))
                .DistinctUntilChanged()
                .Subscribe(tup =>
                {
                    var instrument = tup.Item1;
                    var candle = tup.Item2;
                    var shadow = tup.Item3;

                    
                    Status = $"Executing {candle.TimeStamp} {instrument}";
                    
                    var context = symbolContexts[instrument].Item1;
                    var ctxStream = symbolContexts[instrument].Item2;
                    var condition = symbolContexts[instrument].Item3;


                    if (candle == context.LastCandle)
                        return;


                    context = new ShadowBreakoutDiversifiedContext(context.Strategy, context.LogFile, 
                        instrument, context.Indicators, context.LookbackCandles.Add(candle), context.ContextInfos);
                    var tuple = condition().Evaluate(context, candle);
                    symbolContexts[instrument] = Tuple.Create(tuple.Item2, ctxStream, tuple.Item1);
                    ctxStream.OnNext(tuple.Item2);

                    InstrumentCharts.Single(chart => chart.Instrument == instrument).Summary =
                        $"Executing {instrument} {candle.TimeStamp}. {Strategy.OpenOrder?.ToCsv()} " ;
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
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public IEngine Ibtws { get; }
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
            FileWriter logFile,
            Symbol instrument,
            IEnumerable<IIndicatorFeed> indicators,
            Lookback lookbackCandles,
            IEnumerable<IContextInfo> contextInfos)
        {
            Strategy = strategy;
            LogFile = logFile;
            Instrument = instrument;
            Indicators = indicators;
            LookbackCandles = lookbackCandles;
            ContextInfos = contextInfos;
            
            LastCandle = LookbackCandles.LastCandle;
        }

        public Candle LastCandle { get; }
        public Strategy Strategy { get; }
        public FileWriter LogFile { get; }
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
               context.LogFile.Write($"Placing SELL Order at {entryPrice}");

                var shortSellOrder = new ShortSellOrder(
                    new OrderInfo(timeStamp, context.Instrument, context.Strategy, entryPrice.USD(),
                        100000));
                context.Strategy.Open(shortSellOrder);
                return new ShadowBreakoutDiversifiedContext(context.Strategy, context.LogFile, context.Instrument, context.Indicators,
                    context.LookbackCandles, context.ContextInfos);
            }

            if (side == Side.Buy)
            {
                context.LogFile.Write($"Placing BUY Order at {entryPrice}");
                
                context.Strategy.Open(new BuyOrder(
                    new OrderInfo(timeStamp, context.Instrument, context.Strategy, entryPrice.USD(),
                        100000)));
                return new ShadowBreakoutDiversifiedContext(context.Strategy, context.LogFile, context.Instrument, context.Indicators,
                    context.LookbackCandles, context.ContextInfos);
            }

            throw new Exception("unexpected error");
        }

        public static ShadowBreakoutDiversifiedContext AddContextInfo(this ShadowBreakoutDiversifiedContext context, IContextInfo info)
            => new ShadowBreakoutDiversifiedContext(context.Strategy, context.LogFile, context.Instrument, context.Indicators, context.LookbackCandles,
                context.ContextInfos.ToList().Append(info).ToList());

        public static ShadowBreakoutDiversifiedContext ReplaceContextInfo(this ShadowBreakoutDiversifiedContext context,
            IContextInfo newInfo)
        {

            var contextInfos = context.ContextInfos.ToList();

            var oldInfo = contextInfos.SingleOrDefault(info => info.GetType() == newInfo.GetType());

            if (oldInfo != null)
                contextInfos.Remove(oldInfo);

            return new ShadowBreakoutDiversifiedContext(context.Strategy, context.LogFile, context.Instrument, context.Indicators,
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
                onSuccess: exitCondition,
                onFailure: didPriceHitSR,
                predicates: new List<Func<ShadowBreakoutDiversifiedContext, PredicateResult>>()
                {
                    {
                        ctx =>
                        {

                            var shadowCandle = ctx.Indicators.OfType<ShadowCandleFeed>().Single().ShadowCandle;

                            var isBuy = ctx.LastCandle.PassedThroughPrice(shadowCandle.High);
                            var isSell = ctx.LastCandle.PassedThroughPrice(shadowCandle.Low);

                            if (!isBuy && !isSell) 
                             return PredicateResult.Fail;

                            if (ctx.Strategy.OpenOrder != null)
                                return PredicateResult.Fail;
                            

                            //if (ctx.LastCandle.TimeStamp.DateTime.Date == new DateTime(2017, 01, 16)
                            //    && ctx.LastCandle.TimeStamp.Hour == 06)
                            //{
                            //    var breakpoint = true;
                            //}

                            
                            if (ctx.Strategy.RecentClosedOrder?.OrderInfo.TimeStamp.DayOfYear ==
                                ctx.LastCandle.TimeStamp.DateTime.DayOfYear &&
                                ctx.Strategy.RecentClosedOrder?.OrderInfo.TimeStamp.Hour ==
                                ctx.LastCandle.TimeStamp.Hour)
                            {
                                ctx.LogFile.Write(
                                    $"Shadow Candle: {shadowCandle.ToCsv()} ," +
                                    "Failed placed stop limit ," +
                                    $"Recent Closed Order in this hour: {ctx.Strategy.RecentClosedOrder?.OrderInfo.ToCsv()}");
                                return PredicateResult.Fail;
                            }

                            var recentClosedOrder = ctx.ContextInfos.OfType<RecentOrderInfo>().SingleOrDefault()?.ClosedOrder;
                            
                            var alreadyTradedThisDay = 
                                recentClosedOrder?.OrderInfo.TimeStamp.DateTime.DayOfYear ==
                                ctx.LastCandle.TimeStamp.DateTime.DayOfYear;

                            if (alreadyTradedThisDay)
                            {
                                ctx.LogFile.Write(
                                    $"Shadow Candle: {shadowCandle.ToCsv()} ," +
                                    "Failed placed stop limit ," +
                                    $"Traded this instrument for the day: {recentClosedOrder?.OrderInfo.ToCsv()}");
                                return PredicateResult.Fail;
;                           }
                            
                            if (isBuy)
                            {
                                ctx.LogFile.Write(
                                    $"Shadow Candle: {shadowCandle.ToCsv()} ," +
                                    "Shadow Candle BUY Breakout ," +
                                    $"{ctx.LastCandle.ToCsv()}");
                            }

                            if (isSell)
                            {
                                ctx.LogFile.Write(
                                    $"Shadow Candle: {shadowCandle.ToCsv()} ," +
                                    "Shadow Candle SELL Breakout ," +
                                    $"{ctx.LastCandle.ToCsv()}");
                            }

                            if (isBuy &&
                                ctx.LookbackCandles.Candles
                                    .Where(c => c.TimeStamp.Date == ctx.LastCandle.TimeStamp.Date)
                                    .First(c => c.PassedThroughPrice(shadowCandle.High)) !=
                                ctx.LastCandle)
                            {
                                return PredicateResult.Fail;
                            }

                            if (isSell &&
                                ctx.LookbackCandles.Candles
                                    .Where(c => c.TimeStamp.Date == ctx.LastCandle.TimeStamp.Date)
                                    .First(c => c.PassedThroughPrice(shadowCandle.Low)) !=
                                ctx.LastCandle)
                            {
                                return PredicateResult.Fail;
                            }


                            return (isBuy || isSell).ToPredicateResult();
                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    {
                        if (ctx.Strategy.OpenOrder != null)
                        {
                            ctx.LogFile.Write($"Could not place the order: Open Order exists, {ctx.Strategy.OpenOrder.ToCsv()}");
                            return ctx;
                        }

                        var shadowCandle = ctx.Indicators.OfType<ShadowCandleFeed>().Single().ShadowCandle;

                        var isBuy = ctx.LastCandle.PassedThroughPrice(shadowCandle.High);
                        var isSell = ctx.LastCandle.PassedThroughPrice(shadowCandle.Low);

                        var side = isBuy ? Side.Buy : Side.ShortSell;
                        var entryPrice = isBuy ? shadowCandle.High : shadowCandle.Low;

                        double? stopLoss = null;
                        if (isBuy)
                            //stopLoss = entryPrice - ctx.LookbackCandles.Candles.Min(c => c.Low);
                            stopLoss = entryPrice - ctx.LookbackCandles.Candles.TakeLast(2).First().Low;
                        else if (isSell)
                            //stopLoss = ctx.LookbackCandles.Candles.Max(c => c.High) - entryPrice;
                            stopLoss = ctx.LookbackCandles.Candles.TakeLast(2).First().High - entryPrice;

                        //stopLoss = 0.0010;
                        ctx = ctx.PlaceOrder(ctx.LastCandle.TimeStamp, ctx.LastCandle, entryPrice, side)
                            .ReplaceContextInfo(new TPSLInfo(0.0010, stopLoss.Value, shadowCandle));

                        return ctx;
                        //var targetProfitInfo = ctx.ContextInfos.OfType<TargetProfitInfo>()
                        //    .Single(info => ctx.Instrument  ==  info.Symbol);

                        //if (isBuy && targetProfitInfo.BuyTpCalculator.Average.HasValue)
                        //{
                        //    var candle = ctx.LastCandle;
                        //    ctx = ctx.PlaceOrder(ctx.LastCandle.TimeStamp, ctx.LastCandle, shadowCandle.High, Side.Buy);


                        //    var exitPrice = ctx.LastCandle.Close;

                        //    if (ctx.LastCandle.High - shadowCandle.High >
                        //        targetProfitInfo.BuyTpCalculator.Average.Value)
                        //        exitPrice = (shadowCandle.High + targetProfitInfo.BuyTpCalculator.Average.Value);

                        //    var closeOrder =
                        //        new SellOrder((BuyOrder) ctx.Strategy.OpenOrder,
                        //            new OrderInfo(candle.TimeStamp, ctx.Instrument, ctx.Strategy, exitPrice.USD(),
                        //                100000, candle));
                        //    ctx.Strategy.Close(closeOrder);

                        //    ctx = ctx.ReplaceContextInfo(new RecentOrderInfo(closeOrder));

                        //}

                        //else if (isSell && targetProfitInfo.SellTpCalculator.Average.HasValue)
                        //{
                        //    var candle = ctx.LastCandle;
                        //    ctx = ctx.PlaceOrder(ctx.LastCandle.TimeStamp, ctx.LastCandle, shadowCandle.Low,
                        //        Side.ShortSell);

                        //    var exitPrice = ctx.LastCandle.Close;

                        //    if (shadowCandle.Low - ctx.LastCandle.Low > targetProfitInfo.SellTpCalculator.Average.Value)
                        //        exitPrice = (shadowCandle.Low - targetProfitInfo.SellTpCalculator.Average.Value);


                        //    var closeOrder = new BuyToCoverOrder((ShortSellOrder) ctx.Strategy.OpenOrder,
                        //        new OrderInfo(candle.TimeStamp, ctx.Instrument, ctx.Strategy,
                        //            exitPrice.USD(),
                        //            100000, candle));
                        //    ctx.Strategy.Close(closeOrder);

                        //    ctx = ctx.ReplaceContextInfo(new RecentOrderInfo(closeOrder));
                        //}

                        //if (isBuy)
                        //{
                        //    var profit = ctx.LastCandle.High - entryPrice;

                        //    if (profit > 0)
                        //    {
                        //        targetProfitInfo.BuyTpCalculator.Add(profit);
                        //    }
                        //}
                        //else if (isSell)
                        //{
                        //    var profit = entryPrice - ctx.LastCandle.Low;

                        //    if (profit > 0)
                        //    {
                        //        targetProfitInfo.SellTpCalculator.Add(profit);
                        //    }
                        //}
                    }

                    return ctx;
                });

        private static Func<FuncCondition<ShadowBreakoutDiversifiedContext>> exitCondition = () =>
            new FuncCondition<ShadowBreakoutDiversifiedContext>(
                onSuccess: didPriceHitSR,
                onFailure: exitCondition,
                predicates: new List<Func<ShadowBreakoutDiversifiedContext, PredicateResult>>()
                {
                    {
                        ctx =>
                        {
                            if (ctx.Strategy.OpenOrder?.OrderInfo.Symbol != ctx.Instrument)
                                return PredicateResult.Success;

                            var tpslInfo = ctx.ContextInfos.OfType<TPSLInfo>().Single();

                            //if (ctx.Strategy.OpenOrder is BuyOrder)
                            //{
                            //    var loss = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.Low, 1);

                            //    if (loss <= -1 * tpslInfo.Sl)
                            //    {
                            //        ctx.LogFile.Write(
                            //            $"BUY Stop Loss triggered," +
                            //            $"SL - {tpslInfo.Sl}, Loss - {loss}," +
                            //            $"{ctx.LastCandle.ToCsv()}");
                            //        return PredicateResult.Success;
                            //    }

                            //    var profit = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.High, 1);

                            //    if (profit >= tpslInfo.Tp)
                            //    {

                            //        ctx.LogFile.Write(
                            //            $"BUY Profit triggered," +
                            //            $"TP - {tpslInfo.Tp}, Profit - {profit}," +
                            //            $"{ctx.LastCandle.ToCsv()}");
                            //        return PredicateResult.Success;
                            //    }
                            //}

                            //if (ctx.Strategy.OpenOrder is ShortSellOrder)
                            //{
                            //    var loss = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.High, 1);

                            //    if (loss <= -1 * tpslInfo.Sl)
                            //    {
                            //        if (loss <= -1 * tpslInfo.Sl)
                            //        {
                            //            ctx.LogFile.Write(
                            //                $"SELL Stop Loss triggered," +
                            //                $"SL, {tpslInfo.Sl}," +
                            //                $"{ctx.LastCandle.ToCsv()}");
                            //            return PredicateResult.Success;
                            //        }
                            //        return PredicateResult.Success;
                            //    }

                            //    var profit = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.Low, 1);

                            //    if (profit >= tpslInfo.Tp)
                            //    {
                            //        ctx.LogFile.Write(
                            //            $"SELL Profit triggered," +
                            //            $"TP, {tpslInfo.Tp}," +
                            //            $"{ctx.LastCandle.ToCsv()}");

                            //        return PredicateResult.Success;
                            //    }
                            //}

                            if (ctx.LastCandle.TimeStamp.Hour != ctx.Strategy.OpenOrder.OrderInfo.TimeStamp.Hour)
                            {
                                var pl = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.Close, 1);


                                ctx.LogFile.Write(
                                    $"Time limit triggered," +
                                    $"PL, {pl}," +
                                    $"{ctx.LastCandle.ToCsv()}");

                                return PredicateResult.Success;
                            }

                            return PredicateResult.Fail;
                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    if (ctx.Strategy.OpenOrder?.OrderInfo.Symbol != ctx.Instrument)
                        return ctx;


                    var pl = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle, 1);
                    var entryCandle = ctx.ContextInfos.OfType<TPSLInfo>().Single().ShadowCandle;

                    Price exitPrice = null;
                    var tpslInfo = ctx.ContextInfos.OfType<TPSLInfo>().Single();
                    var targetProfitInfo = ctx.ContextInfos.OfType<TargetProfitInfo>().Single();


                    if (ctx.Strategy.OpenOrder is BuyOrder)
                    {
                        var tp = targetProfitInfo.BuyTpCalculator.Average.HasValue
                            ? targetProfitInfo.BuyTpCalculator.Average.Value
                            : tpslInfo.Tp;

                        //tp = 0.0010;

                        var loss = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.Low, 1);
                        var profit = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.High, 1);

                        //if (loss <= -1 * tpslInfo.Sl)
                        //{
                        //    exitPrice = (ctx.Strategy.OpenOrder.OrderInfo.Price.Value - tpslInfo.Sl).USD();

                        //}

                        //else


                        var highestCandle = ctx.LookbackCandles.Candles.Where(c =>
                                c.TimeStamp >= ctx.Strategy.OpenOrder.OrderInfo.TimeStamp)
                            .OrderByDescending(c => c.High).First();
                        var maxProfit = highestCandle.High - ctx.Strategy.OpenOrder.OrderInfo.Price.Value;

                        Debug.WriteLine($"Max Profit achieved at HIGH of {highestCandle.TimeStamp}, {maxProfit}, {tp}");
                        
                        

                        if (ctx.LastCandle.TimeStamp.Hour != ctx.Strategy.OpenOrder.OrderInfo.TimeStamp.Hour)
                        {
                            exitPrice = ctx.LastCandle.Ohlc.Close;

                            
                            if (maxProfit > 0)
                                targetProfitInfo.BuyTpCalculator.Add(maxProfit);

                            if (maxProfit >= tp)
                            {
                                exitPrice = (ctx.Strategy.OpenOrder.OrderInfo.Price.Value + tp).USD();
                                
                            }
                        }

                    }

                    else if (ctx.Strategy.OpenOrder is ShortSellOrder)
                    {
                        var tp = targetProfitInfo.SellTpCalculator.Average.HasValue
                            ? targetProfitInfo.SellTpCalculator.Average.Value
                            : tpslInfo.Tp;

                        //tp = 0.0010;

                        var loss = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.High, 1);
                        var profit = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.Low, 1);



                        var lowestCandle = ctx.LookbackCandles.Candles.Where(c =>
                                c.TimeStamp >= ctx.Strategy.OpenOrder.OrderInfo.TimeStamp)
                            .OrderBy(c => c.Low).First();

                        var maxProfit = ctx.Strategy.OpenOrder.OrderInfo.Price.Value - lowestCandle.Low;


                        Debug.WriteLine($"Max Profit achieved at LOWEST {lowestCandle.TimeStamp}, {maxProfit}, {tp}");
                        

                        //if (loss <= -1 * tpslInfo.Sl)
                        //{
                        //    exitPrice = (ctx.Strategy.OpenOrder.OrderInfo.Price.Value + tpslInfo.Sl).USD();
                        //}

                        //else 

                        if (ctx.LastCandle.TimeStamp.Hour != ctx.Strategy.OpenOrder.OrderInfo.TimeStamp.Hour)
                        {
                            exitPrice = ctx.LastCandle.Ohlc.Close;
                        
                            if (maxProfit >= tp)
                                exitPrice = (ctx.Strategy.OpenOrder.OrderInfo.Price.Value - tp).USD();


                            if (maxProfit > 0)
                                targetProfitInfo.SellTpCalculator.Add(maxProfit);

                        }
                    }


                    if (ctx.Strategy.OpenOrder is BuyOrder)
                    {

                        var candle = ctx.LastCandle;
                        //var closePrice = ctx.LastCandle.ClosedAboveHigh(openTimeShadow) ? openTimeShadow.High : candle.Close;

                        var closedOrder = new SellOrder((BuyOrder) ctx.Strategy.OpenOrder,
                            new OrderInfo(candle.TimeStamp, ctx.Instrument, ctx.Strategy, exitPrice,
                                100000, candle));
                        ctx.Strategy.Close(closedOrder);
                        return ctx.ReplaceContextInfo(new RecentOrderInfo(closedOrder));
                    }
                    else if (ctx.Strategy.OpenOrder is ShortSellOrder)
                    {
                        var candle = ctx.LastCandle;
                        var closedOrder = new BuyToCoverOrder((ShortSellOrder) ctx.Strategy.OpenOrder,
                            new OrderInfo(candle.TimeStamp, ctx.Instrument, ctx.Strategy, exitPrice,
                                100000, candle));
                        ctx.Strategy.Close(closedOrder);
                        return ctx.ReplaceContextInfo(new RecentOrderInfo(closedOrder));
                    }

                    throw new Exception("Unexpected error");
                });

        public static Func<FuncCondition<ShadowBreakoutDiversifiedContext>> Strategy = contextReadyCondition;
    }


    public static class ShadowBreakoutDiversifiedLogic1
    {
        private static Func<FuncCondition<ShadowBreakoutDiversifiedContext>> contextReadyCondition = () =>
            new FuncCondition<ShadowBreakoutDiversifiedContext>(
                onSuccess: didPriceHitSR,
                onFailure: contextReadyCondition,
                predicate: context => context.IsReady().ToPredicateResult());

        private static Func<FuncCondition<ShadowBreakoutDiversifiedContext>> didPriceHitSR = () =>
            new FuncCondition<ShadowBreakoutDiversifiedContext>(
                onSuccess: exitCondition,
                onFailure: didPriceHitSR,
                predicates: new List<Func<ShadowBreakoutDiversifiedContext, PredicateResult>>()
                {
                    {
                        ctx =>
                        {
                            if(!(ctx.LastCandle.TimeStamp.Hour >= 9 && ctx.LastCandle.TimeStamp.Hour <= 17))
                                return PredicateResult.Fail;



                            if(ctx.Strategy.OpenOrder != null)
                                return PredicateResult.Fail;

                            //if (ctx.Strategy.RecentClosedOrder?.OrderInfo.TimeStamp.DayOfYear ==
                            //    ctx.LastCandle.TimeStamp.DateTime.DayOfYear)
                            //{
                            //    return PredicateResult.Fail;
                            //}

                            var shadowCandle = ctx.Indicators.OfType<ShadowCandleFeed>().Single().ShadowCandle;
                            var shadowStrengthFeed = ctx.Indicators.OfType<ShadowStrengthFeed>().Single();

                            //var shouldBuy = (shadowStrengthFeed.ShadowStrengths.TakeLast(3).Select(v => v.Val).IsInAscending()
                            //        && shadowStrengthFeed.ShadowStrengths.TakeLast(3).Select(v => v.Val).First() < shadowCandle.High
                            //        && shadowStrengthFeed.ShadowStrengths.TakeLast(3).Select(v => v.Val).Last() > shadowCandle.High);

                            //var something = shadowStrengthFeed.ShadowStrengths.Select(v => v.Val)
                            //    .Zip(shadowStrengthFeed.ShadowStrengths.Select(v => v.Val).Skip(1), Tuple.Create)
                            //    .Distinct()
                            //    .Select(tup =>
                            //    {
                            //        if (tup.Item1 < shadowCandle.High && tup.Item2 > shadowCandle.High)
                            //            return 1d;

                            //        if (tup.Item1 > shadowCandle.High && tup.Item2 < shadowCandle.High)
                            //            return 2d;

                            //        return 0d;

                            //    }).Where(i => i != 0d);

                            //var shouldBuy = (something.Count() >= 2 && something.IsInAscending());





                            var shouldBuy = shadowStrengthFeed.ShadowStrengths.All(v => v.Val > shadowCandle.MiddlePoint() && v.Val < shadowCandle.High)
                                            && ctx.LastCandle.Close < shadowCandle.MiddlePoint();

                            //var shouldBuy = shadowStrengthFeed.ShadowStrength > shadowCandle.MiddlePoint()
                            //                && ctx.LastCandle.Close < shadowCandle.MiddlePoint();

                            var shouldSell = shadowStrengthFeed.ShadowStrengths.All(v => v.Val < shadowCandle.MiddlePoint() && v.Val > shadowCandle.Low)
                                             && ctx.LastCandle.Close > shadowCandle.MiddlePoint();

                            //var shouldSell = false;

                            //var shouldBuy = shadowStrengthFeed.ShadowStrength  > shadowCandle.High
                            //            && (shadowStrengthFeed.ShadowStrength.Value - shadowCandle.High).PercentageOf(shadowCandle.CandleLength()) < 10;


                            //prevShStrengthHigh.Add(sStrength);





                            if (shouldBuy)
                            {

                                var breakpoint = true;
                                //Console.WriteLine($"----------{ctx.LastCandle.TimeStamp}--------------");
                                //shadowStrengthFeed.ShadowStrengths
                                //    .Foreach(v => Console.WriteLine(v));
                                //Console.WriteLine($"------------------------");

                                //shadowStrengthFeed.ShadowStrengths.TakeLast(3)
                                //    .Foreach(v => Console.WriteLine(v));


                                //Console.WriteLine(shadowCandle.ToCsv());

                                //Console.WriteLine("----------");
                            }


                            return (shouldBuy || shouldSell).ToPredicateResult();


                            //var firstTwo = shadowStrengthFeed.ShadowStrengths.TakeLast(3).Take(2).ToList();
                            //var allExceptLast =
                            //    shadowStrengthFeed.ShadowStrengths.Take(shadowStrengthFeed.ShadowStrengths.Count - 1);
                            //shouldBuy =
                            //    (allExceptLast.IsInAscending()
                            //        && firstTwo.IsInAscending()
                            //        && shadowStrengthFeed.ShadowStrengths.TakeLast(2).IsInDescending()
                            //        && firstTwo.First() < shadowCandle.High
                            //        && firstTwo.Last() > shadowCandle.High
                            //        && ctx.LastCandle.Close < shadowCandle.High);


                            


                            //var s = shadowStrengthFeed;

                            //var shouldBuy = false;
                            //if (s.ShadowStrength.Value < s.PrevShadowCandle.Low)
                            //{
                            //    var strength =(s.PrevShadowCandle.Low - s.ShadowStrength.Value)
                            //        .PercentageOf(s.PrevShadowCandle.CandleLength());

                            //    shouldBuy = (strength > 300);
                            //}

                            //var shadowStrength = shadowStrengthFeed.ShadowStrength;
                            //var shouldBuy = shadowStrength.HasValue
                            //                && ctx.LastCandle.Open < shadowStrength.Value
                            //                && ctx.LastCandle.Close > shadowStrength.Value
                            //                && shadowStrengthFeed.ShadowStrengths.All(s => s < shadowMiddlePoint);

                            return shouldBuy.ToPredicateResult();
                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    if (ctx.Strategy.OpenOrder != null)
                    {
                        ctx.LogFile.Write($"Could not place the order: Open Order exists, {ctx.Strategy.OpenOrder.ToCsv()}");
                        return ctx;
                    }
                    var shadowStrengthFeed = ctx.Indicators.OfType<ShadowStrengthFeed>().Single();
                    var shadowCandle = ctx.Indicators.OfType<ShadowCandleFeed>().Single().ShadowCandle;


                    var shouldBuy = shadowStrengthFeed.ShadowStrengths.All(v => v.Val > shadowCandle.MiddlePoint() && v.Val < shadowCandle.High)
                                    && ctx.LastCandle.Close < shadowCandle.MiddlePoint();

                    //var shouldBuy = shadowStrengthFeed.ShadowStrength > shadowCandle.MiddlePoint()
                    //                && ctx.LastCandle.Close < shadowCandle.MiddlePoint();

                    var shouldSell = shadowStrengthFeed.ShadowStrengths.All(v => v.Val < shadowCandle.MiddlePoint() && v.Val > shadowCandle.Low)
                                     && ctx.LastCandle.Close > shadowCandle.MiddlePoint();


                    var side = shouldBuy ? Side.Buy : Side.ShortSell;
                    //var entryPrice = shadowCandle.High;
                    var entryPrice = ctx.LastCandle.Close;

                    ctx = ctx.PlaceOrder(ctx.LastCandle.TimeStamp, ctx.LastCandle, entryPrice, side);
                        
                    return ctx;
                });

        private static Func<FuncCondition<ShadowBreakoutDiversifiedContext>> exitCondition = () =>
            new FuncCondition<ShadowBreakoutDiversifiedContext>(
                onSuccess: didPriceHitSR,
                onFailure: exitCondition,
                predicates: new List<Func<ShadowBreakoutDiversifiedContext, PredicateResult>>()
                {
                    {
                        ctx =>
                        {
                            return PredicateResult.Success;
                            if (ctx.Strategy.OpenOrder?.OrderInfo.Symbol != ctx.Instrument)
                                return PredicateResult.Success;

                            var shadowCandle = ctx.Indicators.OfType<ShadowCandleFeed>().Single().ShadowCandle;
                            var shadowCandles = ctx.Indicators.OfType<ShadowCandleFeed>().Single().Lookback;
                            var shadowStrengthFeed = ctx.Indicators.OfType<ShadowStrengthFeed>().Single();


                            var shadowStrength = shadowStrengthFeed.ShadowStrength;
                            var shouldSell = shadowStrength.HasValue
                                            && shadowStrength.Value < (shadowCandle.Low + shadowCandle.CandleLength() / 2)
                                            && ctx.LastCandle.Close > shadowStrength.Value;

                            return shouldSell.ToPredicateResult();
                            

                            

                            //if (ctx.LastCandle.TimeStamp.Date != ctx.Strategy.OpenOrder.OrderInfo.TimeStamp.Date)
                            //{
                            //    var pl = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.Close, 1);

                            //    ctx.LogFile.Write(
                            //        $"Time limit triggered," +
                            //        $"PL, {pl}," +
                            //        $"{ctx.LastCandle.ToCsv()}");

                            //    return PredicateResult.Success;
                            //}

                            //var shadowCandles = ctx.Indicators.OfType<ShadowCandleFeed>().Single().Lookback;


                            //var tupStrength = ctx.LookbackCandles.Candles
                            //    .Take(24)
                            //    .Select(c =>
                            //    {
                            //        var shadow =
                            //            (shadowCandles.LastOrDefault(sh => sh.TimeStamp.Date < c.TimeStamp.Date));

                            //        if (shadow == null)
                            //            return 0;

                            //        //if (ctx.LastCandle.TimeStamp.Date == new DateTime(2017, 04, 04))
                            //        //{
                            //        //    Debug.WriteLine($"APR - SH:{shadow.TimeStamp},C:{c.TimeStamp}, {c.High - shadow.Low}, {shadow.High - c.Low}");
                            //        //}

                            //        var tup = Tuple.Create((c.High - shadow.Low).PercentageOf(shadow.CandleLength()),
                            //            (shadow.High - c.Low).PercentageOf(shadow.CandleLength()));
                            //        return ((int) (tup.Item1 - tup.Item2));
                            //    }).ToList();

                            //return (tupStrength.Take(23).Average() > tupStrength.Take(24).Average())
                            //    .ToPredicateResult();
                        }
                    }
                },
                onSuccessAction: ctx =>
                {

                    if (ctx.Strategy.OpenOrder is BuyOrder)
                    {

                        var pl = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.High, 1);
                        var candle = ctx.LastCandle;
                        //var exitPrice = pl > 0.0010
                        //    ? ctx.Strategy.OpenOrder.OrderInfo.Price + 0.0010d.USD()
                        //    : ctx.LastCandle.Close.USD();
                        var exitPrice = ctx.LastCandle.Close.USD();
                        var closedOrder = new SellOrder((BuyOrder)ctx.Strategy.OpenOrder,
                            new OrderInfo(candle.TimeStamp, ctx.Instrument, ctx.Strategy, exitPrice,
                                100000, candle));
                        ctx.Strategy.Close(closedOrder);
                        return ctx.ReplaceContextInfo(new RecentOrderInfo(closedOrder));
                    }

                    if (ctx.Strategy.OpenOrder is ShortSellOrder)
                    {
                        var pl = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.Low, 1);
                        var candle = ctx.LastCandle;
                        //var exitPrice = pl > 0.0010
                        //    ? ctx.Strategy.OpenOrder.OrderInfo.Price - 0.0010d.USD()
                        //    : ctx.LastCandle.Close.USD();

                        var exitPrice = ctx.LastCandle.Close.USD();

                        var closedOrder = new BuyToCoverOrder((ShortSellOrder)ctx.Strategy.OpenOrder,
                            new OrderInfo(candle.TimeStamp, ctx.Instrument, ctx.Strategy, exitPrice,
                                100000, candle));
                        ctx.Strategy.Close(closedOrder);
                        return ctx.ReplaceContextInfo(new RecentOrderInfo(closedOrder));
                    }

                    throw new Exception("Unexpected error");
                });

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


    public static class BreakoutDiversifiedLogic
    {
        private static Func<FuncCondition<ShadowBreakoutDiversifiedContext>> contextReadyCondition = () =>
            new FuncCondition<ShadowBreakoutDiversifiedContext>(
                onSuccess: didPriceHitSR,
                onFailure: contextReadyCondition,
                predicate: context => context.IsReady().ToPredicateResult());

        private static Func<FuncCondition<ShadowBreakoutDiversifiedContext>> didPriceHitSR = () =>
            new FuncCondition<ShadowBreakoutDiversifiedContext>(
                onSuccess: exitCondition,
                onFailure: didPriceHitSR,
                predicates: new List<Func<ShadowBreakoutDiversifiedContext, PredicateResult>>()
                {
                    {
                        ctx =>
                        {
                            if(ctx.Strategy.OpenOrder != null)
                                return PredicateResult.Fail;
                            

                            if(ctx.LastCandle.TimeStamp.DayOfWeek == DayOfWeek.Sunday)
                                return PredicateResult.Fail;
                            

                            if(ctx.LastCandle.TimeStamp.Hour != 23)
                                return PredicateResult.Fail;

                            var shadowCandle = ctx.Indicators.OfType<ShadowCandleFeed>().Single().ShadowCandle;

                            var isBuy = ctx.LastCandle.Close > shadowCandle.High;
                            var isSell = ctx.LastCandle.Close < shadowCandle.Low;


                            return (isBuy || isSell).ToPredicateResult();
                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    {
                        //if (ctx.Strategy.OpenOrder != null)
                        //{
                        //    ctx.LogFile.Write($"Could not place the order: Open Order exists, {ctx.Strategy.OpenOrder.ToCsv()}");
                        //    return ctx;
                        //}

                        var shadowCandle = ctx.Indicators.OfType<ShadowCandleFeed>().Single().ShadowCandle;

                        var isBuy = ctx.LastCandle.Close > shadowCandle.High;
                        var isSell = ctx.LastCandle.Close < shadowCandle.Low;

                        var side = isBuy ? Side.Buy : Side.ShortSell;
                        var entryPrice = ctx.LastCandle.Close;

                        ctx = ctx.PlaceOrder(ctx.LastCandle.TimeStamp, ctx.LastCandle, entryPrice, side);

                        return ctx;
                    }

                    return ctx;
                });

        private static Func<FuncCondition<ShadowBreakoutDiversifiedContext>> exitCondition = () =>
            new FuncCondition<ShadowBreakoutDiversifiedContext>(
                onSuccess: didPriceHitSR,
                onFailure: exitCondition,
                predicates: new List<Func<ShadowBreakoutDiversifiedContext, PredicateResult>>()
                {
                    {
                        ctx =>
                        {
                            var shadow = ctx.Indicators.OfType<ShadowCandleFeed>().Single().ShadowCandle;

                            if (ctx.LastCandle.PassedThroughPrice(shadow.High) && ctx.Strategy.OpenOrder is ShortSellOrder)
                                return PredicateResult.Success;
                            else if (ctx.LastCandle.PassedThroughPrice(shadow.Low) && ctx.Strategy.OpenOrder is BuyOrder)
                                return PredicateResult.Success;
                            else if (ctx.LastCandle.TimeStamp.Hour == 22)
                                return PredicateResult.Success;

                            //return PredicateResult.Success;
                            return PredicateResult.Fail;
                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    //if (ctx.Strategy.OpenOrder?.OrderInfo.Symbol != ctx.Instrument)
                    //    return ctx;
                    
                    Price exitPrice = ctx.LastCandle.Ohlc.Close;

                    var shadow = ctx.Indicators.OfType<ShadowCandleFeed>().Single().ShadowCandle;

                    if (ctx.LastCandle.PassedThroughPrice(shadow.High) && ctx.Strategy.OpenOrder is ShortSellOrder)
                        exitPrice = shadow.High.USD();
                    else if (ctx.LastCandle.PassedThroughPrice(shadow.Low) && ctx.Strategy.OpenOrder is BuyOrder)
                        exitPrice = shadow.Low.USD();




                    if (ctx.Strategy.OpenOrder is BuyOrder)
                    {
                        var candle = ctx.LastCandle;
                        var closedOrder = new SellOrder((BuyOrder)ctx.Strategy.OpenOrder,
                            new OrderInfo(candle.TimeStamp, ctx.Instrument, ctx.Strategy, exitPrice,
                                100000, candle));
                        ctx.Strategy.Close(closedOrder);
                        return ctx;
                    }
                    else if (ctx.Strategy.OpenOrder is ShortSellOrder)
                    {
                        var candle = ctx.LastCandle;
                        var closedOrder = new BuyToCoverOrder((ShortSellOrder)ctx.Strategy.OpenOrder,
                            new OrderInfo(candle.TimeStamp, ctx.Instrument, ctx.Strategy, exitPrice,
                                100000, candle));
                        ctx.Strategy.Close(closedOrder);
                        return ctx;
                    }

                    throw new Exception("Unexpected error");
                });

        public static Func<FuncCondition<ShadowBreakoutDiversifiedContext>> Strategy = contextReadyCondition;
    }
}
