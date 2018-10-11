using System;
using System.Collections.Generic;
using System.Linq;
using Carvers.Infra.Math.Geometry;

namespace Carvers.Infra.Extensions
{
    public static class EnumerableExtensions
    {
        public static void Foreach<T>(this IEnumerable<T> items, Action<T> act)
        {
            foreach (var item in items)
                act(item);
        }

        public static IEnumerable<T> TakeLastInReverse<T>(this IEnumerable<T> source, int n)
        {
            return source.Reverse().Take(n);
        }

        public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int n)
        {
            return source.Reverse().Take(n).Reverse();
        }

        public static T Maximum<T, TKey>(this IEnumerable<T> items, Func<T, TKey> selector)
            where T : class
            where TKey : IComparable
        {
            return items.Aggregate((max, current) => selector(max).CompareTo(selector(current)) >= 0 ? max : current);
        }

        public static bool AnyConsecutiveItemsEqual(this IEnumerable<double> items)
        {
            var previousItem = double.NaN;
            foreach (var item in items)
            {
                if (Equals(previousItem, item))
                    return true;

                previousItem = item;
            }

            return false;
        }

        public static IEnumerable<IGrouping<int, T>> GroupBy<T>(this IEnumerable<T> source, int itemsPerGroup)
        {
            var enumerable = source.ToList();
            return enumerable
                .Zip(Enumerable.Range(0, enumerable.Count), (s, r) => new { Group = r / itemsPerGroup, Item = s })
                .GroupBy(i => i.Group, g => g.Item)
                .ToList();
        }

        public static Line<double, double> GetLine(this IEnumerable<double> values, int approximation)
        {
            var approximatedValues = values.TakeLast(approximation).ToList();
            return new Line<double, double>(1d, approximatedValues.First(), 2d, approximatedValues.Last());
        }
    }
}