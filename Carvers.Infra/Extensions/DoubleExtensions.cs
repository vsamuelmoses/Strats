using System.Collections.Generic;
using System.Linq;

namespace Carvers.Infra.Extensions
{
    public static class DoubleExtensions
    {
        public static double PercentageOf(this double value, double denominator)
            => ((value*100)/denominator);

        public static double PercentageRise(this double value, double denominator)
            => ((value - denominator * 100) / denominator);

        public static bool IsInRange(this double value, double from, double to)
            => value >= from && value <= to;

        public static bool IsGreaterThan(this double value, double from)
            => value > from;

        public static bool IsInAscending(this IEnumerable<double> sequence)
        {
            var seq = sequence.ToList();
            return seq.SequenceEqual(seq.OrderBy(s => s)) && !seq.AnyConsecutiveItemsEqual();
        }

        public static bool IsInDescending(this IEnumerable<double> sequence)
        {
            var seq = sequence.ToList();
            return seq.SequenceEqual(seq.OrderByDescending(s => s)) && !seq.AnyConsecutiveItemsEqual();
        }
    }
}