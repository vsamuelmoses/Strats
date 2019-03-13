using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
using Carvers.Infra;
using Carvers.Infra.Math;
using Carvers.Infra.ViewModels;
using Carvers.Models;
using Carvers.Models.DataReaders;
using Carvers.Models.Events;
using Carvers.Models.Extensions;
using Carvers.Models.Indicators;
using Carvers.Utilities;
using FxTrendFollowing.Strategies;
using ShadowStrengthStrategy.Models;

namespace ShadowStrengthStrategy.ViewModels
{
    public class SssViewModel : ViewModel
    {
        private string _status;
        private TraderViewModel _chartVm;
        private CreateMultiPaneStockChartsViewModel _traderChart;
        private readonly Dictionary<Symbol, IObservable<Tuple<Symbol, Candle>>> symbolFeeds = new Dictionary<Symbol, IObservable<Tuple<Symbol, Candle>>>();
        private readonly Dictionary<Symbol, Tuple<SssContext, Subject<SssContext>, Func<FuncCondition<SssContext>>>> symbolContexts
            = new Dictionary<Symbol, Tuple<SssContext, Subject<SssContext>, Func<FuncCondition<SssContext>>>>();

        public ObservableCollection<SymbolChartsViewModel> InstrumentCharts { get; private set; }
        public SssViewModel(IEnumerable<Symbol> instruments)
        {
            Instruments = instruments;

            Ibtws = new IBTWS();
            var barspan = TimeSpan.FromSeconds(5);

            //Ibtws = new IBTWSSimulator(Utility.SymbolFilePathGetter,
            //new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero),
            //    new DateTimeOffset(2017, 12, 15, 0, 0, 0, TimeSpan.Zero));

            //var barspan = TimeSpan.FromMinutes(1);

            IbtwsViewModel = new IBTWSViewModel(Ibtws);

            Strategy = new Strategy("SS Strategy");

            InstrumentCharts = new ObservableCollection<SymbolChartsViewModel>();

            Strategy.OpenOrders
                .Merge(Strategy.CloseddOrders)
                .Subscribe(order =>
                {
                    string side = "SELL";
                    if (order is BuyOrder || order is BuyToCoverOrder)
                        side = "BUY";

                    if(Ibtws is IBTWS)
                        Ibtws.PlaceOrder(ContractCreator.GetContract(order.OrderInfo.Symbol), OrderCreator.GetOrder(Ibtws.NextOrderId, side, "100000", "MKT", "", "DAY"));
                });


            foreach (var instrument in Instruments)
            {
                var shadowCandlesFile = GlobalPaths.ShadowCandlesFor(instrument, "1D", liveData: Ibtws is IBTWS);

                var originalFeed = Ibtws.RealTimeBarStream
                    .Where(msg => msg.IsForCurrencyPair(instrument))
                    .Select(msg => msg.ToCandle(barspan))
                    .Select(c => new Timestamped<Candle>(c.TimeStamp, c));

                var file = new FileWriter(GlobalPaths.CandleFileFor(instrument, "5S", Ibtws is IBTWS).FullName, 12);
                originalFeed.Subscribe(c =>
                {
                    Debug.WriteLine(c.Val.ToCsv());
                    file.WriteWithTs(c.Val.ToCsv());
                });

                var minuteFeed = originalFeed;
                if (barspan < TimeSpan.FromMinutes(1))
                {
                    minuteFeed = new AggreagateCandleFeed(originalFeed, TimeSpan.FromMinutes(1)).Stream;
                }

                var hourlyFeed = new AggreagateCandleFeed(originalFeed, TimeSpan.FromHours(1)).Stream;
                var dailyfeed = new AggreagateCandleFeed(hourlyFeed, TimeSpan.FromDays(1)).Stream;

                var shadowCandles = Enumerable.Empty<ShadowCandle>().ToList();
                if (File.Exists(shadowCandlesFile.FullName))
                    shadowCandles = CsvReader
                        .ReadFile(shadowCandlesFile, CsvToModelCreators.CsvToCarversShadowCandle, skip: 0)
                        .ToList();

                //var shadowStrength = new ShadowStrengthFeed(shadowCandleFeed, hourlyFeed);
                var shadowStrength = new ShadowStrengthFeed(shadowCandles, hourlyFeed);

                var candleFeed = hourlyFeed;

                symbolFeeds.Add(instrument,
                    candleFeed
                        .Zip(shadowStrength.Stream, (c, strength) =>
                        {
                            Debug.Assert(c.Timestamp == strength.Timestamp);
                            return Tuple.Create(instrument, c.Val);
                        }));

                var context = new SssContext(Strategy,
                    new FileWriter(GlobalPaths.StrategySummaryFile(Strategy, instrument, DateTime.Now.Date.ToString("yyyyMMdd")).FullName),
                    instrument,
                    GlobalPaths.ShadowCandlesFor(instrument, "1D", liveData: Ibtws is IBTWS),
                    new List<IIndicatorFeed> { shadowStrength },
                    new Lookback(1, new List<Candle>()),
                    new List<IContextInfo>()
                    {
                        new TargetProfitInfo(instrument, new AvgCalculator(7), new AvgCalculator(7))
                    });

                var contextStream = new Subject<SssContext>();
                symbolContexts.Add(instrument, Tuple.Create(context, contextStream, StrategyLogic.Strategy));

                var eventsFeed = Strategy.OpenOrders
                    .Where(order => order.OrderInfo.Symbol == instrument)
                    .Select(order => (IEvent)new OrderExecutedEvent(order.OrderInfo.TimeStamp, order))
                    .Merge(Strategy.CloseddOrders
                        .Where(order => order.OrderInfo.Symbol == instrument)
                        .Select(order => (IEvent)new OrderExecutedEvent(order.OrderInfo.TimeStamp, order)));

                var priceChart = candleFeed
                    .Zip(shadowStrength.Stream, Tuple.Create)
                    .Select(tup =>
                    {
                        Debug.Assert(tup.Item1.Timestamp == tup.Item2.Timestamp);
                        return Tuple.Create(tup.Item1.Val, tup.Item2.Val);
                    })
                    .DistinctUntilChanged()
                    .Select(
                    (tup) =>
                    {
                        var candle = tup.Item1;
                        var shadowCandle = ShadowCandle.Null;
                        if(candle != null)
                            shadowCandle = shadowCandles.LastOrDefault(sh => sh.TimeStamp.Date < candle.TimeStamp.Date);
                        var shadowSrtength = tup.Item2;

                        var sStrength = shadowCandle.Low + shadowCandle.CandleLength() / 2 +
                                        (shadowCandle.CandleLength() * shadowSrtength.Value.Val * 0.01);

                        return (candle,
                            (IEnumerable<(IIndicator, double)>)new List<(IIndicator, double)>()
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
                .Where(tup => tup.Item2 != null)
                .Select(tup => Tuple.Create(tup.Item1, tup.Item2))
                .DistinctUntilChanged()
                .Subscribe(tup =>
                {
                    var instrument = tup.Item1;
                    var candle = tup.Item2;

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

                    var shadowCandlesFile = GlobalPaths.ShadowCandlesFor(instrument, "1D", liveData: Ibtws is IBTWS);

                    context = new SssContext(context.Strategy, context.LogFile, 
                        instrument, shadowCandlesFile, context.Indicators, context.LookbackCandles.Add(candle), context.ContextInfos);
                    var tuple = condition().Evaluate(context, candle);
                    symbolContexts[instrument] = Tuple.Create(tuple.Item2, ctxStream, tuple.Item1);
                    ctxStream.OnNext(tuple.Item2);

                    InstrumentCharts.Single(chart => chart.Instrument == instrument).Summary =
                        $"Executing {instrument} {candle.TimeStamp}. {Strategy.OpenOrder?.ToCsv()} ";
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

        public Reporters Reporters { get; }
    }

}
