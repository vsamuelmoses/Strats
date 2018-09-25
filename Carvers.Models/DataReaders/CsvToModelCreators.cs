using System;
using System.Globalization;

namespace Carvers.Models.DataReaders
{
    public static class CsvToModelCreators
    {
        public static MinuteCandle CsvToFx1MinCandle(string[] values)
        {
            try
            {
                var index = 0;
                if (!DateTimeOffset.TryParseExact(values[index++], "yyyy.MM.dd", null, DateTimeStyles.AssumeUniversal,
                    out var date))
                    return MinuteCandle.NullMinuteCandle;

                if (!TimeSpan.TryParseExact(values[index++], "g", CultureInfo.CurrentCulture, out var time))
                    return MinuteCandle.NullMinuteCandle;

                var timestamp = date.Add(time);

                if (!double.TryParse(values[index++], out var open))
                    return MinuteCandle.NullMinuteCandle;

                if (!double.TryParse(values[index++], out var high))
                    return MinuteCandle.NullMinuteCandle;

                if (!double.TryParse(values[index++], out var low))
                    return MinuteCandle.NullMinuteCandle;

                if (!double.TryParse(values[index++], out var close))
                    return MinuteCandle.NullMinuteCandle;

                if (!double.TryParse(values[index++], out var volume))
                    return MinuteCandle.NullMinuteCandle;

                return new MinuteCandle(new Ohlc(open, high, low, close, volume), timestamp);
            }
            catch (Exception)
            {
                return MinuteCandle.NullMinuteCandle;
            }
        }
    }
}
