using System.IO;
using Carvers.Models;
using Carvers.Models.Indicators;

namespace Carvers.Utilities
{
    public static class GlobalPaths
    {
        private const string Data = @"C:\Users\svemagiri\Documents\GitHub\Strats\Data\";
        private const string IbData = Data + "IBData\\";
        private const string HistoricalData = Data + "HistoricalData\\";

        public const string FxHistoricalData = HistoricalData + @"Fx\";
        public const string FxIbData = IbData + @"Fx\";

        public const string StrategyLogs = @"..\Logs\Strategies\";
        public static FileInfo StrategySummaryFile(Strategy strategy, Symbol symbol)
        {
            return new FileInfo(StrategyLogs + $"{strategy.StrategyName}\\{symbol}.txt");
        }

        public static FileInfo ShadowCandlesFor(Symbol symbol, string span, bool liveData)
        {
            string folder = HistoricalData;

            if (liveData)
                folder = IbData;

            return new FileInfo(folder + $@"{symbol}\{symbol}.Shadow.{span}.csv");
        }
    }
}