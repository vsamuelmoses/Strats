using System;
using System.Collections.Generic;
using System.Linq;
using Carvers.Models;
using Carvers.Models.Indicators;
using FxTrendFollowing.Breakout.ViewModels;

namespace FxTrendFollowing.Strategies
{
    public static class MovingAveragesPerfectOrder
    {
        private static int candlesAfterPerfectOrder = 0;
        private const string InterestedSMA = Indicators.CloseSma14;

        private static Func<FuncCondition<SMAContext>> contextReadyCondition = () =>
            new FuncCondition<SMAContext>(
                onSuccess: entryCondition,
                onFailure: contextReadyCondition,
                predicate: context => context.IsReady());

        private static Func<FuncCondition<SMAContext>> entryCondition = () =>
            new FuncCondition<SMAContext>(
                onSuccess: exitCondition,
                onFailure: entryCondition,
                predicates: new List<Func<SMAContext, bool>>()
                {
                    {
                        ctx =>
                        {
                            var smas = new List<double>()
                            {
                                ctx.Indicators[Indicators.SMA10].OfType<MovingAverage>().Value,
                                ctx.Indicators[Indicators.SMA20].OfType<MovingAverage>().Value,
                                ctx.Indicators[Indicators.SMA50].OfType<MovingAverage>().Value,
                                ctx.Indicators[Indicators.SMA100].OfType<MovingAverage>().Value,
                                ctx.Indicators[Indicators.SMA200].OfType<MovingAverage>().Value,
                            };

                            var isPerfectOrder = smas.OrderByDescending(v => v).SequenceEqual(smas) ||
                                   smas.OrderBy(v => v).SequenceEqual(smas);

                            if (isPerfectOrder)
                                candlesAfterPerfectOrder++;
                            else
                                candlesAfterPerfectOrder = 0;


                            if (isPerfectOrder && candlesAfterPerfectOrder == 5)
                            {
                                candlesAfterPerfectOrder = 0;
                                return true;
                            }

                            return false;
                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    if (ctx.Indicators[Indicators.SMA10].OfType<MovingAverage>().Value 
                        < ctx.Indicators[Indicators.SMA200].OfType<MovingAverage>().Value)
                        return ctx.PlaceOrder(ctx.LastCandle, Side.ShortSell);

                    if (ctx.Indicators[Indicators.SMA10].OfType<MovingAverage>().Value
                        > ctx.Indicators[Indicators.SMA200].OfType<MovingAverage>().Value)
                        return ctx.PlaceOrder(ctx.LastCandle, Side.Buy);

                    throw new Exception("Unexpected");
                });

        private static Func<FuncCondition<SMAContext>> exitCondition = () =>
            new FuncCondition<SMAContext>(
                onSuccess: entryCondition,
                onFailure: exitCondition,
                predicates: new List<Func<SMAContext, bool>>()
                {
                    {
                        /* When Moving averages cross */
                        ctx =>
                        {
                            var smas = new List<double>()
                            {
                                ctx.Indicators[Indicators.SMA10].OfType<MovingAverage>().Value,
                                ctx.Indicators[Indicators.SMA20].OfType<MovingAverage>().Value,
                                ctx.Indicators[Indicators.SMA50].OfType<MovingAverage>().Value,
                                ctx.Indicators[Indicators.SMA100].OfType<MovingAverage>().Value,
                                ctx.Indicators[Indicators.SMA200].OfType<MovingAverage>().Value,
                            };

                            return !smas.OrderByDescending(v => v).SequenceEqual(smas) &&
                                   !smas.OrderBy(v => v).SequenceEqual(smas);
                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    if (ctx.Strategy.OpenOrder is BuyOrder)
                    {
                        var candle = ctx.LastCandle;
                        ctx.Strategy.Close(
                            new SellOrder((BuyOrder)ctx.Strategy.OpenOrder,
                                new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, ctx.Strategy, candle.Ohlc.Close, 100000, candle)));
                        return ctx;
                    }

                    if (ctx.Strategy.OpenOrder is ShortSellOrder)
                    {
                        var candle = ctx.LastCandle;
                        ctx.Strategy.Close(
                            new BuyToCoverOrder((ShortSellOrder)ctx.Strategy.OpenOrder,
                                new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, ctx.Strategy, candle.Ohlc.Close, 100000, candle)));
                        return ctx;
                    }

                    throw new Exception("Unexpected error");
                });


        public static Func<FuncCondition<SMAContext>> Strategy = contextReadyCondition;
    }
}
