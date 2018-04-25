using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Carvers.Infra.Extensions;
using Carvers.Models;
using Carvers.Models.Extensions;
using Carvers.TradingEngine;
using SykesStrategies.Data;
using SykesStrategies.ViewModels.Strategies;
using System.Windows.Threading;
using Carvers.Infra;
using CSIDataProvider;
using System.Threading.Tasks;
using Carvers.Infra.ViewModels;

namespace SykesStrategies.ViewModels
{
    public class StrategyRunner : ViewModel
    {
        private string status;
        private Reporters selectedReporters;

        public StrategyRunner()
        {
            StrategyVariations =
                new List
                    <StrategyExecution<SellOnMax, SellOnDayMaxStrategyOptions>>();

            reporters = new Dictionary<StrategyExecution<SellOnMax, SellOnDayMaxStrategyOptions>, Reporters>();
            
            //var percentageRange = Enumerable.Range(0, 100).Where(i => i%5 == 0).Select(i => Range<double>.Create<double>(i, i+5));

            //var openingPriceRanges = new List<Range<Price>>();

            //for(double i=0; i<15; i++)
            //    openingPriceRanges.Add(new Range<Price>(i.USD(), (i +1).USD()));


            //percentageRange.Select(percentageChange =>
            //    openingPriceRanges.Select(openPriceRange => new SellOnDayMaxStrategyOptions(openPriceRange, 30, percentageChange)))
            //    .SelectMany(option =>
            //    {
            //        var sellOnDayMaxStrategyOptionses = option as SellOnDayMaxStrategyOptions[] ?? option.ToArray();
            //        return sellOnDayMaxStrategyOptionses;
            //    })
            //    .Foreach(option => StrategyVariations.Add(option, new StrategyExecution<SellOn30DayMax, SellOnDayMaxStrategyOptions>(option)));

            var options = new SellOnDayMaxStrategyOptions(Range<Price>.Create(5d.USD(), 10d.USD()), 4, Range<double>.Create(15d, 1500d), TimeSpan.FromDays(3), 
                "Price5to10.LB4.VChange15.SpanBwMax2");
            StrategyVariations.Add(new StrategyExecution<SellOnMax, SellOnDayMaxStrategyOptions>(options));

            var options2 = new SellOnDayMaxStrategyOptions(Range<Price>.Create(5d.USD(), 10d.USD()), 8, Range<double>.Create(15d, 1500d), TimeSpan.FromDays(3),
                "Price5to10.LB8.VChange15.SpanBwMax2");
            StrategyVariations.Add(new StrategyExecution<SellOnMax, SellOnDayMaxStrategyOptions>(options2));

            var options3 = new SellOnDayMaxStrategyOptions(Range<Price>.Create(5d.USD(), 10d.USD()), 30, Range<double>.Create(15d, 1500d), TimeSpan.FromDays(3),
                "Price5to10.LB30.VChange15.SpanBwMax2");
            StrategyVariations.Add(new StrategyExecution<SellOnMax, SellOnDayMaxStrategyOptions>(options3));


            //Enumerable.Range(5, 60)
            //    .Select(lookback => new SellOnDayMaxStrategyOptions(Range<Price>.Create(5d.USD(), 15d.USD()), lookback, Range<double>.Create(15d, 1500d)))
            //    .Foreach(option => StrategyVariations.Add(option, new StrategyExecution<SellOn30DayMax, SellOnDayMaxStrategyOptions>(option)));


        }
        public void Start()
        {
            var dispatcher = Dispatcher.CurrentDispatcher; ;

            var beforeElection = new EngineConfig(new DateTimeOffset(2016, 5, 30, 0, 0, 0, TimeSpan.Zero),
                        new DateTimeOffset(2016, 10, 30, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromDays(1));

            var afterElection = new EngineConfig(new DateTimeOffset(2017, 01, 30, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromDays(1));

            var april = new EngineConfig(new DateTimeOffset(2017, 04, 01, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2017, 04, 30, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromDays(1));

            var may = new EngineConfig(new DateTimeOffset(2017, 05, 01, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2017, 05, 30, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromDays(1));

            var from2014 = new EngineConfig(new DateTimeOffset(2014, 07, 01, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2016, 10, 30, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromDays(1));

            var from2006 = new EngineConfig(new DateTimeOffset(2006, 07, 01, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2016, 10, 30, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromDays(1));

            var from2006Till2014 = new EngineConfig(new DateTimeOffset(2006, 07, 01, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2014, 10, 30, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromDays(1));

            //Universe = new DirectoryUniverse(new DirectoryInfo(Paths.CSIDir), CandleCreator.CSIFormat);
            //Universe = new DirectoryUniverse(new DirectoryInfo(Paths.NasdaqUniverse), CandleCreator.QuoteMediaFormat);
            //Universe = new SykesNasdaqUniverse();

            Universe = new SykesUniverse(
                CsvReader.ReadColumn<string>(CSIDataProvider.Paths.SykesUniverse.AsFileInfo(), 0).Select(val => val.AsSymbol()),
                can => can.TimeStamp.Year > 2006);

            //Universe = new SykesUniverse(
            //    new List<Symbol> { "DSNY".AsSymbol() },
            //    can => can.TimeStamp.Year > 2006);

            Universe.Initialise
                .ContinueWith(task => Status = task.IsCompleted ? $"Universe Loaded {Universe.Stocks.Count()}" : "Error - Loading Universe")
                .ContinueWith(task => OnPropertyChanged(nameof(Universe)))
                .ContinueWith(_ =>
                {
                    foreach (var paras in StrategyVariations)
                    {
                        var strategies = Universe.Stocks.Select(stock => new SellOnMax(stock, paras.Options)).ToList();
                        paras.Strategies.AddRange(strategies);
                        var logReport = new StrategyLogReport(strategies.Cast<IStrategy>(), logName:paras.Options.Description);
                        var chartReport = new StrategyChartReport(strategies, dispatcher);
                        var summaryReport = new StrategySummaryReport(strategies);
                        reporters.Add(paras, new Reporters(logReport,chartReport, summaryReport));
                    }
                    return StrategyVariations;
                })
                .ContinueWith(task =>
                {
                    var ticksStream = EventGenerator.Ticks(from2006);
                    ticksStream.Subscribe(date => {

                        foreach (var paras in task.Result)
                            Status = $"Executing Strategy {date}, {paras.Options.Range}, {paras.ProfitLoss}";

                        task.Result.Foreach(stratExecution => stratExecution.Strategies.ForEach(strat => Task.Factory.StartNew(() => strat.Execute(date))));
                        task.Result.Foreach(stratExecution => stratExecution.ComputePL(date));

                    },
                        exception => {
                            Status = "Error executing strategy";
                        }
                        ,
                        () => {

                            task.Result.Foreach(kvp => kvp.Strategies.Foreach(strat => strat.Stop()));
                        });
                    ticksStream.Connect();
                });

        }

        public List<StrategyExecution<SellOnMax, SellOnDayMaxStrategyOptions>> StrategyVariations { get; set; }
        private Dictionary<StrategyExecution<SellOnMax, SellOnDayMaxStrategyOptions>,Reporters> reporters;

        private StrategyExecution<SellOnMax, SellOnDayMaxStrategyOptions> selectedStrategy;

        public string Status
        {
            get { return status; }
            set
            {
                status = value;
                OnPropertyChanged();
            }
        }

        public Price ProfitLoss { get; private set; }
        public Universe Universe { get; private set; }

        public StrategyExecution<SellOnMax, SellOnDayMaxStrategyOptions> SelectedStrategy
        {
            get { return selectedStrategy; }
            set
            {
                selectedStrategy = value;
                OnPropertyChanged();
                SelectedReporters = reporters[selectedStrategy];
            }
        }

        public Reporters SelectedReporters
        {
            get { return selectedReporters; }
            private set { selectedReporters = value; OnPropertyChanged(); }
        }
    }
}