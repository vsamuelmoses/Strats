using System;
using Carvers.Models.Extensions;

namespace Carvers.Models
{
    public class SellOrder : IClosedOrder
    {
        public SellOrder(BuyOrder buyOrder, IOrderInfo info)
        {
            BuyOrder = buyOrder;
            ProfitLoss = info.Price - buyOrder.OrderInfo.Price;
            OrderInfo = info;
        }

        public SellOrder(BuyOrder buyOrder, IOrderInfo info, Func<CurrencyPair, double> excahngeRateProvider)
        {
            BuyOrder = buyOrder;
            ProfitLoss = ((CurrencyPair)info.Symbol).ProfitLoss(BuyOrder.OrderInfo.Size, (info.Price - buyOrder.OrderInfo.Price).Value, excahngeRateProvider).USD();
            OrderInfo = info;
        }


        public string ToCsv()
        {
            return $"{GetType().Name},{OrderInfo.ToCsv()},{BuyOrder.ToCsv()},{ProfitLoss}";
        }

        public BuyOrder BuyOrder { get; }
        public Price ProfitLoss { get; }
        public IOrderInfo OrderInfo { get; }
    }
}