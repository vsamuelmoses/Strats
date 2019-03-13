using System.IO;
using Carvers.Models;
using Carvers.Models.Indicators;

namespace Carvers.Utilities
{
    public static class GlobalPaths
    {
        private const string Data = @"C:\Users\svemagiri\Documents\GitHub\Strats\Data\";
        //private const string Data = @"C:\LiveStrats\Data\";

        private const string IbData = Data + "IBData\\";
        private const string HistoricalData = Data + "HistoricalData\\";

        public const string FxHistoricalData = HistoricalData + @"Fx\";
        public const string FxIbData = IbData + @"Fx\";

        public const string StrategyLogs = @"..\Logs\Strategies\";
        public static FileInfo StrategySummaryFile(Strategy strategy, Symbol symbol, string optionalAppend = null)
        {
            var append = optionalAppend == null ? string.Empty : optionalAppend; 
            return new FileInfo(StrategyLogs + $"{strategy.StrategyName}\\{symbol}.{optionalAppend}.txt");
        }

        public static FileInfo CandleFileFor(Symbol symbol, string span, bool liveData)
        {
            return liveData 
                ? new FileInfo(FxIbData + $@"{symbol}\DAT_IBData_{symbol}_{span}.csv") 
                : new FileInfo(FxHistoricalData + $@"{symbol}\DAT_MT_{symbol}_{span}.csv");
        }

        public static FileInfo ShadowCandlesFor(Symbol symbol, string span, bool liveData)
        {
            string folder = string.Empty;

            if (liveData)
            {
                folder = symbol is CurrencyPair ? FxIbData : IbData;
            }
            else
            {
                folder = symbol is CurrencyPair ? FxHistoricalData : HistoricalData;
            }


            
            return new FileInfo(folder + $@"{symbol}\{symbol}.Shadow.{span}.csv");
        }
    }
}