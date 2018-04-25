using System;
using System.Collections.Generic;
using System.Linq;
using Carvers.Infra.Extensions;

namespace Carvers.Models.Extensions
{
    public static class StockDataExtensions
    {
        public static double AverageClosePrice(this StockData stock, int numberOfCandles)
        {
            return stock.Candles.TakeLastInReverse(numberOfCandles)
                .Average(candle => candle.Value.Ohlc.Close.Value);
        }

        public static double? DaysMovingAverage(this StockData stock, DateTimeOffset dateTime, int period)
        {
            var candles = stock.Candles.Where(candle => candle.Key <= dateTime);
            return candles.Count() > period
                ? candles.TakeLastInReverse(period).Average(candle => candle.Value.Ohlc.Close.Value) 
                : (double?)null;
        }

        public static Candle MaxClose(this IEnumerable<Candle> candles)
            => candles.Maximum(candle => candle.Ohlc.Close);

        public static Candle MaxClose(this StockData stock, DateTimeOffset dateTime, int period)
            => stock.Candles.Values.MaxClose(dateTime, period);
        
        public static Candle MaxClose(this IEnumerable<Candle> candles, DateTimeOffset dateTime, int period)
        {
            var fromDate = dateTime.Subtract(TimeSpan.FromDays(period));

            if (!candles.Any(candle => candle.TimeStamp <= fromDate))
                return Candle.Null;

            var cands = candles
                .Where(candle => candle.TimeStamp <= dateTime && candle.TimeStamp >= fromDate).ToList();

            return cands.MaxClose();
        }

        public static Price ChangeIn(this Candle candle1, Candle candle2, Func<Candle, Price> val)
        {
            if (candle2 == null)
                return null;

            return val(candle1) - val(candle2);
        }

        public static double ChangeIn(this Candle candle1, Candle candle2, Func<Candle, double> val)
        {
            return val(candle1) - val(candle2);
        }

        public static Candle CandleBefore(this StockData data, Candle candle)
        {
            var previousTimeStamp = candle.TimeStamp.Subtract(candle.Span);
            while (!data.Candles.ContainsKey(previousTimeStamp))
            {
                previousTimeStamp = previousTimeStamp.Subtract(candle.Span);
            }
            return data.Candles[previousTimeStamp];
        }

        public static Tuple<double, Func<Candle, double>> CloseVal(this Candle thisCandle)
        {
            return Tuple.Create<double, Func<Candle, double>>(thisCandle.Ohlc.Close.Value, candle => candle.Ohlc.Close.Value);
        }

        public static bool IsMaxIn<T>(this Candle candle, Func<Candle, T> getter, IEnumerable<Candle> candles)
            where T : IComparable
            => ReferenceEquals(candles.Maximum(getter), candle);

        public static IEnumerable<Candle> Between(this IEnumerable<Candle> candles, DateTimeOffset from, DateTimeOffset to)
            => candles.Where(cand => cand.TimeStamp >= from && cand.TimeStamp <= to);

        public static double PercentageChangeIn(this Candle candle, Func<Candle, double> getter, Candle candle2)
            => ((getter(candle) - getter(candle2))*100)/getter(candle2);


        public static double PercentageChangeFromPrevious(this Candle candle, Func<Candle, double> getter,
            IEnumerable<Candle> candles)
        {
            var candlesList = candles.ToList();
            return candle.PercentageChangeIn(getter, candlesList.Single(can => can.TimeStamp == candle.TimeStamp.Subtract(TimeSpan.FromDays(1))));
        } 
    }
}