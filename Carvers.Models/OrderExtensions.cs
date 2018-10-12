using System;
using System.Collections.Generic;
using System.Linq;
using Carvers.Models.Extensions;

namespace Carvers.Models
{
    public static class OrderExtensions
    {
        public static Price ProfitLoss(this IEnumerable<IClosedOrder> orders)
            => orders.Select(order => order.ProfitLoss).Total();

        public static double CurrentProfitLoss(this IOrder order, Candle candle, int size)
        {
            var movement = size * (candle.Close - order.OrderInfo.Price.Value);
            if (order is BuyOrder)
                return movement;

            if (order is ShortSellOrder)
                return -1 * movement;

            throw new Exception("Unexpected error");
        }
    }
}