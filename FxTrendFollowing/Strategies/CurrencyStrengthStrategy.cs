using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
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

namespace FxTrendFollowing.Strategies
{
    public class CurrencyStrengthStrategy : ViewModel
    {
        private Stopwatch stopwatch;

        private string _status;
        private TraderViewModel _chartVm;
        private readonly Dictionary<Symbol, IObservable<Tuple<Symbol, Candle, ShadowCandle>>> symbolFeeds =
            new Dictionary<Symbol, IObservable<Tuple<Symbol, Candle, ShadowCandle>>>();

        private readonly Dictionary<Symbol, IObservable<Tuple<Symbol, Candle, ShadowCandle, double, double>>> symbolFeedsAgg =
            new Dictionary<Symbol, IObservable<Tuple<Symbol, Candle, ShadowCandle, double, double>>>();
        public ObservableCollection<SymbolChartsViewModel> InstrumentCharts { get; private set; }
        public CurrencyStrengthStrategy(IEnumerable<Symbol> instruments)
        {
            stopwatch = new Stopwatch();
            Instruments = instruments;

            //Ibtws = new IBTWSSimulator(Utility.SymbolFilePathIbDataGetter,
            //    new DateTimeOffset(2019, 02, 01, 0, 0, 0, TimeSpan.Zero),
            //    new DateTimeOffset(2019, 02, 19, 0, 0, 0, TimeSpan.Zero));

            //var barspan = TimeSpan.FromSeconds(5);


            Ibtws = new IBTWSSimulator(Utility.SymbolFilePathGetter,
                new DateTimeOffset(2016, 01, 01, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2016, 12, 28, 0, 0, 0, TimeSpan.Zero));

            var barspan = TimeSpan.FromMinutes(1);

            //Ibtws = new IBTWS();
            //var barspan = TimeSpan.FromSeconds(5);

            IbtwsViewModel = new IBTWSViewModel(Ibtws);

            Strategy = new Strategy("CandleStick Pattern");

            InstrumentCharts = new ObservableCollection<SymbolChartsViewModel>();
            var cashBalanceGetter = new CashBalanceSummary(Ibtws.CashBalanceStream);

            Strategy.OpenOrders
               .Merge(Strategy.CloseddOrders)
               .Subscribe(order =>
               {
                   string side = "SELL";
                   if (order is BuyOrder || order is BuyToCoverOrder)
                       side = "BUY";

                   
                   if (Ibtws is IBTWS)
                   {
                       Ibtws.PlaceOrder(ContractCreator.GetContract(order.OrderInfo.Symbol), OrderCreator.GetOrder(Ibtws.NextOrderId, side, order.OrderInfo.Size.ToString(), "MKT", "", "DAY"));
                   }
               });



            var feed = Ibtws.RealTimeBarStream
                .Select(msg => Tuple.Create(msg.ToCurrencyPair(), msg.ToCandle(barspan)))
                .Publish()
                .RefCount();

            var exchangeRateGetter = new ExchangeRateGetter(feed);
            
            var instrumentToOriginalFeed = Instruments
                .Select(instrument => Tuple.Create(instrument,
                        feed
                            .Where(f => f.Item1 == instrument)
                            .Select(f => f.Item2)
                            .Publish()
                            .RefCount()))
                .ToDictionary(tup => tup.Item1, tup => tup.Item2);

            instrumentToOriginalFeed.Foreach(kvp => {

                if (Ibtws is IBTWS)
                {
                    var instrument = kvp.Key;
                    var originalFeed = kvp.Value;
                    var file = new FileWriter(GlobalPaths.CandleFileFor(instrument, "5S", Ibtws is IBTWS).FullName,
                        12);
                    originalFeed.Subscribe(c => file.WriteWithTs(c.ToCsv()));
                }
            });

            var instrumentToHourlyStream = instrumentToOriginalFeed.ToDictionary(kvp => (CurrencyPair)kvp.Key, kvp => kvp.Value.ToHourlyStream());
            currencyStrength = instrumentToHourlyStream.ToCurrencyPairStrength();

            var instrumentToHourlyScanStream = instrumentToOriginalFeed.ToDictionary(kvp => (CurrencyPair)kvp.Key, kvp => kvp.Value.ToHourlyScanStream());
            var currencyStrengthScanStream = instrumentToHourlyScanStream.ToCurrencyPairStrengthWithCandle();

            instrumentToHourlyScanStream.Foreach(kvp =>
            {
                var instrument = kvp.Key;
                ExecuteCloseStrategy(currencyStrengthScanStream, barspan, instrument);
            });

            currencyStrengthScanStream
                .Buffer(10, 10)
                .Subscribe(css =>
                {
                    var strengths = css.Select(cs => cs.Timestamp + "," + string.Join(",",
                                                         cs.RankedStrengths.OrderByDescending(kvp => kvp.Key.Symbol)
                                                             .Select(t => t.Value)));
                    var filePath = Path.Combine(Paths.Reports, "Strength.2.Min.csv");

                    Console.WriteLine(string.Join(",", css.First().Strength.Select(c => c.Key).OrderByDescending(s => s.Symbol)));

                    if (!File.Exists(filePath))
                        File.Create(filePath);

                    File.AppendAllLines(filePath, strengths);

                });

            //currencyStrength
            //    .Buffer(100,100)
            //    .Subscribe(css =>
            //    {
            //        var strengths = css.Select(cs => cs.Timestamp + "," + string.Join(",",
            //                                             cs.RankedStrengths.OrderByDescending(kvp => kvp.Key.Symbol)
            //                                                 .Select(t => t.Value)));
            //        var filePath = Path.Combine(Paths.Reports, "Strength.csv");

            //        Console.WriteLine(string.Join(",", css.First().Strength.Select(c => c.Key).OrderByDescending(s => s.Symbol)));

            //        if (!File.Exists(filePath))
            //            File.Create(filePath);

            //        File.AppendAllLines(filePath, strengths);
            //    });


            //currencyStrength
            //    .CombineLatest()

            instrumentToHourlyStream.Foreach(kvp =>
            {
                var instrument = kvp.Key;
                ExecuteStrategy(kvp.Value, currencyStrength, barspan, instrument);
            });

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

            if ((Ibtws is IBTWS))
                emailReport = new StrategyEmailReport(new[] { Strategy });

            Reporters = new Carvers.Infra.ViewModels.Reporters(logReport, chartReport, summaryReport);
        }

        private void ExecuteCloseStrategy(IObservable<CurrencyStrength> currencyStrengthScanStream, TimeSpan barspan, CurrencyPair instrument)
        {
            currencyStrengthScanStream
                .Subscribe(tup =>
                {
                    var strength = tup;
                    var openOrder = Strategy.OpenedOrders.SingleOrDefault(order => order.OrderInfo.Symbol == instrument);
                    if (openOrder != null)
                    {
                        var diff = strength.Strength[instrument.BaseCurrency] - strength.Strength[instrument.TargetCurrency];
                        var c = strength.Candles[instrument];
                        
                        if ((openOrder is BuyOrder && diff < -50d)
                            || (openOrder is ShortSellOrder && diff > 50d)
                            || openOrder.OrderInfo.TimeStamp.Hour != c.TimeStamp.Hour)
                        {
                            var closeOrder = openOrder.GetCloseOrder(Strategy, double.MaxValue, double.MaxValue,
                                c.Close, c,
                                ExchangeRateGetter.GetExchangeRate);

                            Strategy.Close(closeOrder.Item2);
                        }
                    }
                });
        }

        private void ExecuteStrategy(IObservable<Candle> hourlyStream, IObservable<CurrencyStrength> strengthFeed, TimeSpan barspan, CurrencyPair instrument)
        {
            if (instrument.BaseCurrency == Currency.GBP || instrument.TargetCurrency == Currency.GBP)
                return;
            //hourlyStream
            //    .Subscribe(c =>
            //    {
            //        var openOrder = Strategy.OpenedOrders.SingleOrDefault(order => order.OrderInfo.Symbol == instrument);
            //        if (openOrder != null)
            //        {

            //            var entryCandle = openOrder.OrderInfo.Candle;
            //            var openInfo = openOrder.OrderInfo;

            //            var slPips = (entryCandle.CandleLength() * 0.5);
            //            var sl = ((CurrencyPair) openInfo.Symbol).ProfitLoss(openInfo.Size, slPips,
            //                ExchangeRateGetter.GetExchangeRate);

                        
            //            var closeOrder = openOrder.GetCloseOrder(Strategy, double.MaxValue, slPips, c.Close, c,
            //                ExchangeRateGetter.GetExchangeRate);

            //            Strategy.Close(closeOrder.Item2);

            //        }
            //    });



            hourlyStream
                .Zip(strengthFeed, Tuple.Create)
                .Subscribe(tup =>
                {
                    Debug.Assert(tup.Item1.TimeStamp == tup.Item2.Timestamp);

                    if (Strategy.OpenOrder != null)
                        return;

                    Status = $"Executing {tup.Item1.TimeStamp} {instrument}";

                    var strength = tup.Item2;
                    var strengths = strength.Strength.ToList().OrderByDescending(kvp => kvp.Value);
                    var targetCurrency = strengths.First().Key;
                    var baseCurrency = strengths.Last().Key;

                    var pair = CurrencyPair.Contains(targetCurrency, baseCurrency)
                        ? CurrencyPair.Get(targetCurrency, baseCurrency)
                        : CurrencyPair.Get(baseCurrency, targetCurrency);

                    if (pair != instrument)
                        return;

                    var quantity = 200000;
                    var bc = instrument.BaseCurrency;
                    if (bc != Currency.GBP)
                        quantity = (int)(ExchangeRateGetter.GetExchangeRate(CurrencyPair.Get(Currency.GBP, bc)) *
                                          quantity);

                    if (pair.TargetCurrency != targetCurrency)
                    {
                        var candle = tup.Item1;
                        Strategy.Open(new BuyOrder(new OrderInfo(candle.TimeStamp, instrument, Strategy,
                            candle.Close.USD(), quantity, candle)));
                    }

                    if (pair.TargetCurrency == targetCurrency)
                    {
                        var candle = tup.Item1;
                        Strategy.Open(new ShortSellOrder(new OrderInfo(candle.TimeStamp, instrument, Strategy,
                            candle.Close.USD(), quantity, candle)));
                    }
                });

            var eventsFeed = Strategy.OpenOrders
                .Where(order => order.OrderInfo.Symbol == instrument)
                .Select(order => (IEvent)new OrderExecutedEvent(order.OrderInfo.TimeStamp, order))
                .Merge(Strategy.CloseddOrders
                    .Where(order => order.OrderInfo.Symbol == instrument)
                    .Select(order => (IEvent)new OrderExecutedEvent(order.OrderInfo.TimeStamp, order)));

            var priceChart = hourlyStream
                .Select(candle => (candle,
                    (IEnumerable<(IIndicator, double)>)new List<(IIndicator, double)>() { }));

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
        private IObservable<CurrencyStrength> currencyStrength;

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

    public static class CurrencyPairStrength
    {
        public static IObservable<CurrencyStrength> ToCurrencyPairStrength(this Dictionary<CurrencyPair, IObservable<Candle>> feed)
        {
            return Observable.Create<CurrencyStrength>(o => {

                var currencyPairToStrengthFeed = feed
                    .Select(kvp =>
                        kvp.Value.Select(candle =>
                        {
                            var pairStrength = candle.ToCurrencyStrength();
                            return (kvp.Key, pairStrength.Item1, pairStrength.Item2, pairStrength.Item3);
                        }));

                currencyPairToStrengthFeed
                    .Merge()
                    .GroupBy(tup => tup.Item2)
                    .Subscribe(g => g.Buffer(21)
                        .Subscribe(gr => o.OnNext(new CurrencyStrength(g.Key, gr))));

                return () => { };
            });
        }


        public static IObservable<CurrencyStrength> ToCurrencyPairStrengthWithCandle(this Dictionary<CurrencyPair, IObservable<Candle>> feed)
        {
            return Observable.Create<CurrencyStrength>(o => {

                var currencyPairToStrengthFeed = feed
                    .Select(kvp =>
                        kvp.Value.Select(candle =>
                        {
                            var pairStrength = candle.ToCurrencyStrength();
                            return (kvp.Key, pairStrength.Item1, pairStrength.Item2, pairStrength.Item3, candle);
                        }));

                currencyPairToStrengthFeed
                    .Merge()
                    .GroupBy(tup => tup.Item2)
                    .Subscribe(g => g.Buffer(21)
                        .Subscribe(gr => o.OnNext(new CurrencyStrength(g.Key, gr))));

                return () => { };
            });
        }
    }

    public class CurrencyStrength
    {
        private readonly IEnumerable<(CurrencyPair, DateTimeOffset, double, double)> strength;
        public Dictionary<Currency, double> Strength { get; }
        public DateTimeOffset Timestamp { get; }

        public Dictionary<CurrencyPair, Candle> Candles { get; }
        public CurrencyStrength(DateTimeOffset timestamp,
            IEnumerable<(CurrencyPair, DateTimeOffset, double, double, Candle)> strength)
        
        {
            Timestamp = timestamp;


            Debug.Assert(strength.Count() == 21);

            Strength = strength
                .Select(tup => (tup.Item1.TargetCurrency, tup.Item3))
                .Concat(strength.Select(tup => (tup.Item1.BaseCurrency, tup.Item4)))
                .GroupBy(tup => tup.Item1)
                .Select(gr => (gr.Key, gr.Average(g => g.Item2)))
                .ToDictionary(t => t.Key, t => t.Item2);

            Debug.Assert(Strength.Count() == 7);

            RankedStrengths = Strength.ToList().OrderByDescending(kvp => kvp.Value).ToList();
            Candles = strength.ToDictionary(s => s.Item1, s => s.Item5);
        }

        public CurrencyStrength(DateTimeOffset timestamp, IEnumerable<(CurrencyPair, DateTimeOffset, double, double)> strength)
        {
            this.strength = strength;
            Timestamp = timestamp;


            Debug.Assert(strength.Count() == 21);

            Strength = strength
                .Select(tup => (tup.Item1.TargetCurrency, tup.Item3))
                .Concat(strength.Select(tup => (tup.Item1.BaseCurrency, tup.Item4)))
                .GroupBy(tup => tup.Item1)
                .Select(gr => (gr.Key, gr.Average(g => g.Item2)))
                .ToDictionary(t => t.Key, t => t.Item2);

            Debug.Assert(Strength.Count() == 7);

            RankedStrengths = Strength.ToList().OrderByDescending(kvp => kvp.Value).ToList();

        }

        public IList<KeyValuePair<Currency, double>> RankedStrengths { get; set; }

        public static Dictionary<Currency, double> StrengthExcluding(CurrencyStrength currencyStrength, CurrencyPair pair)
        {
            return currencyStrength.strength
                .Where(t => t.Item1 != pair)
                .Select(tup => (tup.Item1.TargetCurrency, tup.Item3))
                .Concat(currencyStrength
                    .strength
                    .Where(t => t.Item1 != pair)
                    .Select(tup => (tup.Item1.BaseCurrency, tup.Item4)))
                .GroupBy(tup => tup.Item1)
                .Select(gr => (gr.Key, gr.Average(g => g.Item2)))
                .ToDictionary(t => t.Key, t => t.Item2);
        }
    }

    public class CashBalanceSummary
    {
        private CashBalanceSummary instance;

        public CashBalanceSummary(IObservable<CashBalance> stream)
        {
            CashBalances = Currency.Currencies
                .Select(c => new CashBalance(c, 0d))
                .ToDictionary(cb => cb.Currency, cb => cb);

            stream
                .Subscribe(cb => CashBalances[cb.Currency] = cb);

            CashBalances = new Dictionary<Currency, CashBalance>();
        }
        public Dictionary<Currency, CashBalance> CashBalances { get; }
    }
}
