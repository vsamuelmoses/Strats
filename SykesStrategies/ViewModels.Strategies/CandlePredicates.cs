using System;
using System.Linq;
using Carvers.Infra.Extensions;
using Carvers.Models;

namespace SykesStrategies.ViewModels.Strategies
{
    public static class CandlePredicates
    {
        public static CandleRule CandleRule(this Predicate<Candle> predicate, string description)
            => new CandleRule(predicate, description);

        public static CandleRule CandleRule(this Func<Candle, Candle, bool> predicate, string description)
            => new CandleRule(predicate, description);

        public static CandleRule CandleRule(this Func<StockData, Candle, bool> predicate, string description)
            => new CandleRule(predicate, description);

        public static Predicate<Candle> IsGreaterThan<T>(Func<Candle, T> getter, T from)
            where T: IComparable
            => candle => getter(candle).CompareTo(from) > 0;

        public static Predicate<Candle> IsLessThan<T>(Func<Candle, T> getter, T from)
            where T : IComparable
            => candle => getter(candle).CompareTo(from) < 0;

        public static Predicate<Candle> IsEqualTo<T>(Func<Candle, T> getter, T from)
            where T : IComparable
            => candle => getter(candle).CompareTo(from) == 0;

        public static Predicate<Candle> IsInRange<T>(Func<Candle, T> getter, T from, T to)
            where T : IComparable
            => candle => IsGreaterThan(getter, from)(candle) && IsLessThan(getter, from)(candle);
        
        public static Func<Candle, Candle, bool> IsReferenceEqual()
            => ReferenceEquals;

        public static Func<Candle, Candle, double> PercentageChange(Func<Candle, double> valueGetter)
            =>(candle1, candle2) => ((valueGetter(candle2) - valueGetter(candle1))*100)/valueGetter(candle1);

        public static Func<Candle, Candle, bool> IsGreaterThan<T>(this Func<Candle, Candle, T> valueGetter, T val)
            where T : IComparable
            => (candle1, candle2) => valueGetter(candle1, candle2).CompareTo(val) > 0;

        public static Func<Candle, Candle, bool> IsLessThan<T>(this Func<Candle, Candle, T> valueGetter, T val)
            where T : IComparable
            => (candle1, candle2) => valueGetter(candle1, candle2).CompareTo(val) < 0;

        public static Func<StockData, Candle, bool> IsValueMaxSince<T>(Func<Candle, T> getter, TimeSpan span)
            where T : IComparable
        {
            return (stock, candle)
                =>
            {
                var to = candle.TimeStamp;
                var from = to.Subtract(span);
                return
                    ReferenceEquals(stock.Candles
                    .Where(c => c.Key >= from && c.Key <= to)
                    .Select(c => c.Value).Maximum(getter), candle);
            };
        }
    }
}