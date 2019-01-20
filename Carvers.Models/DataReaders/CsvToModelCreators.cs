using System;
using System.Diagnostics;
using System.Globalization;
using Carvers.Models.Indicators;

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

        public static ShadowCandle CsvToCarversShadowCandle(string[] values)
        {
            try
            {
                var index = 0;
                var date = DateTimeOffset.Parse(values[index++]);

                if (!TimeSpan.TryParseExact(values[index++], "g", CultureInfo.CurrentCulture, out var time))
                    return ShadowCandle.Null;

                var timestamp = date;

                if (!double.TryParse(values[index++], out var open))
                    return ShadowCandle.Null;

                if (!double.TryParse(values[index++], out var high))
                    return ShadowCandle.Null;

                if (!double.TryParse(values[index++], out var low))
                    return ShadowCandle.Null;

                if (!double.TryParse(values[index++], out var close))
                    return ShadowCandle.Null;

                if (!double.TryParse(values[index++], out var volume))
                    return ShadowCandle.Null;

                
                return new ShadowCandle(new Ohlc(open, high, low, close, volume), timestamp);
            }
            catch (Exception)
            {
                return ShadowCandle.Null;
            }
        }


        public static DailyCandle CsvToCarversDailyCandle(string[] values)
        {
            try
            {
                var index = 0;
                var date = DateTimeOffset.Parse(values[index++]);

                var timestamp = date;

                if (!double.TryParse(values[index++], out var open))
                    return DailyCandle.Null;

                if (!double.TryParse(values[index++], out var high))
                    return DailyCandle.Null;

                if (!double.TryParse(values[index++], out var low))
                    return DailyCandle.Null;

                if (!double.TryParse(values[index++], out var close))
                    return DailyCandle.Null;

                if (!double.TryParse(values[index++], out var volume))
                    return DailyCandle.Null;


                return new DailyCandle(new Ohlc(open, high, low, close, volume), timestamp);
            }
            catch (Exception)
            {
                return DailyCandle.Null;
            }
        }
    }
}
