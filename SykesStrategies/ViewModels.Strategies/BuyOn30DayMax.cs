using System;
using System.Collections.Generic;
using Carvers.Models;

namespace SykesStrategies.ViewModels.Strategies
{
    

    //public class BuyOn30DayMax : IStrategy
    //{
    //    private Candle day30Max;
    //    public BuyOn30DayMax(StockData data)
    //    {
    //        Data = data;
    //    }

    //    public void Execute(DateTimeOffset dateTimeOffset)
    //    {
    //        var thisCandle = Data.Candles.SingleOrDefault(candle => candle.TimeStamp == dateTimeOffset);
    //        if (thisCandle == null)
    //            return;

    //        if (Side == Side.Buy)
    //        {
    //            CloseCandle = thisCandle;
    //            PL = CloseCandle.Ohlc.Close - OpenCandle.Ohlc.Close;
    //            Debug.WriteLine($"{dateTimeOffset} - {Data.Symbol} - {Side} - {PL}");

    //            Side = Side.NoPosition;
    //        }

    //        var temp = day30Max;

    //        day30Max = day30Max != Candle.Null
    //            ? day30Max = new [] {day30Max, thisCandle}.MaxClose(dateTimeOffset, 30)
    //            : Data.MaxClose(dateTimeOffset, 30);


    //        if(day30Max != temp)
    //            Debug.WriteLine($"{dateTimeOffset} - {Data.Symbol} - Max - {day30Max}");


    //        if (ReferenceEquals(day30Max, thisCandle))
    //        {
    //            Side = Side.Buy;
    //            Debug.WriteLine($"{dateTimeOffset} - {Data.Symbol} - {Side}");
    //            OpenCandle = thisCandle;
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