using System;
using System.Collections.Generic;
using System.Linq;
using Carvers.Infra.Extensions;
using Carvers.Infra.Math.Geometry;
using Carvers.Models;
using Carvers.Models.Indicators;
using Vector = System.Windows.Vector;

namespace FxTrendFollowing.Breakout.ViewModels
{
    public static class SMAAngles
    {
        public static (double, double,double,double) Angle(double[] points)
        {
            var start = new Vector(0, points[0] * 100000);

            var min = points.Except(new[] {points.First(), points.Last()}).Min();
            var max = points.Except(new[] {points.First(), points.Last()}).Max();

            double midpoint = min;

            if (max - points.Last() > points.Last() - min)
                midpoint = max;

            var mid = new Vector(1, midpoint * 100000);
            var end = new Vector(2, points[points.Length-1] * 100000);

            var angle = VectorHelper.GetAngle(start, mid, end);

            return (points[0] * 100000, midpoint * 100000, points[points.Length - 1] * 100000, angle);
        }

        public static bool IsAngleLessThan(double[] points, double angle)
            => Angle(points).Item4 < angle;
    }

    public static class SMAAnglesStrategy
    {
        private static Func<FuncCondition<SMAContext>> contextReadyCondition = () =>
            new FuncCondition<SMAContext>(
                onSuccess: entryCondition,
                onFailure: contextReadyCondition,
                predicate: context =>
                {
                    var isSufficientTimeBetweenLastTrade = true;
                    var lastOrder = context.Strategy.ClosedOrders.LastOrDefault();
                    if (lastOrder != null)
                        isSufficientTimeBetweenLastTrade =
                            context.LastCandle.TimeStamp - lastOrder.OrderInfo.TimeStamp > TimeSpan.FromMinutes(60);

                    return context.IsReady() && isSufficientTimeBetweenLastTrade;
                });

        private static Func<FuncCondition<SMAContext>> entryCondition = () =>
            new FuncCondition<SMAContext>(
                onSuccess: exitCondition,
                onFailure: entryCondition,
                predicates: new List<Func<SMAContext, bool>>()
                {
                    {
                        ctx =>
                        {
                            var angle = SMAAngles.Angle(ctx.ExMa50.Averages.ToArray());

                            if (angle.Item4 < 30)
                            {
                                if (angle.Item3 > angle.Item2
                                    && ctx.ExMa50.Averages.Last() > ctx.Sma250.Averages.Last())
                                    return true;

                                if (angle.Item3 < angle.Item2
                                    && ctx.ExMa50.Averages.Last() < ctx.Sma250.Averages.Last())
                                    return true;

                                return false;

                            }

                            return false;

                        }
                    }
                },
                onSuccessAction: ctx =>
                {
                    var angle = SMAAngles.Angle(ctx.ExMa50.Averages.ToArray());

                    if (angle.Item3 > angle.Item2)
                        return ctx.PlaceOrder(ctx.LastCandle, Side.Buy)
                            .AddContextInfo(new SmaContextInfo(ctx.Sma3600.Current, ctx.ExMa50.Current));

                    if (angle.Item3 < angle.Item2)
                        return ctx.PlaceOrder(ctx.LastCandle, Side.Buy)
                            .AddContextInfo(new SmaContextInfo(ctx.Sma3600.Current, ctx.ExMa50.Current));

                    throw new Exception("Unexpected");
                });

        private static Func<FuncCondition<SMAContext>> exitCondition = () =>
            new FuncCondition<SMAContext>(
                onSuccess: contextReadyCondition,
                onFailure: exitCondition,
                predicates: new List<Func<SMAContext, bool>>()
                {
                    {
                        /* When Moving averages cross */
                        ctx =>
                        {
                            //return ctx.LastCandle.TimeStamp -  ctx.Strategy.OpenOrder.OrderInfo.TimeStamp > TimeSpan.FromMinutes(20) &&
                            // SMAAngles.IsAngleLessThan(ctx.ExMa50.Averages.ToArray(), 45);
                            return (Math.Abs(ctx.Strategy.OpenOrder.CurrentProfitLoss(ctx.LastCandle, 100000)) >= 250);

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


                            //var movingAvgLine1Pts = ctx.Sma50.Averages;
                            //var movingAvgLine2Pts = ctx.Sma250.Averages;
                            //
                            //var movingAvgLine1Pts = ctx.ExMa3600.Averages;
                            //var movingAvgLine2Pts = ctx.Sma3600.Averages;

                            //var movingAvgLine1 = movingAvgLine1Pts.GetLine(3);
                            //var movingAvgLine2 = movingAvgLine2Pts.GetLine(3);

                            //if (ctx.Strategy.OpenOrder is BuyOrder)
                            //{
                            //    return movingAvgLine2.IntersectionPoint(movingAvgLine1).Item2
                            //           && movingAvgLine1Pts.Last() < movingAvgLine2Pts.Last();
                            //}

                            //if (ctx.Strategy.OpenOrder is ShortSellOrder)
                            //{
                            //    return movingAvgLine2.IntersectionPoint(movingAvgLine1).Item2
                            //           && movingAvgLine1Pts.Last() > movingAvgLine2Pts.Last();
                            //}



                            //throw new Exception("Unexpected error");
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
