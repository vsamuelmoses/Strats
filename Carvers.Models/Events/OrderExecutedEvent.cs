using System;

namespace Carvers.Models.Events
{
    public class OrderExecutedEvent : DateTimeEvent<IOrder>
    {
        public OrderExecutedEvent(DateTimeOffset dateTimeOffset, IOrder anEvent) 
            : base(dateTimeOffset, anEvent)
        {
        }
    }

    public class LowestCandleInLbEvent : DateTimeEvent<Candle>
    {
        public LowestCandleInLbEvent(DateTimeOffset dateTimeOffset, Candle anEvent) 
            : base(dateTimeOffset, anEvent)
        {
        }
    }

    public class MarketOpeningIndicator : DateTimeEvent<Candle>
    {
        public MarketOpeningIndicator(DateTimeOffset dateTimeOffset, Candle anEvent) 
            : base(dateTimeOffset, anEvent)
        {
        }
    }
}