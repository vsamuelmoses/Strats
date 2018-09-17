using System;
using Carvers.Models.Extensions;

namespace Carvers.Models
{
    public class Price : IComparable
    {
        public double Value { get; }
        public string Currency { get; }

        public Price(double value, string currency)
        {
            Value = value;
            Currency = currency;
        }

        public bool IsPositive()
        {
            return Value > 0;
        }

        public override string ToString()
        {
            return $"{Value}";
        }

        public static bool operator ==(Price x, Price y)
        {
            // If both are null, or both are same instance, return true.
            if (System.Object.ReferenceEquals(x, y))
            {
                return true;
            }

            // If one is null, but not both, return false.
            if (((object)x == null) || ((object)y == null))
            {
                return false;
            }

            // Return true if the fields match:
            return x.Value == y.Value && x.Currency == y.Currency;
        }

        public static bool operator !=(Price x, Price y)
        {
            return !(x == y);
        }

        public static Price operator *(Price x, Price y)
        {
            if (x.Currency != y.Currency)
                throw new Exception("Different currencies");

            return new Price(x.Value * y.Value, x.Currency);
        }

        public static Price operator *(Price x, int y)
            => new Price(x.Value * y, x.Currency);

        public static Price operator *(Price x, double y)
            => new Price(x.Value * y, x.Currency);


        public static Price operator /(Price x, int y)
            => new Price(x.Value / y, x.Currency);

        public static Price operator +(Price x, Price y)
        {
            if (x.Currency != y.Currency)
                throw new Exception("Different currencies");

            return new Price(x.Value + y.Value, x.Currency);
        }

        public static Price operator -(Price x, Price y)
        {
            if(x.Currency != y.Currency)
                throw new Exception("Different currencies");

            return new Price(x.Value - y.Value, x.Currency);
        }

        public static bool operator <(Price x, Price y)
        {
            if (x.Currency != y.Currency)
                throw new Exception("Different currencies");

            return x.Value < y.Value;
        }

        public static bool operator <=(Price x, Price y)
        {
            if (x.Currency != y.Currency)
                throw new Exception("Different currencies");

            return x.Value <= y.Value;
        }

        public static bool operator >=(Price x, Price y)
        {
            if (x.Currency != y.Currency)
                throw new Exception("Different currencies");

            return x.Value >= y.Value;
        }

        public static bool operator >(Price x, Price y)
        {
            if (x.Currency != y.Currency)
                throw new Exception("Different currencies");

            return x.Value > y.Value;
        }

        public int CompareTo(object obj)
        {
            var price = obj as Price;

            if(price == null)
                throw new Exception("Invalid Comparision");

            return this > price ? 1 : (this == price ? 0 : -1);
        }

        public static Price ZeroUSD { get; } = 0.0d.USD();
    }
}