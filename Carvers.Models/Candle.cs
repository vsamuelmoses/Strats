using System;

namespace Carvers.Models
{
    public class Candle
    {
        protected Candle(Ohlc ohlc, DateTimeOffset timeStamp)
        {
            Ohlc = ohlc;
            TimeStamp = timeStamp;
        }

        public Candle(Ohlc ohlc, DateTimeOffset timeStamp, TimeSpan span)
        {
            Ohlc = ohlc;
            TimeStamp = timeStamp;
            Span = span;
        }

        public Ohlc Ohlc { get; }
        public DateTimeOffset TimeStamp { get; }
        public virtual TimeSpan Span { get; }

        public virtual string ToCsv()
        {
            return $"{TimeStamp},{Span},{Ohlc.ToCsv()}";
        }

        public override bool Equals(object obj)
        {
            var candle = obj as Candle;
            return candle != null && Equals(candle);
        }

        protected bool Equals(Candle other)
        {
            return Equals(Ohlc, other.Ohlc) 
                && TimeStamp.Equals(other.TimeStamp);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Ohlc?.GetHashCode() ?? 0)*397) ^ TimeStamp.GetHashCode();
            }
        }

        public double Open => Ohlc.Open.Value;
        public double High => Ohlc.High.Value;
        public double Low => Ohlc.Low.Value;
        public double Close => Ohlc.Close.Value;
        
        public static Candle Null => null;
    }
}
