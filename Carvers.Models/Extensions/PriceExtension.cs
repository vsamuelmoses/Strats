using System.Collections.Generic;
using System.Linq;

namespace Carvers.Models.Extensions
{
    public static class PriceExtension
    {
        private static readonly string usd = "$";
        public static Price USD(this double value) => new Price(value, usd);
        public static bool IsInRange(this double value, Range<double> range)
           => value >= range.From && value <= range.To;

        public static Price Total(this IEnumerable<Price> prices)
        {
            var initial = 0.0d.USD();
            return prices.Aggregate(initial, (sum, price2) => sum + price2);
        }

        public static bool IsInRange(this Price price, Price from, Price to)
            => price >= from && price <= to;

        public static bool IsInRange(this Price price, Range<Price> range)
            => price >= range.From && price <= range.To;
    }

}