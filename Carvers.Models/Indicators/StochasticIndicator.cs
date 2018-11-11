using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Carvers.Models.Indicators
{
    public class StochasticIndicator : IndicatorBase<(Candle, StochasticKValue), StochasticKValue>
    {
        public StochasticIndicator() 
            : base("Stochastic", tup => new StochasticKValue(tup.Item2.Length, tup.Item2.Candles.Add(tup.Item1)))
        {
        }
    }

    public class StochasticKValue
    {
        public int Length { get; }
        public double High { get; }
        public double Low { get; }
        public double K { get; }

        public ImmutableList<Candle> Candles { get; }

        public StochasticKValue(int length, ImmutableList<Candle> candles)
        {
            Length = length;
            Candles = candles;

            if (Candles.Count > Length)
            {
                Candles = Candles.RemoveAt(0);
            }

            Debug.Assert(Candles.Count <= Length );

            if (Candles.Count == Length)
            {
                High = Candles.Max(c => c.High);
                Low = Candles.Min(c => c.Low);

                K = Candles.Last().ToStochasticValue(High, Low);
            }
            else
            {
                High = double.NaN;
                Low = double.NaN;
                K = double.NaN;
            }
        }
    }
}
