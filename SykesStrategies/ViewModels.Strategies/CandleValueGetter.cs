using System;
using Carvers.Models;

namespace SykesStrategies.ViewModels.Strategies
{
    public class CandleValueGetter
    {
        public Candle Candle { get; }

        public CandleValueGetter(Func<Candle, double> getter, Candle candle)
        {
            Getter = getter;
            Candle = candle;
            Value = Getter(candle);
        }

        public Func<Candle, double> Getter { get; }

        public double Value { get; }
    }
}