using System;

namespace Carvers.Models
{
    public class BuyOrder : IOpenOrder<SellOrder>
    {
        public BuyOrder(IOrderInfo info)
        {
            OrderInfo = info;
        }

        public string ToCsv()
        {
            return $"{GetType().Name},{OrderInfo.ToCsv()}";
        }

        public SellOrder Close(IOrderInfo info) => new SellOrder(this, info);

        public SellOrder Close(IOrderInfo info, Func<CurrencyPair, double> excahngeRateProvider) => new SellOrder(this, info, excahngeRateProvider);

        
        public IOrderInfo OrderInfo { get; }
    }
}