using System;
using System.Collections.Generic;
using System.Windows.Threading;
using Carvers.Infra.Extensions;
using Carvers.Infra.ViewModels;
using Carvers.Models;
using Carvers.TradingEngine;

namespace HVLF
{
    public class StrategyRunner : ViewModel
    {
        private string status;
        private Reporters selectedReporters;

        public StrategyRunner()
        {
            StrategyVariations =
                new List
                    <StrategyExecution<BuyOnMorningDip, BuyOnMorningDipOptions>>();

            reporters = new Dictionary<StrategyExecution<BuyOnMorningDip, BuyOnMorningDipOptions>, Reporters>();

            var options = new BuyOnMorningDipOptions();
            StrategyVariations.Add(new StrategyExecution<BuyOnMorningDip, BuyOnMorningDipOptions>(options));
        }

        public void Run()
        {
            var dispatcher = Dispatcher.CurrentDispatcher;

            var all2015 = new EngineConfig(new DateTimeOffset(2017, 10, 10, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2017, 10, 16, 0, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1));

           
            foreach (var stratExecutor in StrategyVariations)
            {
                var strategy = new BuyOnMorningDip(dt => Utility.DailyUniverseGenerator(dt), stratExecutor.Options);
                var strategies = new List<IStrategy> {strategy};
                stratExecutor.Strategies.Add(strategy);
                var logReport = new StrategyLogReport(strategies,
                    logName: $"TrendFollowing.{stratExecutor.Options.ToString()}");
                var chartReport = new StrategyChartReport(strategies, dispatcher);
                var summaryReport = new StrategySummaryReport(strategies);
                var reports = new Reporters(logReport, chartReport, summaryReport);
                reporters.Add(stratExecutor, reports);
                stratExecutor.Reporters = reports;
            }

            var ticksStream = EventGenerator.Ticks(all2015);
            ticksStream.Subscribe(date =>
                {
                    foreach (var paras in StrategyVariations)
                        Status = $"Executing Strategy {date}, {paras.ProfitLoss}";

                    StrategyVariations.Foreach(stratExecution =>
                        stratExecution.Strategies.ForEach(strat =>
                            strat.Execute(date))); // Task.Factory.StartNew(() => strat.Execute(date))));
                    StrategyVariations.Foreach(stratExecution => stratExecution.ComputePL(date));

                },
                exception => { Status = "Error executing strategy"; }
                ,
                () => { StrategyVariations.Foreach(kvp => kvp.Strategies.Foreach(strat => strat.Stop())); });
            ticksStream.Connect();

        }

        public List<StrategyExecution<BuyOnMorningDip, BuyOnMorningDipOptions>> StrategyVariations { get; set; }
        private Dictionary<StrategyExecution<BuyOnMorningDip, BuyOnMorningDipOptions>, Reporters> reporters;

        private StrategyExecution<BuyOnMorningDip, BuyOnMorningDipOptions> selectedStrategy;

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

        public StrategyExecution<BuyOnMorningDip, BuyOnMorningDipOptions> SelectedStrategy
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
            private set
            {
                selectedReporters = value;
                OnPropertyChanged();
            }
        }


    }
}