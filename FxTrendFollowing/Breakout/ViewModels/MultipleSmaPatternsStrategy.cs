using System;
using System.Collections.Generic;
using System.Linq;
using Carvers.Infra.Extensions;
using Carvers.Infra.Math.Geometry;
using Carvers.Models;
using Carvers.Models.Indicators;

namespace FxTrendFollowing.Breakout.ViewModels
{
    public static class MultipleSmaPatternsStrategy
    {
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
                            var smas = new double[]
                            {

                                ctx.Sma50.Current,
                                ctx.Sma100.Current,
                                ctx.Sma250.Current,
                                ctx.Sma500.Current,
                                ctx.Sma1000.Current,
                                ctx.Sma3600.Current,
                            };


                            return smas.Min() == ctx.Sma3600.Current;
                            //return smas.SequenceEqual(smas.OrderByDescending(s => s));
                                   
                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    //if (ctx.Sma3600.Averages.Last() < ctx.Sma100.Averages.Last())
                    //    return ctx.PlaceOrder(ctx.LastCandle, Side.ShortSell)
                    //        .AddContextInfo(new SmaContextInfo(ctx.Sma3600.Current, ctx.Sma50.Current));

                    //if (ctx.Sma3600.Averages.Last() > ctx.Sma100.Averages.Last())
                    return ctx.PlaceOrder(ctx.LastCandle, Side.Buy)
                        .AddContextInfo(new SmaContextInfo(ctx.Sma3600.Current, ctx.Sma50.Current));

                    //throw new Exception("Unexpected");
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

                            var smas = new double[]
                            {

                                ctx.Sma50.Current,
                                ctx.Sma100.Current,
                                ctx.Sma250.Current,
                                ctx.Sma500.Current,
                                ctx.Sma1000.Current,
                                ctx.Sma3600.Current,
                            };

                            return smas.Max() == ctx.Sma3600.Current;

                            //var movingAvgLine1Pts = ctx.Sma50.Averages;
                            //var movingAvgLine2Pts = ctx.Sma250.Averages;

                            //var movingAvgLine1 = movingAvgLine1Pts.GetLine(3);
                            //var movingAvgLine2 = movingAvgLine2Pts.GetLine(3);


                            //return movingAvgLine2.IntersectionPoint(movingAvgLine1).Item2
                            //       && movingAvgLine1Pts.Last() < movingAvgLine2Pts.Last();

                            //return smas.Max() == ctx.Sma50.Current;

                            //return Enumerable.SequenceEqual(smas, smas.OrderByDescending(s => s));
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