using System.Collections.Generic;
using System.IO;
using System.Linq;
using Carvers.Infra;
using Carvers.Models;
using Carvers.Models.DataReaders;
using Carvers.Models.Indicators;

namespace ShadowStrengthStrategy.Models
{
    public class SssContext : IContext
    {
        public SssContext(Strategy strategy,
            FileWriter logFile,
            Symbol instrument,
            FileInfo shadowIndicatorFile,
            IEnumerable<IIndicatorFeed> indicators,
            Lookback lookbackCandles,
            IEnumerable<IContextInfo> contextInfos)
        {
            Strategy = strategy;
            LogFile = logFile;
            Instrument = instrument;
            ShadowIndicatorFile = shadowIndicatorFile;
            Indicators = indicators;
            LookbackCandles = lookbackCandles;
            ContextInfos = contextInfos;

            if (File.Exists(shadowIndicatorFile.FullName))
                ShadowCandles = CsvReader
                    .ReadFile(shadowIndicatorFile, CsvToModelCreators.CsvToCarversShadowCandle, skip: 0)
                    .ToList();

            LastCandle = LookbackCandles.LastCandle;
        }

        public IEnumerable<ShadowCandle> ShadowCandles { get; }

        public Candle LastCandle { get; }
        public Strategy Strategy { get; }
        public FileWriter LogFile { get; }
        public Symbol Instrument { get; }
        public FileInfo ShadowIndicatorFile { get; }
        public IEnumerable<IIndicatorFeed> Indicators { get; }
        public Lookback LookbackCandles { get; }
        public IEnumerable<IContextInfo> ContextInfos { get; }

        public bool IsReady() => true;
    }
}