using System.Collections.Generic;
using Carvers.Models;
using Carvers.Models.DataReaders;
using Carvers.Models.Indicators;

namespace ShadowStrengthStrategy.Models
{
    public class SssContext : IContext
    {
        public SssContext(Carvers.Models.Indicators.Strategy strategy,
            FileWriter logFile,
            Symbol instrument,
            IEnumerable<IIndicatorFeed> indicators,
            Lookback lookbackCandles,
            IEnumerable<IContextInfo> contextInfos)
        {
            Strategy = strategy;
            LogFile = logFile;
            Instrument = instrument;
            Indicators = indicators;
            LookbackCandles = lookbackCandles;
            ContextInfos = contextInfos;

            LastCandle = LookbackCandles.LastCandle;
        }

        public Candle LastCandle { get; }
        public Strategy Strategy { get; }
        public FileWriter LogFile { get; }
        public Symbol Instrument { get; }
        public IEnumerable<IIndicatorFeed> Indicators { get; }
        public Lookback LookbackCandles { get; }
        public IEnumerable<IContextInfo> ContextInfos { get; }

        public bool IsReady() => true;
    }
}