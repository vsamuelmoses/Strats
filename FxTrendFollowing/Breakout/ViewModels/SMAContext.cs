using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Carvers.Models;
using Carvers.Models.Indicators;

namespace FxTrendFollowing.Breakout.ViewModels
{
    public class SMAContext : IContext
    {
        public SMAContext(Strategy strategy,
            IEnumerable<IIndicator> indicators,
            Candle candle)
            : this(strategy, indicators, candle, Enumerable.Empty<IContextInfo>())
        {
        }

        public SMAContext(Strategy strategy,
            IEnumerable<IIndicator> indicators,
            Candle candle,
            IEnumerable<IContextInfo> infos)
        {
            Strategy = strategy;
            Indicators = indicators.ToDictionary(ma => ma.Description, ma => ma);
            LastCandle = candle;
            Infos = infos;
        }

        public Candle LastCandle { get; private set; }
        public IEnumerable<IContextInfo> Infos { get; }

        public bool IsReady()
            => Indicators.All(i => i.Value.HasValidValue);
        public Strategy Strategy { get; }
        public Dictionary<string, IIndicator> Indicators { get; }
    }

    public class SMAContextStreamingService
    {
        private Subject<SMAContext> _stream;
        public SMAContextStreamingService(
            SMAContext initialContext,
            IObservable<Candle> candleStream, 
            IObservable<MovingAverage> maStream)
        {
            CurrentContext = initialContext;

            Stream = candleStream
                .Zip(maStream, Tuple.Create)
                .Select(t => {
                    CurrentContext = new SMAContext(CurrentContext.Strategy, new List<MovingAverage>() {t.Item2}, t.Item1);
                    return CurrentContext;
                });
        }
        public SMAContext CurrentContext { get; private set; }
        public IObservable<SMAContext> Stream { get; }
    }
}