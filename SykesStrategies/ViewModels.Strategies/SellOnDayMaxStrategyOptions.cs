using Carvers.Models;
using System;

namespace SykesStrategies.ViewModels.Strategies
{
    public class SellOnDayMaxStrategyOptions : IStrategyOptions
    {
        public Range<Price> Range { get; }
        public int Lookback { get; }
        public Range<double> PercentageChange { get; }
        public string Description { get; }
        public TimeSpan SpanBetweenMax { get; }

        public SellOnDayMaxStrategyOptions(Range<Price> range, int lookback, Range<double> percentChange, TimeSpan spanBetweenMax, string description)
        {
            Range = range;
            Lookback = lookback;
            PercentageChange = percentChange;
            Description = description;
            SpanBetweenMax = spanBetweenMax;
        }


    }
}