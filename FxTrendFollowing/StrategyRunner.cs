using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Threading;
using Carvers.Infra.Extensions;
using Carvers.Infra.ViewModels;
using Carvers.Models;
using Carvers.TradingEngine;

namespace FxTrendFollowing
{

    public class CurrencyStrengthViewModel : ViewModel
    {
        private readonly IndicatorFeed<CurrencyStrengthIndicator> csIndicatorFeed;
        public ObservableCollection<CurrencyStrengthIndicator> Items { get; private set; }

        public CurrencyStrengthViewModel(IndicatorFeed<CurrencyStrengthIndicator> csIndicatorFeed, Dispatcher dispatcher)
        {
            Items = new ObservableCollection<CurrencyStrengthIndicator>();

            this.csIndicatorFeed = csIndicatorFeed;
            this.csIndicatorFeed
                .ObserveOn(dispatcher)
                .Subscribe(ind => {

                    Items.Insert(0, ind);
                    if (Items.Count > 5)
                        Items.RemoveAt(5);
                });
        }
    }

    public class StrategyRunner : ViewModel
    {
        private readonly Dispatcher dispatcher;
        private string status;
        private Carvers.Infra.ViewModels.Reporters selectedReporters;

        public CurrencyStrengthViewModel CurrencyStrengthViewModel
        {
            get => currencyStrengthViewModel;
            private set { currencyStrengthViewModel = value; OnPropertyChanged();}
        }

        public StrategyRunner(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            StrategyVariations =
                new List
                    <StrategyExecution<TrendFollowing, TrendFollowingOptions>>();

            reporters = new Dictionary<StrategyExecution<TrendFollowing, TrendFollowingOptions>, Carvers.Infra.ViewModels.Reporters>();

            // SR - 1.6 var options = new TrendFollowingOptions(lookbackPeriod: 6, groupByCount: 1, holdPeriod: TimeSpan.FromMinutes((5)));

            //var options = new TrendFollowingOptions(lookbackPeriod:3, groupByCount:1, holdPeriod:TimeSpan.FromHours(3));
            //StrategyVariations.Add(new StrategyExecution<TrendFollowing, TrendFollowingOptions>(options));

            //var options2 = new TrendFollowingOptions(lookbackPeriod: 3, groupByCount: 1, holdPeriod: TimeSpan.FromHours(4));
            //StrategyVariations.Add(new StrategyExecution<TrendFollowing, TrendFollowingOptions>(options2));

            //var options3 = new TrendFollowingOptions(lookbackPeriod: 4, groupByCount: 1, holdPeriod: TimeSpan.FromHours(3));
            //StrategyVariations.Add(new StrategyExecution<TrendFollowing, TrendFollowingOptions>(options3));

            //var options4 = new TrendFollowingOptions(lookbackPeriod: 4, groupByCount: 1, holdPeriod: TimeSpan.FromHours(4));
            //StrategyVariations.Add(new StrategyExecution<TrendFollowing, TrendFollowingOptions>(options4));

            //var options5 = new TrendFollowingOptions(lookbackPeriod: 2, groupByCount: 1, holdPeriod: TimeSpan.FromHours(2));
            //StrategyVariations.Add(new StrategyExecution<TrendFollowing, TrendFollowingOptions>(options5));

            //var options6 = new TrendFollowingOptions(lookbackPeriod: 2, groupByCount: 1, holdPeriod: TimeSpan.FromHours(1));
            //StrategyVariations.Add(new StrategyExecution<TrendFollowing, TrendFollowingOptions>(options6));

            //var options7 = new TrendFollowingOptions(lookbackPeriod: 2, groupByCount: 1, holdPeriod: TimeSpan.FromHours(3));
            //StrategyVariations.Add(new StrategyExecution<TrendFollowing, TrendFollowingOptions>(options7));

            var options8 = new TrendFollowingOptions(lookbackPeriod: TimeSpan.FromHours(3), groupByCount: 1, holdPeriod: TimeSpan.FromHours(2), candleFeedInterval:TimeSpan.FromMinutes(1), shouldCacheCandleFeed:false );
            StrategyVariations.Add(new StrategyExecution<TrendFollowing, TrendFollowingOptions>(options8));
        }

        public void Run()
        {
            var dispatcher = Dispatcher.CurrentDispatcher;

            //var all2017 = new EngineConfig(new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero),
            //    new DateTimeOffset(2017, 12, 31, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1));

            //Universe = new DirectoryUniverse(new DirectoryInfo("../../2017"), Utility.Fx1Min, Utility.Fx1MinFileNameToSymbolConverter);

            var engineConfig = new EngineConfig(new DateTimeOffset(2016, 01, 01, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2016, 01, 31, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1));

            Universe = new DirectoryUniverse(new DirectoryInfo("../../2016"), Utility.Fx1Min, Utility.Fx1MinFileNameToSymbolConverter);


            //var engineConfig = new EngineConfig(new DateTimeOffset(2015, 01, 01, 0, 0, 0, TimeSpan.Zero),
            //    new DateTimeOffset(2015, 12, 31, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1));

            //Universe = new DirectoryUniverse(new DirectoryInfo("../../2015"), Utility.Fx1Min, Utility.Fx1MinFileNameToSymbolConverter);

            //var engineConfig = new EngineConfig(new DateTimeOffset(2018, 01, 01, 0, 0, 0, TimeSpan.Zero),
            //    new DateTimeOffset(2018, 03, 01, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1));

            //Universe = new DirectoryUniverse(new DirectoryInfo("../../2018"), Utility.Fx1Min, Utility.Fx1MinFileNameToSymbolConverter);



            //var feb2018 = new EngineConfig(new DateTimeOffset(2018, 01, 01, 0, 0, 0, TimeSpan.Zero),
            //    new DateTimeOffset(2018, 02, 27, 23, 59, 0, TimeSpan.Zero), TimeSpan.FromHours(1));

            //Universe = new DirectoryUniverse(new DirectoryInfo("../../Data.bak"), Utility.Fx1Min, Utility.Fx1MinFileNameToSymbolConverter);
            Universe.Initialise
                .ContinueWith(task =>
                    Status = task.IsCompleted
                        ? $"Universe Loaded {Universe.Stocks.Count()}"
                        : "Error - Loading Universe")
                .ContinueWith(task => OnPropertyChanged(nameof(Universe)))
                .ContinueWith(_ =>
                {
                    var csIndicatorFeed = new IndicatorFeed<CurrencyStrengthIndicator>(CurrencyStrengthIndicatorStore.CurrencyStrengthFeedForHour(Universe.Stocks), TimeSpan.FromSeconds(1), TimeSpan.FromHours(1), engineConfig.Start);
                    CurrencyStrengthViewModel = new CurrencyStrengthViewModel(csIndicatorFeed, dispatcher);

                    foreach (var stratExecutor in StrategyVariations)
                    {
                        var strategy = new TrendFollowing(Universe, CurrencyStrengthFeed.CurrencyStrengthFeedForHour(Universe.Stocks), stratExecutor.Options);
                        var strategies = new List<IStrategy> {strategy};
                        stratExecutor.Strategies.Add(strategy);
                        var logReport = new StrategyLogReport(strategies, logName: $"TrendFollowing.{stratExecutor.Options.ToString()}");
                        var chartReport = new StrategyChartReport(strategies, dispatcher);
                        var summaryReport = new StrategySummaryReport(strategies);
                        var reports = new Carvers.Infra.ViewModels.Reporters(logReport, chartReport, summaryReport);
                        reporters.Add(stratExecutor, reports);
                        stratExecutor.Reporters = reports;
                    }

                    return StrategyVariations;
                })
                .ContinueWith(task =>
                {
                    var ticksStream = EventGenerator.Ticks(engineConfig);
                    ticksStream.Subscribe(date =>
                        {

                            foreach (var paras in task.Result)
                                Status = $"Executing Strategy {date}, {paras.ProfitLoss}";

                            task.Result.Foreach(stratExecution => stratExecution.Strategies.ForEach(strat => strat.Execute(date)));// Task.Factory.StartNew(() => strat.Execute(date))));
                            task.Result.Foreach(stratExecution => stratExecution.ComputePL(date));

                        },
                        exception =>
                        {
                            Status = "Error executing strategy";
                        }
                        ,
                        () =>
                        {

                            task.Result.Foreach(kvp => kvp.Strategies.Foreach(strat => strat.Stop()));
                        });
                    ticksStream.Connect();

                    //SelectedStrategy = StrategyVariations.First();
                });

        }

        public List<StrategyExecution<TrendFollowing, TrendFollowingOptions>> StrategyVariations { get; set; }
        private Dictionary<StrategyExecution<TrendFollowing, TrendFollowingOptions>, Carvers.Infra.ViewModels.Reporters> reporters;

        private StrategyExecution<TrendFollowing, TrendFollowingOptions> selectedStrategy;
        private CurrencyStrengthViewModel currencyStrengthViewModel;

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

        public StrategyExecution<TrendFollowing, TrendFollowingOptions> SelectedStrategy
        {
            get { return selectedStrategy; }
            set
            {
                selectedStrategy = value;
                OnPropertyChanged();
                SelectedReporters = reporters[selectedStrategy];
            }
        }

        public Carvers.Infra.ViewModels.Reporters SelectedReporters
        {
            get { return selectedReporters; }
            private set { selectedReporters = value; OnPropertyChanged(); }
        }
    }
}