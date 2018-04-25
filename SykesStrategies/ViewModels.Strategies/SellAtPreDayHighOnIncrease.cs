using System;
using System.Diagnostics;
using System.Linq;

namespace SykesStrategies.ViewModels.Strategies
{
    //public class SellAtPreDayHighOnIncrease : IStrategy
    //{
    //    private readonly double increment;

    //    private double? sellSignal;
    //    private double? open;

    //    public SellAtPreDayHighOnIncrease(StockData data, int increment)
    //    {
    //        this.increment = increment;
    //        Data = data;
    //    }

    //    public Side ExecuteStrategy(DateTimeOffset dateTimeOffset)
    //    {
    //        var thisCandle = Data.Candles.SingleOrDefault(candle => candle.TimeStamp == dateTimeOffset);
    //        if (thisCandle == null)
    //            return Side;

    //        if (sellSignal.HasValue)
    //        {
    //            if (thisCandle.Ohlc.High >= sellSignal.Value)
    //            {
    //                Side = Side.Sell;
    //                open = sellSignal.Value;
    //            }
    //        }


    //        if (Side == Side.Sell)
    //        {
    //            PL = open.Value - thisCandle.Ohlc.Close;
    //            Debug.WriteLine($"{dateTimeOffset} - {Data.Symbol} - {Side} - {PL}");

    //            sellSignal = null;
    //            Side = Side.NoPosition;
    //            return Side;
    //        }
    //        else
    //        {

    //            var previousDayCandle =
    //                Data.Candles.SingleOrDefault(
    //                    candle => candle.TimeStamp == dateTimeOffset.Subtract(TimeSpan.FromDays(1)).Date);

    //            if (previousDayCandle == null)
    //                return Side;

    //            if (thisCandle.Ohlc.Close >= previousDayCandle.Ohlc.Close + previousDayCandle.Ohlc.Close*(increment/100))
    //                sellSignal = thisCandle.Ohlc.High;

    //            return Side;
    //        }
    //    }

    //    public Candle OpenCandle { get; private set; }
    //    public Candle CloseCandle { get; private set; }
    //    public double PL { get; private set; }
    //    public Side Side { get; private set; }
    //    public StockData Data { get; }
    //    public void Stop()
    //    {
    //        throw new NotImplementedException();
    //    }
    //}
}