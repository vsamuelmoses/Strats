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
using Carvers.Utilities;
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
                new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2017, 12, 15, 0, 0, 0, TimeSpan.Zero));

            //new DateTimeOffset(2017, 01, 31, 0, 0, 0, TimeSpan.Zero));
            var barspan = TimeSpan.FromMinutes(1);
            //var lookback = 60 * 24;
            var lookback = 1;

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
                var dailyfeed = new AggreagateCandleFeed(hourlyFeed, TimeSpan.FromDays(1)).Stream;
                var shadowCandleFeed = new ShadowCandleFeed(GlobalPaths.ShadowCandlesFor(instrument, "1D"), dailyfeed, 3);

                //var hourlyCandleFile = new FileWriter(Paths.IBDataCandlesFor(instrument, "1H").FullName, 1);
                //hourlyFeed.Subscribe(candle => hourlyCandleFile.Write(candle.ToCsv()));

                //var dailyCandleFile = new FileWriter(Paths.IBDataCandlesFor(instrument, "1D").FullName, 1);
                //dailyfeed.Subscribe(candle => dailyCandleFile.Write(candle.ToCsv()));

                var shadowCandleFile = new FileWriter(GlobalPaths.ShadowCandlesFor(instrument, "1D").FullName, 1);
                shadowCandleFeed.Stream
                    .Select(c => c.Val)
                    .DistinctUntilChanged()
                    .Subscribe(candle => shadowCandleFile.Write(candle.ToCsv()));

                var shadowStrength = new ShadowStrengthFeed(shadowCandleFeed, hourlyFeed);

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
                    new FileWriter(GlobalPaths.StrategySummaryFile(Strategy, instrument).FullName),
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
                    });

                var symbolChart = new CreateMultiPaneStockChartsViewModel(instrument, priceChart,
                    new List<Dictionary<IIndicator, IObservable<(IIndicator, DateTime, double)>>>()
                    {
                        new Dictionary<IIndicator, IObservable<(IIndicator, DateTime, double)>>()
                        {
                            {
                                CustomIndicator.Get(CustomIndicators.EquityCurveIndicator),
                                candleFeed
                                    .Select(c =>
                                    {
                                        //Debug.WriteLine($"{c.Timestamp}, {c.Val}");
                                        return c.Val;
                                    })
                                    .DistinctUntilChanged()
                                    .Select(c =>
                                {

                                    if(c == null)
                                        return (CustomIndicator.Get(CustomIndicators.EquityCurveIndicator),
                                            DateTime.MinValue, (Strategy.PL(closedOrder => closedOrder.OrderInfo.Symbol== instrument)?.Value).GetValueOrDefault());

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

                    if (candle.TimeStamp.DateTime == new DateTime(2017, 01, 10, 07, 0, 0))
                    {
                        var breakpoint = true;
                    }


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
                            if(ctx.Strategy.OpenOrder != null)
                                return PredicateResult.Fail;

                            var shadowCandle = ctx.Indicators.OfType<ShadowCandleFeed>().Single().ShadowCandle;
                            var shadowStrengthFeed = ctx.Indicators.OfType<ShadowStrengthFeed>().Single();

                            var shouldBuy = shadowStrengthFeed.ShadowStrengths.All(v => v.Val > shadowCandle.MiddlePoint() && v.Val < shadowCandle.High)
                                            && Math.Abs(shadowStrengthFeed.ShadowStrength.GetValueOrDefault() - ctx.LastCandle.Close) >= 0.0010
                                            && ctx.LastCandle.Close < shadowCandle.MiddlePoint();

                            var shouldSell = shadowStrengthFeed.ShadowStrengths.All(v => v.Val < shadowCandle.MiddlePoint() && v.Val > shadowCandle.Low)
                                             && Math.Abs(shadowStrengthFeed.ShadowStrength.GetValueOrDefault() - ctx.LastCandle.Close) >= 0.0010
                                             && ctx.LastCandle.Close > shadowCandle.MiddlePoint();

                            if (ctx.LastCandle.TimeStamp.DateTime == new DateTime(2017, 01, 10, 06, 0, 0))
                            {
                                Debug.WriteLine($"ShadowStrengths: {string.Join(",", shadowStrengthFeed.ShadowStrengths.Select(s => s.Val))}");
                                Debug.WriteLine($"ShadowCandle: {shadowCandle.ToCsv()}");
                                Debug.WriteLine($"ShadowMidPoint: {shadowCandle.MiddlePoint()}");
                                Debug.WriteLine($"Close: {ctx.LastCandle.Close}");
                            }


                            return (shouldBuy || shouldSell).ToPredicateResult();
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

                    var shouldSell = shadowStrengthFeed.ShadowStrengths.All(v => v.Val < shadowCandle.MiddlePoint() && v.Val > shadowCandle.Low)
                                     && ctx.LastCandle.Close > shadowCandle.MiddlePoint();


                    var side = shouldBuy ? Side.Buy : Side.ShortSell;
                    var entryPrice = ctx.LastCandle.Close;
                    ctx = ctx.PlaceOrder(ctx.LastCandle.TimeStamp, ctx.LastCandle, entryPrice, side);
                    return ctx;
                });

        private static Func<FuncCondition<ShadowBreakoutDiversifiedContext>> exitCondition = () =>
            new FuncCondition<ShadowBreakoutDiversifiedContext>(
                onSuccess: didPriceHitSR,
                onFailure: exitCondition,
                predicates: new List<Func<ShadowBreakoutDiversifiedContext, PredicateResult>>()
                { { ctx => PredicateResult.Success } },
                onSuccessAction: ctx =>
                {
                    if (ctx.Strategy.OpenOrder is BuyOrder)
                    {
                        var pl = ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle.High, 1);
                        var candle = ctx.LastCandle;
                        //var exitPrice = pl > 0.0040
                        //    ? ctx.Strategy.OpenOrder.OrderInfo.Price + 0.0040d.USD()
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
                        //var exitPrice = pl > 0.0040
                        //    ? ctx.Strategy.OpenOrder.OrderInfo.Price - 0.0040d.USD()
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
}
