using System;
using System.Collections.Generic;

namespace Carvers.Models
{
    public interface IStrategy
    {
        IObservable<IOrder> OpenOrders { get; }
        IObservable<IOrder> CloseddOrders { get; }
        IEnumerable<IClosedOrder> ClosedOrders { get; }
        void Execute(DateTimeOffset dateTimeOffset);
        StockData Data { get; }
        void Stop();
        Price ProfitLoss { get; }
    }

    public interface IStrategyOptions
    {

    }
}