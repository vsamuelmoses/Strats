namespace Carvers.Infra.ViewModels
{

    public class Reporters
    {
        public Reporters(StrategyLogReport logReport, StrategyChartReport chartReport, StrategySummaryReport summaryReport)
        {
            LogReport = logReport;
            ChartReport = chartReport;
            SummaryReport = summaryReport;

        }

        public StrategyLogReport LogReport { get; private set; }
        public StrategyChartReport ChartReport { get; private set; }
        public StrategySummaryReport SummaryReport { get; private set; }
    }
}