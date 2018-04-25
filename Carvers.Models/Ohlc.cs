using Carvers.Models.Extensions;

namespace Carvers.Models
{
    public class Ohlc
    {
        public Ohlc(double open, double high, double low, double close, double volume)
            : this((Price) open.USD(), high.USD(), low.USD(), close.USD(), volume)
        {
        }

        public Ohlc(Price open, Price high, Price low, Price close, double volume)
        {
            Open = open;
            High = high;
            Low = low;
            Close = close;
            Volume = volume;
        }

        public Price Open { get; }
        public Price High { get; }
        public Price Low { get; }
        public Price Close { get; }
        public double Volume { get; }

        public string ToCsv()
        {
            return $"{Open},{High},{Low},{Close},{Volume}";
        }

        public override bool Equals(object obj)
        {
            var ohlc = obj as Ohlc;
            if (ohlc == null)
                return false;
            return Equals(ohlc);
        }

        protected bool Equals(Ohlc other)
        {
            return Open.Equals(other.Open) 
                && High.Equals(other.High) 
                && Low.Equals(other.Low) 
                && Close.Equals(other.Close) 
                && Volume.Equals(other.Volume);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Open.GetHashCode();
                hashCode = (hashCode*397) ^ High.GetHashCode();
                hashCode = (hashCode*397) ^ Low.GetHashCode();
                hashCode = (hashCode*397) ^ Close.GetHashCode();
                hashCode = (hashCode*397) ^ Volume.GetHashCode();
                return hashCode;
            }
        }
    }
}