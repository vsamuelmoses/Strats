using System.Collections.Generic;
using System.Linq;
using Carvers.Infra.Extensions;
using Carvers.Models;
using Carvers.Models.Indicators;

namespace FxTrendFollowing.Breakout.ViewModels
{
    public class SMAContext : IContext
    {
        private readonly List<MovingAverage> smas;
        public SMAContext(Strategy strategy,
            MovingAverage sma50,
            MovingAverage sma100,
            MovingAverage sma250,
            MovingAverage sma500,
            MovingAverage sma1000,
            MovingAverage sma3600,
            ExponentialMovingAverage exma3600,
            ExponentialMovingAverage exma50,
            Lookback lookback,
            IContextInfo contextInfo)
        {
            Strategy = strategy;
            Sma50 = sma50;
            Sma100 = sma100;
            Sma250 = sma250;
            Sma500 = sma500;
            Sma1000 = sma1000;
            Sma3600 = sma3600;
            ExMa3600 = exma3600;
            ExMa50 = exma50;
            ContextInfo = contextInfo;
            Lookback = lookback;

            smas = new List<MovingAverage> { Sma50, Sma100, Sma250, Sma500, Sma1000, Sma3600 };
        }

        public IContext Add(Candle candle)
        {
            Lookback.Add(candle);
            LastCandle = candle;
            smas.Foreach(sma =>
            {
                sma.Push(candle.Close);

            });

            ExMa3600.Push(candle.Close);
            ExMa50.Push(candle.High);
            return this;
        }
        

        public Candle LastCandle { get; private set; }

        public bool IsReady()
            => smas.All(sma => !double.IsNaN(sma.Current));

        public Strategy Strategy { get; }
        public MovingAverage Sma50 { get; private set; }
        public MovingAverage Sma100 { get; private set; }
        public MovingAverage Sma250 { get; private set; }
        public MovingAverage Sma500 { get; }
        public MovingAverage Sma1000 { get; }
        public MovingAverage Sma3600 { get; private set; }
        public ExponentialMovingAverage ExMa3600 { get; private set; }
        public ExponentialMovingAverage ExMa50 { get; }
        public IContextInfo ContextInfo { get; }
        public Lookback Lookback { get; }
    }
}