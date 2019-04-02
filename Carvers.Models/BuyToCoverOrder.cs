using System;
using Carvers.Models.Extensions;

namespace Carvers.Models
{
    public class BuyToCoverOrder : IClosedOrder
    {
        public BuyToCoverOrder(ShortSellOrder openOrder, IOrderInfo orderInfo)
        {
            OpenOrder = openOrder;
            ProfitLoss = openOrder.OrderInfo.Price - orderInfo.Price;
            OrderInfo = orderInfo;
        }

        public BuyToCoverOrder(ShortSellOrder openOrder, IOrderInfo orderInfo, Func<CurrencyPair, double> excahngeRateProvider)
        {
            OpenOrder = openOrder;
            ProfitLoss = ((CurrencyPair)orderInfo.Symbol)
                .ProfitLoss(openOrder.OrderInfo.Size, (openOrder.OrderInfo.Price - orderInfo.Price).Value, excahngeRateProvider).USD();
            OrderInfo = orderInfo;
        }

        public string ToCsv()
        {
            return $"{GetType().Name},{OrderInfo.ToCsv()},{OpenOrder.ToCsv()},{ProfitLoss}";
        }

        public Price ProfitLoss { get; }
        public IOrderInfo OrderInfo { get; }
        public ShortSellOrder OpenOrder { get; }
    }
}