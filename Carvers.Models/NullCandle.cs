using Carvers.Models.Indicators;

namespace Carvers.Models
{
    public class CandleSentiment
    {
        public static CandleSentiment Red = new CandleSentiment();
        public static CandleSentiment Green = new CandleSentiment();
        public static CandleSentiment Doji = new CandleSentiment();
        private CandleSentiment() { }

        public static CandleSentiment Of(Candle candle)
        {
            var diff = candle.Ohlc.Close - candle.Ohlc.Open;
            if (diff.Value > 0)
                return CandleSentiment.Green;
            if (diff.Value < 0)
                return CandleSentiment.Red;

            return CandleSentiment.Doji;
        }
    }

    public static class CandleSentimentExtensions
    {
        public static Side ToSide(this CandleSentiment sentiment)
        {
            if (sentiment == CandleSentiment.Red)
                return Side.ShortSell;

            if (sentiment == CandleSentiment.Green)
                return Side.Buy;

            throw new System.Exception("No sentiment to side conversion exist");

        }
    }
}