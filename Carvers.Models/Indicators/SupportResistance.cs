using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;
using Carvers.Infra.Math.Geometry;
using Carvers.Models.Events;
using Carvers.Models.Extensions;

namespace Carvers.Models.Indicators
{
    public class DailyHighLowIndicator : IEvent
    {
        
    }

    public class SupportResistance
    {
        public SupportResistance(Line<DateTimeOffset, double> support, Line<DateTimeOffset, double> resistance)
        {
            Support = support;
            Resistance = resistance;
        }

        public static SupportResistance Calculate(Lookback lookback)
        {
            var topHighCandles = lookback.Candles.OrderByDescending(candle => candle.High)
                .Take(2)
                .OrderBy(c => c.TimeStamp);

            var h1 = topHighCandles.First();
            var h2 = topHighCandles.Last();
            var resistance = new Line<DateTimeOffset, double>(h1.TimeStamp, h1.High, h2.TimeStamp, h2.High);

            var bottomLowCandles = lookback.Candles.OrderBy(candle => candle.Low)
                .Take(2)
                .OrderBy(c => c.TimeStamp);

            var l1 = bottomLowCandles.First();
            var l2 = bottomLowCandles.Last();
            var support = new Line<DateTimeOffset, double>(l1.TimeStamp, l1.Low, l2.TimeStamp, l2.Low);

            return new SupportResistance(support, resistance);
        }

        public Line<DateTimeOffset, double> Support { get; }
        public Line<DateTimeOffset, double> Resistance { get; }

    }


}
