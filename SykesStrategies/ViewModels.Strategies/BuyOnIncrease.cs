using System;
using System.Collections.Generic;

namespace SykesStrategies.ViewModels.Strategies
{
    //public class BuyOnIncrease : IStrategy
    //{
    //    private readonly double increment;
    //    public BuyOnIncrease(StockData data, int increment)
    //    {
    //        this.increment = increment;
    //        Data = data;
    //        closedOrders = new List<IClosedOrder>();
    //    }

    //    private IOpenOrder openOrder;
    //    public IOpenOrder OpenOrder => openOrder;
    //    private List<IClosedOrder> closedOrders;
    //    public IEnumerable<IClosedOrder> ClosedOrders => closedOrders;
    //    public void Execute(DateTimeOffset dateTimeOffset)
    //    {
    //        var thisCandle = Data.CandleAt(dateTimeOffset);
    //        if (thisCandle == null)
    //            return;

    //        if (openOrder != null)
    //        {
    //            closedOrders.Add(openOrder.Close(thisCandle.Ohlc.Close, dateTimeOffset));
    //            openOrder = null;
    //            return;
    //        }

    //        var previousDayCandle = Data.CandleAt(dateTimeOffset.Subtract(TimeSpan.FromDays(1)).Date);
    //        if (previousDayCandle == null)
    //            return;

    //        if(thisCandle.Ohlc.Close.Value.PercentageOf(previousDayCandle.Ohlc.Close.Value) >= increment)
    //            openOrder = new BuyOrder(thisCandle.Ohlc.Close, dateTimeOffset);
    //    }

    //    public void Stop()
    //    {
    //    }

    //    public Price ProfitLoss { get; }

    //    public StockData Data { get; }
    //}
}