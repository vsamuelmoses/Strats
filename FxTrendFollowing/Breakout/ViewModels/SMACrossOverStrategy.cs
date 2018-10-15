using Carvers.Infra.Extensions;
using Carvers.Models;
using Carvers.Models.Extensions;
using Carvers.Models.Indicators;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Carvers.Infra.Math.Geometry;

namespace FxTrendFollowing.Breakout.ViewModels
{
    public static class SMACrossOverStrategy
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
                            var sma3600Line = ctx.Sma3600.Averages.GetLine(3);
                            var sma100Line = ctx.Sma100.Averages.GetLine(3);

                            return !sma3600Line.HasSameStartPoint(sma100Line)
                                   && !sma3600Line.HasSameEndPoint(sma100Line)
                                   && sma3600Line.IntersectionPoint(sma100Line).Item2;
                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    if (ctx.Sma3600.Averages.Last() < ctx.Sma100.Averages.Last())
                        return ctx.PlaceOrder(ctx.LastCandle, Side.ShortSell)
                            .AddContextInfo(new SmaContextInfo(ctx.Sma3600.Current, ctx.Sma50.Current));

                    if (ctx.Sma3600.Averages.Last() > ctx.Sma100.Averages.Last())
                        return ctx.PlaceOrder(ctx.LastCandle, Side.Buy)
                            .AddContextInfo(new SmaContextInfo(ctx.Sma3600.Current, ctx.Sma50.Current));

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
                            //if (Math.Abs(ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle, 100000)) >= 250)
                            //    return true;

                            //if (ctx.Strategy.OpenOrder is ShortSellOrder)
                            //{
                            //    var entryDiff = ((SmaContextInfo) ctx.ContextInfo).Sma100Current - ((SmaContextInfo) ctx.ContextInfo).Sma3600Current;
                            //    var currentDiff = ctx.Sma100.Current - ctx.Sma3600.Current;

                            //    if (currentDiff > (2 * entryDiff))
                            //        return true;
                            //}

                            //if (ctx.Strategy.OpenOrder is BuyOrder)
                            //{
                            //    var entryDiff = ((SmaContextInfo) ctx.ContextInfo).Sma100Current - ((SmaContextInfo) ctx.ContextInfo).Sma3600Current;
                            //    var currentDiff = ctx.Sma100.Current - ctx.Sma3600.Current;

                            //    if ((-1*currentDiff) > (2 * -1 * entryDiff))
                            //        return true;
                            //}


                            var movingAvgLine1Pts = ctx.Sma50.Averages;
                            var movingAvgLine2Pts = ctx.Sma3600.Averages;

                            var movingAvgLine1 = movingAvgLine1Pts.GetLine(3);
                            var movingAvgLine2 = movingAvgLine2Pts.GetLine(3);

                            if (ctx.Strategy.OpenOrder is BuyOrder)
                            {
                                return movingAvgLine2.IntersectionPoint(movingAvgLine1).Item2
                                       && movingAvgLine1Pts.Last() > movingAvgLine2Pts.Last();
                            }

                            if (ctx.Strategy.OpenOrder is ShortSellOrder)
                            {
                                return movingAvgLine2.IntersectionPoint(movingAvgLine1).Item2
                                       && movingAvgLine1Pts.Last() < movingAvgLine2Pts.Last();
                            }

                            

                            throw new Exception("Unexpected error");
                        }


                        /* Target of Pips */
                        //ctx =>
                        //{
                        //    if (ctx.Strategy.OpenOrder is BuyOrder)
                        //    {
                        //        var target = (ctx.LastCandle.Close - ctx.Strategy.OpenOrder.OrderInfo.Price.Value) * 100000;
                        //        return (target >= 40) || target <= -20;
                        //    }

                        //    if (ctx.Strategy.OpenOrder is ShortSellOrder)
                        //    {
                        //        var target = (ctx.Strategy.OpenOrder.OrderInfo.Price.Value - ctx.LastCandle.Close) * 100000;
                        //        return (target >= 40) || target <= -20;
                        //    }

                        //    return false;
                        //}


                        //ctx =>
                        //{
                        //    if (ctx.Strategy.OpenOrder is BuyOrder)
                        //        return (ctx.LastCandle.Close < ctx.Sma3600.Current);
                                             

                        //    if (ctx.Strategy.OpenOrder is ShortSellOrder)
                        //        return (ctx.LastCandle.Close > ctx.Sma3600.Current);

                        //    throw new Exception("Unexpected Errr");

                        //}
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
