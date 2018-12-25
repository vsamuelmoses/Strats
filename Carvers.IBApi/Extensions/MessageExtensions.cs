using System;
using System.Globalization;
using Carvers.Models;
using IBSampleApp.messages;

namespace Carvers.IBApi.Extensions
{
    public static class MessageExtensions
    {
        public static Candle ToCandle(this RealTimeBarMessage msg, TimeSpan span)
        {
            var dt = msg.Timestamp.UnixEpochToLocalTime();
            return new Candle(new Ohlc(msg.Open, msg.High, msg.Low, msg.Close, msg.Volume), dt, span);
        }

        public static DateTime UnixEpochToLocalTime(this long epoch)
        {
            var start = new DateTime(1970, 1, 1, 0, 0, 0);
            return start.AddMilliseconds(epoch * 1000).ToLocalTime();
        }

        public static bool IsForCurrencyPair(this RealTimeBarMessage msg, CurrencyPair pair)
            => pair.UniqueId == msg.RequestId - IBTWS.RT_BARS_ID_BASE;
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

                return new RealTimeBarMessage(IBTWS.RT_BARS_ID_BASE + tickerId, timestamp.ToUnixTimeSeconds(), open, high, low, close, (long)(volume * 1000), 0, 0);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
