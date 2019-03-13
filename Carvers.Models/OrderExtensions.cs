using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using Carvers.Models.Extensions;

namespace Carvers.Models
{
    public static class OrderExtensions
    {
        public enum CloseOrderTrigger
        {
            Unknown,
            TimeLimit,
            TP,
            SL
        }

        public static Tuple<CloseOrderTrigger, IClosedOrder> GetCloseOrder(this IOrder order, IStrategy strategy, double tp, double sl, double whenNoneMet, Candle candle)
        {
            var entryPrice = order.OrderInfo.Price.Value;

            if (order is BuyOrder)
            {
                var maxProfit = candle.High - entryPrice;
                var maxLoss = entryPrice - candle.Low;
                var exitPrice = whenNoneMet;
                var trigger = CloseOrderTrigger.TimeLimit;

                if (maxLoss > sl)
                {
                    exitPrice = entryPrice - sl;
                    trigger = CloseOrderTrigger.SL;
                }
                else if (maxProfit > tp)
                {
                    exitPrice = entryPrice + tp;
                    trigger = CloseOrderTrigger.TP;
                }

                return  Tuple.Create<CloseOrderTrigger, IClosedOrder>( trigger, new SellOrder((BuyOrder)order,
                    new OrderInfo(candle.TimeStamp, order.OrderInfo.Symbol, strategy, exitPrice.USD(),
                        order.OrderInfo.Size, candle)));
            }
            else if (order is ShortSellOrder)
            {
                var maxLoss = candle.High - entryPrice;
                var maxProfit = entryPrice - candle.Low;
                var exitPrice = whenNoneMet;
                var trigger = CloseOrderTrigger.TimeLimit;

                if (maxLoss > sl)
                {
                    exitPrice = entryPrice + sl;
                    trigger = CloseOrderTrigger.SL;

                }
                else if (maxProfit > tp)
                {
                    exitPrice = entryPrice - tp;
                    trigger = CloseOrderTrigger.TP;

                }

                return Tuple.Create<CloseOrderTrigger, IClosedOrder>(trigger, new BuyToCoverOrder((ShortSellOrder)order,
                    new OrderInfo(candle.TimeStamp, order.OrderInfo.Symbol, strategy, exitPrice.USD(),
                        order.OrderInfo.Size, candle)));
            }
            else
            {
                throw new Exception("Unexpected error");
            }
        }


        public static Price ProfitLoss(this IEnumerable<IClosedOrder> orders)
            => orders.Select(order => order.ProfitLoss).Total();

        public static double CurrentProfitLoss(this IOrder order, Candle candle, int size)
        {
            return CurrentProfitLoss(order, candle.Close, size);
        }


        public static double CurrentProfitLoss(this IOrder order, double price, int size)
        {
            var movement = size * (price - order.OrderInfo.Price.Value);
            if (order is BuyOrder)
                return movement;

            if (order is ShortSellOrder)
                return -1 * movement;

            throw new Exception("Unexpected error");
        }
    }
}