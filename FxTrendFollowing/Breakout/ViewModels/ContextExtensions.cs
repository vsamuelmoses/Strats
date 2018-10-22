using System;
using Carvers.Models;
using Carvers.Models.Indicators;

namespace FxTrendFollowing.Breakout.ViewModels
{
    public static class ContextExtensions
    {
        public static SMAContext PlaceOrder(this SMAContext context, Candle lastCandle, Side side)
        {
            if (side == Side.ShortSell)
            {
                context.Strategy.Open(new ShortSellOrder(
                    new OrderInfo(lastCandle.TimeStamp, CurrencyPair.EURGBP, context.Strategy, lastCandle.Ohlc.Close, 100000)));
                return context;
            }

            if (side == Side.Buy)
            {
                context.Strategy.Open(new BuyOrder(
                    new OrderInfo(lastCandle.TimeStamp, CurrencyPair.EURGBP, context.Strategy, lastCandle.Ohlc.Close, 100000)));
                return context;
            }

            throw new Exception("unexpected error");
        }

        public static SMAContext AddContextInfo(this SMAContext context, IContextInfo info)
            => new SMAContext(context.Strategy, context.Sma50, context.Sma100, context.Sma250, context.Sma500, context.Sma1000, context.Sma3600, context.ExMa3600, context.ExMa3600L, context.ExMa3600H, info);
    }
}