using System;
using System.Linq;
using Carvers.IBApi;
using Carvers.Models;
using Carvers.Models.Extensions;
using Carvers.Models.Indicators;

namespace ShadowStrengthStrategy.Models
{
    public static class SssContextExtensions
    {
        public static SssContext PlaceOrder(this SssContext context, DateTimeOffset timeStamp, Candle candle, double entryPrice,
            Side side)
        {
            if (side == Side.ShortSell)
            {
                context.LogFile.WriteWithTs($"Placing SELL Order at {entryPrice}");

                var shortSellOrder = new ShortSellOrder(
                    new OrderInfo(timeStamp, context.Instrument, context.Strategy, entryPrice.USD(),
                        100000));
                context.Strategy.Open(shortSellOrder);


                return new SssContext(context.Strategy, context.LogFile, context.Instrument, context.ShadowIndicatorFile, context.Indicators,
                    context.LookbackCandles, context.ContextInfos);
            }

            if (side == Side.Buy)
            {
                context.LogFile.WriteWithTs($"Placing BUY Order at {entryPrice}");

                context.Strategy.Open(new BuyOrder(
                    new OrderInfo(timeStamp, context.Instrument, context.Strategy, entryPrice.USD(),
                        100000)));
                return new SssContext(context.Strategy, context.LogFile, context.Instrument, context.ShadowIndicatorFile, context.Indicators,
                    context.LookbackCandles, context.ContextInfos);
            }

            throw new Exception("unexpected error");
        }

        public static SssContext AddContextInfo(this SssContext context, IContextInfo info)
            => new SssContext(context.Strategy, context.LogFile, context.Instrument, context.ShadowIndicatorFile, context.Indicators, context.LookbackCandles,
                context.ContextInfos.ToList().Append(info).ToList());

        public static SssContext ReplaceContextInfo(this SssContext context,
            IContextInfo newInfo)
        {

            var contextInfos = context.ContextInfos.ToList();

            var oldInfo = contextInfos.SingleOrDefault(info => info.GetType() == newInfo.GetType());

            if (oldInfo != null)
                contextInfos.Remove(oldInfo);

            return new SssContext(context.Strategy, context.LogFile, context.Instrument, context.ShadowIndicatorFile, context.Indicators,
                context.LookbackCandles,
                contextInfos.Append(newInfo).ToList());
        }

    }
}
