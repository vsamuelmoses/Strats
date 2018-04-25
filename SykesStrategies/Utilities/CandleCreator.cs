using System;
using System.Globalization;
using Carvers.Models;

namespace SykesStrategies.Utilities
{
    public static class CandleCreator
    {
        public static DailyCandle GoogleFormat(string[] values)
        {
            try
            {
                DateTimeOffset timestamp;
                if(!DateTimeOffset.TryParseExact(values[0], "d-MMM-yy", null, DateTimeStyles.AssumeUniversal, out timestamp))
                    return DailyCandle.NullDailyCandle;

                double open;
                if(!double.TryParse(values[1], out open))
                    return DailyCandle.NullDailyCandle;

                double high;
                if (!double.TryParse(values[2], out high))
                    return DailyCandle.NullDailyCandle;

                double low;
                if (!double.TryParse(values[3], out low))
                    return DailyCandle.NullDailyCandle;

                double close;
                if (!double.TryParse(values[4], out close))
                    return DailyCandle.NullDailyCandle;

                double volume;
                if (!double.TryParse(values[5], out volume))
                    return DailyCandle.NullDailyCandle;

                return new DailyCandle(new Ohlc(open, high, low, close, volume), timestamp);
            }
            catch (Exception)
            {
                return DailyCandle.NullDailyCandle;
            }
        }

        public static DailyCandle QuoteMediaFormat(string[] values)
        {
            try
            {
                // 02/08/2016,7.44,7.4678,7.3,7.32,310401,-0.127,-1.88%,6.6657,2284078.31,1087

                DateTimeOffset timestamp;
                if (!DateTimeOffset.TryParseExact(values[0], "yyyy-MM-dd", null, DateTimeStyles.AssumeUniversal, out timestamp))
                    return DailyCandle.NullDailyCandle;

                double open;
                if (!double.TryParse(values[1], out open))
                    return DailyCandle.NullDailyCandle;

                double high;
                if (!double.TryParse(values[2], out high))
                    return DailyCandle.NullDailyCandle;

                double low;
                if (!double.TryParse(values[3], out low))
                    return DailyCandle.NullDailyCandle;

                double close;
                if (!double.TryParse(values[4], out close))
                    return DailyCandle.NullDailyCandle;

                double volume;
                if (!double.TryParse(values[5], out volume))
                    return DailyCandle.NullDailyCandle;

                return new DailyCandle(new Ohlc(open, high, low, close, volume), timestamp);
            }
            catch(Exception)
            {
                return DailyCandle.NullDailyCandle;
            }
        }

        public static DailyCandle CSIFormat(string[] values)
        {
            try
            {
                DateTimeOffset timestamp;
                if (!DateTimeOffset.TryParseExact(values[0], "yyyyMMdd", null, DateTimeStyles.AssumeUniversal, out timestamp))
                    return DailyCandle.NullDailyCandle;

                double open;
                if (!double.TryParse(values[1], out open))
                    return DailyCandle.NullDailyCandle;

                double high;
                if (!double.TryParse(values[2], out high))
                    return DailyCandle.NullDailyCandle;

                double low;
                if (!double.TryParse(values[3], out low))
                    return DailyCandle.NullDailyCandle;

                double close;
                if (!double.TryParse(values[4], out close))
                    return DailyCandle.NullDailyCandle;

                double volume;
                if (!double.TryParse(values[5], out volume))
                    return DailyCandle.NullDailyCandle;

                return new DailyCandle(new Ohlc(open/100, high/100, low/100, close/100, volume*100), timestamp);
            }
            catch (Exception)
            {
                return DailyCandle.NullDailyCandle;
            }
        }


        public static  Tuple<Symbol, double> AverageClosePrices(string[] values)
        {
            return Tuple.Create(new Symbol(values[0]), double.Parse(values[1]));
        }
    }
}