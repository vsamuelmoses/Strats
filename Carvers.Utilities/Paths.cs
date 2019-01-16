using System.IO;
using Carvers.Models;
using Carvers.Models.Indicators;

namespace Carvers.Utilities
{
    public static class Paths
    {
        public const string IBData = Data + "IBData\\";
        //public const string Data = @"C:\Users\Senior\Documents\Strats\FxTrendFollowing";
        public const string HistoricalData = @"..\..\";
        public const string Data = @"..\..\Data\";
        public const string FxStrenghts = Data + "FxStrength.Data";
        public const string FxStrenghtsAll = Data + @"FxStrength.Data\All.csv";
        public const string StrategyLogs = Data + "Strategies\\";
        public static FileInfo StrategySummaryFile(Strategy strategy, Symbol symbol)
        {
            return new FileInfo(StrategyLogs + $"{strategy.StrategyName}\\{symbol}.txt");
        }

        public static FileInfo IBDataCandlesFor(Symbol instrument, string span)
        {
            return new FileInfo(IBData + $@"{instrument}.{span}.csv");
        }

        public static FileInfo ShadowCandlesFor(Symbol instrument, string span)
        {
            return new FileInfo(IBData + $@"ShadowCandles\{instrument}.Shadow.{span}.csv");
        }
    }
}