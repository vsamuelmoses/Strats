using System;

namespace Carvers.Models
{
    public class DailyCandle : Candle
    {
        public override TimeSpan Span => TimeSpan.FromDays(1);
        public DailyCandle(Ohlc ohlc, DateTimeOffset timeStamp) 
            : base(ohlc, timeStamp)
        {
        }

        public override string ToCsv()
        {
            return $"{TimeStamp:dd/MM/yyyy},{Ohlc.ToCsv()}";
        }

        public static DailyCandle NullDailyCandle => null;
    }


    public class MinuteCandle : Candle
    {
        public override TimeSpan Span => TimeSpan.FromMinutes(1);
        public MinuteCandle(Ohlc ohlc, DateTimeOffset timeStamp)
            : base(ohlc, timeStamp)
        {
        }

        public override string ToCsv()
        {
            return $"{TimeStamp.ToString()},{Ohlc.ToCsv()}";
        }

        public static MinuteCandle NullMinuteCandle => null;
    }

    public class HourCandle : Candle
    {
        public override TimeSpan Span => TimeSpan.FromHours(1);
        public HourCandle(Ohlc ohlc, DateTimeOffset timeStamp)
            : base(ohlc, timeStamp)
        {
        }

        public override string ToCsv()
        {
            return $"{TimeStamp.ToString()},{Ohlc.ToCsv()}";
        }

        public static HourCandle NullHourlyCandle => null;
    }
}