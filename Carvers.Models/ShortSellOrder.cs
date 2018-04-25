using System;

namespace Carvers.Models
{
    public class ShortSellOrder : IOpenOrder<BuyToCoverOrder>
    {
        public ShortSellOrder(IOrderInfo info)
        {
            OrderInfo = info;
        }

        public BuyToCoverOrder Close(IOrderInfo info) => new BuyToCoverOrder(this, info);

        public BuyToCoverOrder Close(IOrderInfo info, Func<CurrencyPair, double> exchangeRateProvider) => new BuyToCoverOrder(this, info, exchangeRateProvider);

        public string ToCsv()
        {
            return $"{GetType().Name},{OrderInfo.ToCsv()}";
        }

        public IOrderInfo OrderInfo { get; }
    }
}