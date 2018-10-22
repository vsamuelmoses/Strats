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
            ExponentialMovingAverage exMa3600L,
            ExponentialMovingAverage exMa3600H,
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
            ExMa3600H = exMa3600H;
            ExMa3600L = exMa3600L;
            ContextInfo = contextInfo;

            smas = new List<MovingAverage> { Sma50, Sma100, Sma250, Sma500, Sma1000, Sma3600 };
        }

        public IContext Add(Candle candle)
        {
            LastCandle = candle;
            smas.Foreach(sma =>
            {
                sma.Push(candle.Close);

            });

            ExMa3600.Push(candle.Close);
            ExMa3600L.Push(candle.Low);
            ExMa3600H.Push(candle.High);
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
        public ExponentialMovingAverage ExMa3600H { get; }
        public ExponentialMovingAverage ExMa3600L { get; }
        public IContextInfo ContextInfo { get; }
    }
}