using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.Remoting.Messaging;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Carvers.Charting.MultiPane;
using Carvers.Charting.ViewModels;
using Carvers.IB.App;
using Carvers.IBApi;
using Carvers.IBApi.Extensions;
using Carvers.Infra;
using Carvers.Infra.Extensions;
using Carvers.Infra.Math;
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
        private Stopwatch stopwatch;

        private string _status;
        private TraderViewModel _chartVm;
        private readonly Dictionary<Symbol, IObservable<Tuple<Symbol, Candle, ShadowCandle>>> symbolFeeds =
            new Dictionary<Symbol, IObservable<Tuple<Symbol, Candle, ShadowCandle>>>();

        private readonly Dictionary<Symbol, IObservable<Tuple<Symbol, Candle, ShadowCandle, double, double>>> symbolFeedsAgg =
            new Dictionary<Symbol, IObservable<Tuple<Symbol, Candle, ShadowCandle, double, double>>>();
        public ObservableCollection<SymbolChartsViewModel> InstrumentCharts { get; private set; }
        public ShadowBreakoutDiversified(IEnumerable<Symbol> instruments)
        {
            stopwatch = new Stopwatch();
            Instruments = instruments;

            //Ibtws = new IBTWSSimulator(Utility.SymbolFilePathIbDataGetter,
            //    new DateTimeOffset(2019, 02, 01, 0, 0, 0, TimeSpan.Zero),
            //    new DateTimeOffset(2019, 02, 27, 0, 0, 0, TimeSpan.Zero));

            //var barspan = TimeSpan.FromSeconds(5);


            //Ibtws = new IBTWSSimulator(Utility.SymbolFilePathGetter,
            //    new DateTimeOffset(2018, 1, 01, 0, 0, 0, TimeSpan.Zero),
            //    new DateTimeOffset(2018, 12, 27, 0, 0, 0, TimeSpan.Zero));

            //var barspan = TimeSpan.FromMinutes(1);

            Ibtws = new IBTWS();
            var barspan = TimeSpan.FromSeconds(5);

            IbtwsViewModel = new IBTWSViewModel(Ibtws);

            Strategy = new Strategy("CandleStick Pattern");

            InstrumentCharts = new ObservableCollection<SymbolChartsViewModel>();

            Strategy.OpenOrders
               .Merge(Strategy.CloseddOrders)
               .Subscribe(order =>
               {
                   string side = "SELL";
                   if (order is BuyOrder || order is BuyToCoverOrder)
                       side = "BUY";

                   if (Ibtws is IBTWS)
                       Ibtws.PlaceOrder(ContractCreator.GetContract(order.OrderInfo.Symbol), OrderCreator.GetOrder(Ibtws.NextOrderId, side, "100000", "MKT", "", "DAY"));
               });



            var feed = Ibtws.RealTimeBarStream
                .Select(msg => Tuple.Create(msg.ToCurrencyPair(), msg.ToCandle(barspan)))
                .Publish()
                .RefCount();

            var exchangeRateGetter = new ExchangeRateGetter(feed);

            foreach (var instrument in Instruments)
            {
                var scheduler = new EventLoopScheduler();
                ExecuteStrategy(feed, barspan, instrument, scheduler);
            }

            StartCommand = new RelayCommand(_ =>
            {
                stopwatch.Start();
                Ibtws.AddRealtimeDataRequests(Instruments
                    .Select(symbol => Tuple.Create(symbol.UniqueId, symbol.GetContract()))
                    .ToList());
            });

            StopCommand = new RelayCommand(_ => { Strategy.Stop(); });


            var logReport = new StrategyLogReport(new[] { Strategy }, logName: "MoBo");
            var chartReport = new StrategyChartReport(new[] { Strategy }, Dispatcher.CurrentDispatcher);
            var summaryReport = new StrategySummaryReport(new[] { Strategy });
            emailReport = new StrategyEmailReport(new[] { Strategy });
            Reporters = new Carvers.Infra.ViewModels.Reporters(logReport, chartReport, summaryReport);
        }


        private void ExecuteStrategy(IObservable<Tuple<CurrencyPair, Candle>> feed, TimeSpan barspan, Symbol instrument, IScheduler scheduler)
        {
            var summaryLog = new FileWriter(GlobalPaths.StrategySummaryFile(Strategy, instrument).FullName, 24);

            var originalFeed = feed.Where(f => f.Item1 == instrument)
                .Select(f => f.Item2)
                .Publish()
                .RefCount();

            var hourlyStream = originalFeed.ToHourlyStream();
            var dailyStream = hourlyStream.ToDailyStream();

            hourlyStream
                .Subscribe(c =>
            {
                var openOrder = Strategy.OpenedOrders.SingleOrDefault(order => order.OrderInfo.Symbol == instrument);
                if (openOrder != null)
                {
                    var closeOrder = openOrder.GetCloseOrder(Strategy, double.MaxValue, double.MaxValue, c.Close, c,
                            ExchangeRateGetter.GetExchangeRate);


                    //if(closeOrder.Item2.ProfitLoss.Value < 0 ||
                    ////if(closeOrder.Item1 != OrderExtensions.CloseOrderTrigger.TimeLimit 
                    //    c.TimeStamp - openOrder.OrderInfo.TimeStamp > TimeSpan.FromHours(2))
                        Strategy.Close(closeOrder.Item2);

                }
            });


            IObservable<IEnumerable<ShadowCandle>> shadowCandlesStream = Observable.Empty<IEnumerable<ShadowCandle>>();
            IObservable<ShadowCandle> shadowFeed = Observable.Empty<ShadowCandle>(); ;

            if (Ibtws is IBTWS)
            {
                var file = new FileWriter(GlobalPaths.CandleFileFor(instrument, "5S", Ibtws is IBTWS).FullName, 12);
                originalFeed.Subscribe(c => file.WriteWithTs(c.ToCsv()));

                var shadowCandlesFile = GlobalPaths.ShadowCandlesFor(instrument, "1D", liveData: true);
                if (File.Exists(shadowCandlesFile.FullName))
                {
                    var shadows = CsvReader
                        .ReadFile(shadowCandlesFile, CsvToModelCreators.CsvToCarversShadowCandle, skip: 0)
                        .ToList()
                        .TakeLast(8);

                    shadowFeed = shadows.ToObservable();

                    shadowCandlesStream = Observable.Return(shadows);
                }
            }
            else
            {
                shadowFeed = dailyStream
                    .Scan(ShadowCandle.Null, GetShadowCandle)
                    .Publish()
                    .RefCount();

                /* Multiple Shaodows */

                shadowCandlesStream = shadowFeed
                    .Buffer(8, 1)
                    .Publish()
                    .RefCount();
            }

           var strengthFeeds = hourlyStream
                .WithLatestFrom(shadowCandlesStream, (c, shadows) =>
                {
                    return shadows.Select(shadow => GetShadowStrengthFeed(instrument, c, shadow));
                })
                .Publish()
                .RefCount();


            strengthFeeds
                .Buffer(4, skip: 1)
                .Subscribe(tups =>
                {
                    tups.SelectMany(tup => tup)
                    .GroupBy(t => t.shadow)
                    .Where(t => t.Count() == 4)
                    .Foreach(t =>
                    {
                        //var sentimentBuy = tups.SelectMany(tup => tup).Select(tu => tu.shadow.MiddlePoint())
                        //    .TakeLast(2)
                        //    .IsInAscending();

                        //var sentimentSell = tups.SelectMany(tup => tup).Select(tu => tu.shadow.MiddlePoint())
                        //    .TakeLast(2)
                        //    .IsInDescending();

                        

                        var buyOrSell = ComputeStrategy(t, true, true);
                        Log(t, buyOrSell, summaryLog);
                    });

                    //tups.Foreach(tup => { tup.Foreach(t => Debug.WriteLine($"Candle: {t.candle.TimeStamp}, Shadow: {t.shadow.TimeStamp}")); });
                    //tups.Foreach(ComputeStrategy);
                });


            var strengthFeed = hourlyStream
                .WithLatestFrom(shadowFeed, (candle, shadow) => GetShadowStrengthFeed(instrument, candle, shadow))
                .Publish()
                .RefCount();

            var eventsFeed = Strategy.OpenOrders
                .Where(order => order.OrderInfo.Symbol == instrument)
                .Select(order => (IEvent)new OrderExecutedEvent(order.OrderInfo.TimeStamp, order))
                .Merge(Strategy.CloseddOrders
                    .Where(order => order.OrderInfo.Symbol == instrument)
                    .Select(order => (IEvent)new OrderExecutedEvent(order.OrderInfo.TimeStamp, order)));

            var shadowMidPoints = strengthFeeds
                .Select(tup =>
                {
                    var midPoints = Enumerable.Range(1, 8)
                        .Zip(tup.Select(t => t.shadow.MiddlePoint()), Tuple.Create)
                        .Select(tupp => (CustomIndicator.Get($"Shadow{tupp.Item1}"), tupp.Item2));

                    return (tup.First().candle, midPoints);
                });

            var priceChart = strengthFeed
                .Select(tup =>
                {
                    return (tup.candle,
                        (IEnumerable<(IIndicator, double)>)new List<(IIndicator, double)>()
                        {
                            (CustomIndicator.Get("Shadow Candle High"), tup.shadow.High),
                            (CustomIndicator.Get("Shadow Candle Middle"),tup.shadow.MiddlePoint()),
                            (CustomIndicator.Get("Shadow Candle Low"), tup.shadow.Low),
                            (CustomIndicator.Get("Shadow Strength"), tup.strength)
                        });
                });

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var symbolChart = new CreateMultiPaneStockChartsViewModel(instrument, priceChart,
                    new List<Dictionary<IIndicator, IObservable<(IIndicator, DateTime, double)>>>()
                    {
                        new Dictionary<IIndicator, IObservable<(IIndicator, DateTime, double)>>() { }
                    }, eventsFeed);


                InstrumentCharts.Add(new SymbolChartsViewModel(instrument, symbolChart, new StrategyInstrumentSummaryReport(Strategy, instrument)));
            }));
        }

        private Tuple<bool,bool> ComputeStrategy(IEnumerable<(Symbol instrument, Candle candle, ShadowCandle shadow, double val, double strength)> tups, bool buyS, bool sellS)
        {
            var quantity = 100000;
            var baseCurrency = ((CurrencyPair)tups.First().instrument).BaseCurrency;
            if (baseCurrency != Currency.GBP)
                quantity = (int)(ExchangeRateGetter.GetExchangeRate(CurrencyPair.Get(Currency.GBP, baseCurrency)) * 100000);


            Debug.Assert(tups.Select(t => t.shadow).Distinct().Count() == 1);

            var noTrade = Tuple.Create(false, false);
            var buyTrade = Tuple.Create(true, false);
            var sellTrade = Tuple.Create(false, true);

            var lastCandle = tups.Last().Item2;
            var recent = tups.Last();

            if (lastCandle.TimeStamp.Hour >= 17)
                return noTrade;
            
            if (Strategy.OpenedOrders.Any() ||
                Strategy.ClosedOrders.OrderBy(o => o.OrderInfo.TimeStamp).LastOrDefault()?.OrderInfo.TimeStamp >= lastCandle.TimeStamp)
            {
                return noTrade;
            }

            Status = $"Executing {recent.candle.TimeStamp} {recent.instrument}";

            var shouldBuy = tups.All(tup => tup.strength.IsInBetween(tup.shadow.MiddlePoint(), tup.shadow.High))
                            && Math.Abs(recent.strength - recent.candle.Close) >= 0.0010
                            && recent.candle.Close < recent.shadow.MiddlePoint();

            if (shouldBuy && buyS)
            {
                Debug.WriteLine($"BUY: Candle: {lastCandle.TimeStamp}, Shadow: {recent.shadow.ToCsv()}, Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
                Strategy.Open(new BuyOrder(new OrderInfo(recent.candle.TimeStamp, recent.instrument, Strategy,
                    recent.candle.Close.USD(), quantity)));
                return buyTrade;
            }


            var shouldSell = tups.All(tup => tup.strength.IsInBetween(tup.shadow.Low, tup.shadow.MiddlePoint()))
                             && Math.Abs(recent.strength - recent.candle.Close) >= 0.0010
                             && recent.candle.Close > recent.shadow.MiddlePoint();

            if (shouldSell && sellS)
            {
                Debug.WriteLine($"SELL: Candle: {lastCandle.TimeStamp}, Shadow: {recent.shadow.ToCsv()}, Thread: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
                Strategy.Open(new ShortSellOrder(new OrderInfo(recent.candle.TimeStamp, recent.instrument, Strategy,
                    recent.candle.Close.USD(), quantity)));
                return sellTrade;
            }

            return noTrade;
        }

        private void Log(IEnumerable<(Symbol instrument, Candle candle, ShadowCandle shadow, double val, double strength)> tups, Tuple<bool,bool> buyOrSell, IFileWriter fileWriter)
        {
            var trade = "NOTRADE";
            if (buyOrSell.Item1)
                trade = "BUY";
            if(buyOrSell.Item2)
                trade = "SHORTSELL";

            tups.Select(tup => $"{tup.candle.ToCsv()},{tup.shadow.ToCsv()},{tup.val},{tup.strength},{trade}")
                .Foreach(fileWriter.WriteWithTs);
        }

        private static (Symbol instrument, Candle candle, ShadowCandle shadow, double val, double strength)
            GetShadowStrengthFeed(Symbol instrument, Candle candle, ShadowCandle shadow)
        {
            //Debug.WriteLine($"Getting Shadow Strength - {candle.TimeStamp}");

            var tupStrength = Tuple.Create(
                (candle.High - shadow.Low).PercentageOf(shadow.CandleLength()),
                (shadow.High - candle.Low).PercentageOf(shadow.CandleLength()));

            var val = tupStrength.Item1 - tupStrength.Item2;

            var strength = shadow.Low + shadow.CandleLength() / 2 + (shadow.CandleLength() * val * 0.01);
            return (instrument, candle, shadow, val, strength);
        }

        private static ShadowCandle GetShadowCandle(ShadowCandle prevShadow, Candle candle)
        {
            //Debug.WriteLine($"Getting Shadow Candle - {candle.TimeStamp}");
            if (prevShadow == ShadowCandle.Null)
                return new ShadowCandle(new Ohlc(candle.Open, candle.High, candle.Low, candle.Close, candle.Ohlc.Volume),
                    candle.TimeStamp);

            return candle.Low > prevShadow.Low && candle.High < prevShadow.High
                ? prevShadow
                : new ShadowCandle(new Ohlc(candle.Open, candle.High, candle.Low, candle.Close, candle.Ohlc.Volume),
                    candle.TimeStamp);
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

        private StrategyEmailReport emailReport;

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


    public class ExchangeRateGetter
    {
        private static readonly Dictionary<CurrencyPair, double> ExchangeRate;

        static ExchangeRateGetter()
        {
            ExchangeRate = CurrencyPair.All()
                .ToDictionary(p => p, p => double.NaN);
        }

        public ExchangeRateGetter(IObservable<Tuple<CurrencyPair, Candle>> feed)
        {
            feed.Subscribe(f => ExchangeRate[f.Item1] = f.Item2.Close);
        }

        public static double GetExchangeRate(CurrencyPair pair)
            //=> 1;
            => ExchangeRate[pair];
    }
}
