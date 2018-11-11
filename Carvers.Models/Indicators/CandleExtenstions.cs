namespace Carvers.Models.Indicators
{
    public static class CandleExtenstions
    {
        public static double ToStochasticValue(this Candle candle, double highValue, double lowValue)
        {
            var diff = highValue - lowValue;

            if (diff == 0)
                return 0d;

            return 100 * (candle.Close - lowValue) / diff;
        }
    }
}
