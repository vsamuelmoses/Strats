using System;
using System.Globalization;
using System.Linq;
using Carvers.Models;
using Carvers.Utilities;
using IBSampleApp.messages;

namespace Carvers.IBApi.Extensions
{
    public static class MessageExtensions
    {
        public static Candle ToCandle(this RealTimeBarMessage msg, TimeSpan span)
        {
            var est = msg.Timestamp.UnixEpochToUtc().ToEst();
            return new Candle(new Ohlc(msg.Open, msg.High, msg.Low, msg.Close, msg.Volume), est, span);
        }

        public static Candle ToDailyCandle(this HistoricalDataMessage msg)
        {
            DateTime dt;
            if (DateTime.TryParseExact(msg.Date, "yyyyMMdd", null, DateTimeStyles.None, out dt))
                return new DailyCandle(new Ohlc(msg.Open, msg.High, msg.Low, msg.Close, msg.Volume), dt);

            throw new Exception("Unexpected error parsing datetime");
        }

        public static DateTime UnixEpochToUtc(this long epoch)
        {
            var start = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            
            return start.AddMilliseconds(epoch * 1000);
        }

        public static CurrencyPair ToCurrencyPair(this RealTimeBarMessage msg)
            => CurrencyPair.All().Single(msg.IsForCurrencyPair);

        public static bool IsForCurrencyPair(this RealTimeBarMessage msg, Symbol pair)
            => pair.UniqueId == msg.RequestId - IBTWS.RT_BARS_ID_BASE;

        public static bool IsForCurrencyPair(this HistoricalDataMessage msg, Symbol pair)
            => pair.UniqueId == msg.RequestId - IBTWS.HISTORICAL_ID_BASE;

        public static int ToUniqueId(this RealTimeBarMessage msg)
            => msg.RequestId - IBTWS.RT_BARS_ID_BASE;
    }

    public static class IbApiUtility
    {
        static string[] dateTimeFormats = new string[] {
            "dd/M/yyyy HH:mm:ss zzz",
            "dd/MM/yyyy HH:mm:ss zzz",
            "yyyy.MM.dd",
            "dd.MM.yyyy HH:mm:ss.fff GMT-0000",
            "dd.MM.yyyy HH:mm:ss.fff GMT+0100"
        };

        public static RealTimeBarMessage ToRealTimeBarMessage(string[] values, int tickerId)
        {

            try
            {
                int index = 0;
                DateTimeOffset date;
                if (!DateTimeOffset.TryParseExact(values[index], dateTimeFormats, null, DateTimeStyles.AssumeUniversal, out date))
                    return null;

                index++;
                TimeSpan time;
                if (!TimeSpan.TryParseExact(values[index++], "g", CultureInfo.CurrentCulture, out time))
                    return null;

                var timestamp = date.Add(time);

                double open;
                if (!double.TryParse(values[index++], out open))
                    return null;

                double high;
                if (!double.TryParse(values[index++], out high))
                    return null;

                double low;
                if (!double.TryParse(values[index++], out low))
                    return null;

                double close;
                if (!double.TryParse(values[index++], out close))
                    return null;

                double volume;
                if (!double.TryParse(values[index++], out volume))
                    volume = 0;

                return new RealTimeBarMessage(IBTWS.RT_BARS_ID_BASE + tickerId, timestamp, timestamp.ToUnixTimeSeconds(), open, high, low, close, (long)(volume * 1000), 0, 0);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static RealTimeBarMessage FromIbHistoricalDataToRealTimeBarMessage(string[] values, int tickerId)
        {
            try
            {
                int index = 1;
                DateTimeOffset timestamp;
                if (!DateTimeOffset.TryParseExact(values[index++], dateTimeFormats, null, DateTimeStyles.AssumeUniversal, out timestamp))
                    return null;


                TimeSpan time;
                if (!TimeSpan.TryParseExact(values[index++], "g", CultureInfo.CurrentCulture, out time))
                    return null;

                double open;
                if (!double.TryParse(values[index++], out open))
                    return null;

                double high;
                if (!double.TryParse(values[index++], out high))
                    return null;

                double low;
                if (!double.TryParse(values[index++], out low))
                    return null;

                double close;
                if (!double.TryParse(values[index++], out close))
                    return null;

                double volume;
                if (!double.TryParse(values[index++], out volume))
                    volume = 0;

                return new RealTimeBarMessage(IBTWS.RT_BARS_ID_BASE + tickerId, timestamp.DateTime, timestamp.ToUnixTimeSeconds(), open, high, low, close, (long)(volume * 1000), 0, 0);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
