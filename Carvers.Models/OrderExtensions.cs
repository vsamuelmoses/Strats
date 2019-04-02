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

        public static Tuple<CloseOrderTrigger, IClosedOrder> GetCloseOrder(this IOrder order, 
            IStrategy strategy, double tp, double sl, double whenNoneMet, Candle candle,
            Func<CurrencyPair, double> exchangeRateProvider)
        {
            var entryPrice = order.OrderInfo.Price.Value;
            var pair = (CurrencyPair)order.OrderInfo.Symbol;
            if (order is BuyOrder)
            {
                var maxProfit = pair.ProfitLoss(order.OrderInfo.Size, candle.High - entryPrice, exchangeRateProvider);
                var maxLoss = pair.ProfitLoss(order.OrderInfo.Size, entryPrice - candle.Low, exchangeRateProvider);
                var exitPrice = whenNoneMet;
                var trigger = CloseOrderTrigger.TimeLimit;

                if (maxLoss > sl)
                {
                    exitPrice = entryPrice - pair.NeededMovementInPips(order.OrderInfo.Size, sl, exchangeRateProvider);
                    trigger = CloseOrderTrigger.SL;
                }
                else if (maxProfit > tp)
                {
                    exitPrice = entryPrice + pair.NeededMovementInPips(order.OrderInfo.Size, tp, exchangeRateProvider);
                    trigger = CloseOrderTrigger.TP;
                }

                return  Tuple.Create<CloseOrderTrigger, IClosedOrder>( trigger, new SellOrder((BuyOrder)order,
                    new OrderInfo(candle.TimeStamp, order.OrderInfo.Symbol, strategy, exitPrice.USD(),
                        order.OrderInfo.Size, candle), exchangeRateProvider));
            }
            else if (order is ShortSellOrder)
            {
                var maxLoss = pair.ProfitLoss(order.OrderInfo.Size, candle.High - entryPrice, exchangeRateProvider);
                var maxProfit = pair.ProfitLoss(order.OrderInfo.Size, entryPrice - candle.Low, exchangeRateProvider);

                var exitPrice = whenNoneMet;
                var trigger = CloseOrderTrigger.TimeLimit;

                if (maxLoss > sl)
                {
                    exitPrice = entryPrice + pair.NeededMovementInPips(order.OrderInfo.Size,  sl, exchangeRateProvider);
                    trigger = CloseOrderTrigger.SL;

                }
                else if (maxProfit > tp)
                {
                    exitPrice = entryPrice - pair.NeededMovementInPips(order.OrderInfo.Size, tp, exchangeRateProvider);
                    trigger = CloseOrderTrigger.TP;

                }

                return Tuple.Create<CloseOrderTrigger, IClosedOrder>(trigger, new BuyToCoverOrder((ShortSellOrder)order,
                    new OrderInfo(candle.TimeStamp, order.OrderInfo.Symbol, strategy, exitPrice.USD(),
                        order.OrderInfo.Size, candle), exchangeRateProvider));
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