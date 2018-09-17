﻿using Carvers.Infra.Extensions;
using Carvers.Models.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;

namespace Carvers.Models.Indicators
{
    public interface IContext
    {

    }

    public static class Extensions
    {
        public static Lookback Add(this Lookback lb, Candle candle)
        {
            if (lb.Period == lb.Count)
            {
                Candle none;
                lb.Candles.TryDequeue(out none);
            }

            Debug.Assert(lb.Candles.All(c => c.TimeStamp != candle.TimeStamp));

            lb.Candles.Enqueue(candle);
            return new Lookback(lb.Period, lb.Candles);
        }

        public static bool IsComplete(this Lookback lb)
            => lb.Period == lb.Count;
    }

    public class Lookback
    {
        public Lookback(int period, ConcurrentQueue<Candle> candles)
        {
            Candles = candles;
            HighCandle = candles.OrderBy(candle => candle.Ohlc.High).LastOrDefault();
            LowCandle = candles.OrderBy(candle => candle.Ohlc.Low).FirstOrDefault();
            Period = period;
        }

        public int Count => Candles.Count;
        public ConcurrentQueue<Candle> Candles { get; }
        public Candle HighCandle { get; }
        public Candle LowCandle { get; }
        public Candle LastCandle => Candles.Last();
        public int Period { get; }
    }


    public enum Side
    {
        Buy,
        ShortSell
    }

    public class Strategy : IStrategy
    {
        public IOrder OpenOrder { get; private set; }

        public Strategy(string strategyName)
        {
            openOrders = new Subject<IOrder>();
            closeddOrders = new Subject<IOrder>();
            closedOrders = new ConcurrentBag<IClosedOrder>();

            //var logReport = new StrategyLogReport(new[] { this }, logName: strategyName);
            //var chartReport = new StrategyChartReport(new[] { this }, Dispatcher.CurrentDispatcher);
            //var summaryReport = new StrategySummaryReport(new[] { this });
            //Reporters = new Carvers.Infra.ViewModels.Reporters(logReport, chartReport, summaryReport);
        }

        public void Execute(DateTimeOffset dateTimeOffset)
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            if (OpenOrder != null)
                Debug.WriteLine(OpenOrder.OrderInfo.ToCsv());

            openOrders.OnCompleted();
            closeddOrders.OnCompleted();
        }

        public void Open<T>(IOpenOrder<T> order)
            where T : IClosedOrder
        {
            OpenOrder = order;
            openOrders.OnNext(OpenOrder);
        }

        public void Close(IClosedOrder order)
        {
            OpenOrder = null;
            closedOrders.Add(order);
            closeddOrders.OnNext(order);
        }

        private readonly Subject<IOrder> openOrders;
        public IObservable<IOrder> OpenOrders => openOrders;

        private readonly Subject<IOrder> closeddOrders;
        public IObservable<IOrder> CloseddOrders => closeddOrders;

        private readonly ConcurrentBag<IClosedOrder> closedOrders;
        public IEnumerable<IClosedOrder> ClosedOrders => closedOrders;

        public Price ProfitLoss => ClosedOrders.ProfitLoss();
        public StockData Data => throw new NotImplementedException();
        //public Carvers.Infra.ViewModels.Reporters Reporters { get; private set; }
    }

    public class StrategyContext : IContext
    {
        public StrategyContext(Strategy strategy, Lookback lb, IContextInfo contextInfo)
        {
            Strategy = strategy;
            Lb = lb;
            Info = contextInfo;
        }

        public Lookback Lb { get; }
        public Strategy Strategy { get; }
        public IContextInfo Info { get; }
    }

    public interface IContextInfo { }
    public class EmptyContext : IContextInfo { }

    public class EntryContextInfo : IContextInfo
    {
        public EntryContextInfo(Candle entryCandle,
            Candle hCandle,
            Candle lCandle)
        {
            EntryCandle = entryCandle;
            HCandle = hCandle;
            LCandle = lCandle;
        }

        public Candle EntryCandle { get; }
        public Candle HCandle { get; }
        public Candle LCandle { get; }
    }


    public class Condition<T>
        where T : IContext
    {
        private readonly Func<T, bool> predicate;
        private readonly Condition<T> onFailure;
        private readonly Condition<T> onSuccess;

        public Condition(
            Condition<T> onSuccess,
            Condition<T> onFailure,
            Func<T, bool> predicate)
        {
            this.onSuccess = onSuccess;
            this.onFailure = onFailure;
            this.predicate = predicate;
        }

        public Tuple<Condition<T>, T> Evaluate(T context, Candle candle)
        {
            var newContext = (T)context.Add(candle);
            if (predicate(newContext))
                return Tuple.Create(onSuccess, newContext);
            return Tuple.Create(onFailure, newContext);
        }

        public bool IsSuccess { get; }
    }


    public static class ConditionExtensions
    {
        public static IContext Add(this IContext context, Candle candle)
        {
            switch (context)
            {
                case StrategyContext sc:
                    return new StrategyContext(sc.Strategy, sc.Lb.Add(candle), sc.Info);
                default:
                    throw new Exception("Unexpected");
            }
        }
    }

    public static class BooleanIndicators
    {
        public static bool TrendContinuation(this IEnumerable<Candle> candles)
        {
            var allDistinctSentiment = candles.Select(CandleSentiment.Of).Distinct();
            var allCandlesOfSameSentiment = allDistinctSentiment.Count() == 1;

            if (!allCandlesOfSameSentiment)
                return false;

            switch (allDistinctSentiment.Single())
            {
                case CandleSentiment cs when cs == CandleSentiment.Red:
                        return candles.ExecuteCondition(ClosedLowerThanPreviousLow);
                case CandleSentiment cs when cs == CandleSentiment.Green:
                    return candles.ExecuteCondition(ClosedHigherThanPreviousHigh);
                default:
                        return false;
            }
        }

        public static bool TrendReversal(Candle candle1, Candle candle2)
        {
            var prevSentiment = CandleSentiment.Of(candle1);
            var thisSentiment = CandleSentiment.Of(candle2);

            if (thisSentiment == prevSentiment)
                return false;

            if (prevSentiment == CandleSentiment.Green 
                && thisSentiment == CandleSentiment.Red
                && ClosedLowerThanPreviousLow(candle1, candle2))
                return true;
            
            if (prevSentiment == CandleSentiment.Red
                && thisSentiment == CandleSentiment.Green
                && ClosedHigherThanPreviousHigh(candle1, candle2))
                return true;

            return false;
        }
        

        public static bool ClosedHigherThanPreviousHigh(Candle candle1, Candle candle2)
            => candle2.Ohlc.Close > candle1.Ohlc.High;

        public static bool ClosedLowerThanPreviousLow(Candle candle1, Candle candle2)
            => candle2.Ohlc.Close < candle1.Ohlc.Low;


        public static bool ExecuteCondition(this IEnumerable<Candle> candles, Func<Candle, Candle, bool> condition)
        {
            return candles.Aggregate(Tuple.Create(true, Candle.Null), (tup, candle) =>
            {
                if (tup.Item1)
                {
                    if (tup.Item2 == Candle.Null)
                        return Tuple.Create(true, candle);
                    else
                        return Tuple.Create(condition(tup.Item2, candle), candle);
                }
                return Tuple.Create(false, Candle.Null);
            }).Item1;
        }

        public static bool TrendContinuation(object p)
        {
            throw new NotImplementedException();
        }
    }

    public class TrendContinuationCondition : Condition<StrategyContext>
    {
        public TrendContinuationCondition(Condition<StrategyContext> onSuccess, Condition<StrategyContext> onFailure) 
            : base(onSuccess, onFailure, context => BooleanIndicators.TrendContinuation(context.Lb.Candles.TakeLast(2)))
        {
        }
    }


    public class TrendReversalCondition : Condition<StrategyContext>
    {
        public TrendReversalCondition(Condition<StrategyContext> onSuccess, Condition<StrategyContext> onFailure)
            : base(onSuccess, onFailure, context => BooleanIndicators.TrendReversal(context.Lb.Candles.TakeLast(2).First(), context.Lb.Candles.TakeLast(2).Last()))
        {
        }
    }

    public class ExitCondition : Condition<StrategyContext>
    {
        public ExitCondition(Condition<StrategyContext> onSuccess, Condition<StrategyContext> onFailure)
            : base(onSuccess, onFailure, 
                  context => BooleanIndicators.TrendReversal(context.Lb.Candles.TakeLast(2).First(), context.Lb.Candles.TakeLast(2).Last()))
        {
        }
    }

    public class FuncContextReadyCondition : FuncCondition<StrategyContext>
    {
        public FuncContextReadyCondition(Func<FuncCondition<StrategyContext>> onSuccess, Func<FuncCondition<StrategyContext>> onFailure)
            : base(onSuccess, onFailure, context => context.Lb.IsComplete())
        {
        }
    }

    public class FuncTrendContinuationCondition : FuncCondition<StrategyContext>
    {
        public FuncTrendContinuationCondition(
            Func<FuncCondition<StrategyContext>> onSuccess, 
            Func<FuncCondition<StrategyContext>> onFailure)
            : base(onSuccess, onFailure, 
                  new List<Func<StrategyContext, bool>>
                  {
                    context => BooleanIndicators.TrendContinuation(context.Lb.Candles.TakeLast(2)),
                    context => {
                        if (CandleSentiment.Of(context.Lb.LastCandle) == CandleSentiment.Red)
                            return CandleSentiment.Of(context.Lb.Candles.ToSingleCandle(TimeSpan.FromMinutes(context.Lb.Period))) == CandleSentiment.Green;

                        if (CandleSentiment.Of(context.Lb.LastCandle) == CandleSentiment.Green)
                            return CandleSentiment.Of(context.Lb.Candles.ToSingleCandle(TimeSpan.FromMinutes(context.Lb.Period))) == CandleSentiment.Red;

                        return false;
                    }
                  })
        {
        }
    }


    public class FuncTrendReversalCondition : FuncCondition<StrategyContext>
    {
        public FuncTrendReversalCondition(
            Func<FuncCondition<StrategyContext>> onSuccess, 
            Func<FuncCondition<StrategyContext>> onFailure)
            : base(onSuccess, onFailure,
                  new List<Func<StrategyContext, bool>> {

                      context => BooleanIndicators.TrendReversal(context.Lb.Candles.TakeLast(2).First(), context.Lb.Candles.TakeLast(2).Last()),
                      context => {

                          if (CandleSentiment.Of(context.Lb.LastCandle) == CandleSentiment.Green)
                              return !context.Lb.LastCandle.IsHigherThan(context.Lb.HighCandle);

                          if (CandleSentiment.Of(context.Lb.LastCandle) == CandleSentiment.Red)
                              return !context.Lb.LastCandle.IsLowerThan(context.Lb.LowCandle);

                          throw new Exception("Unexpected - not expecting Doji. The first trendReversal predicate should have gotten rid of that");
                      },
                      context => {

                          if (CandleSentiment.Of(context.Lb.LastCandle) == CandleSentiment.Green)
                              return (context.Lb.HighCandle.Ohlc.High.Value - context.Lb.LastCandle.Ohlc.Close.Value) * 100000 > 100;

                          if (CandleSentiment.Of(context.Lb.LastCandle) == CandleSentiment.Red)
                              return (context.Lb.LastCandle.Ohlc.Close.Value - context.Lb.LowCandle.Ohlc.Low.Value) * 100000 > 100;

                          throw new Exception("Unexpected - not expecting Doji. The first trendReversal predicate should have gotten rid of that");
                      }
                  },
                  onSuccessAction: context => 
                  {
                      var lastCandle = context.Lb.LastCandle;
                      var candleSentiment = CandleSentiment.Of(context.Lb.LastCandle);
                      if (candleSentiment == CandleSentiment.Red)
                      {
                          var shortSellOrder = new ShortSellOrder(
                              new OrderInfo(lastCandle.TimeStamp, CurrencyPair.EURGBP, context.Strategy, lastCandle.Ohlc.Close, 100000));
                          context.Strategy.Open(shortSellOrder);
                          return new StrategyContext(context.Strategy, context.Lb, new EntryContextInfo(lastCandle, context.Lb.HighCandle, context.Lb.LowCandle));
                      }
                      if (candleSentiment == CandleSentiment.Green)
                      {
                          context.Strategy.Open(new BuyOrder(
                              new OrderInfo(lastCandle.TimeStamp, CurrencyPair.EURGBP, context.Strategy, lastCandle.Ohlc.Close, 100000)));
                          return new StrategyContext(context.Strategy, context.Lb, new EntryContextInfo(lastCandle, context.Lb.HighCandle, context.Lb.LowCandle));
                      }

                      throw new Exception("Unexpected error");
                  })
        {
        }
    }

    public class FuncExitCondition : FuncCondition<StrategyContext>
    {
        public FuncExitCondition(
            Func<FuncCondition<StrategyContext>> onSuccess,
            Func<FuncCondition<StrategyContext>> onFailure)
            : base(onSuccess, onFailure,
                  predicate: context =>
                  {
                      if (context.Strategy.OpenOrder is BuyOrder)
                      {
                          var candle = context.Lb.LastCandle;
                          var entryContext = ((EntryContextInfo)context.Info);

                          if (candle.IsLowerThan(entryContext.EntryCandle))
                          {
                              var pl = entryContext.EntryCandle.Ohlc.Low.Value - entryContext.EntryCandle.Ohlc.Close.Value;
                              Console.WriteLine($"Bought,{entryContext.EntryCandle.TimeStamp},{candle.TimeStamp}, {pl * 100000}");

                              context.Strategy.Close(
                                  new SellOrder((BuyOrder)context.Strategy.OpenOrder, 
                                    new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, context.Strategy, entryContext.EntryCandle.Ohlc.Low, 100000, candle)));
                              return true;
                          }

                          Price target = (entryContext.HCandle.Ohlc.High - entryContext.EntryCandle.Ohlc.Close) * (2d / 3);
                          if (candle.Ohlc.High - entryContext.EntryCandle.Ohlc.Close > target)
                          {
                              Console.WriteLine($"Bought,{entryContext.EntryCandle.TimeStamp},{candle.TimeStamp}, {target.Value * 100000}");

                              context.Strategy.Close(
                                  new SellOrder((BuyOrder)context.Strategy.OpenOrder,
                                    new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, context.Strategy, target, 100000, candle)));
                              return true;
                          }

                          return false;
                      }

                      if (context.Strategy.OpenOrder is ShortSellOrder)
                      {
                          var candle = context.Lb.LastCandle;
                          var entryContext = ((EntryContextInfo)context.Info);
                          
                          if (candle.IsHigherThan(entryContext.EntryCandle))
                          {

                              var pl = entryContext.EntryCandle.Ohlc.High.Value - entryContext.EntryCandle.Ohlc.Close.Value;
                              Console.WriteLine($"SOLD,{entryContext.EntryCandle.TimeStamp},{candle.TimeStamp}, {-1 * pl * 100000}");

                              context.Strategy.Close(
                                  new BuyToCoverOrder((ShortSellOrder)context.Strategy.OpenOrder,
                                    new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, context.Strategy, entryContext.EntryCandle.Ohlc.High, 100000, candle)));
                              return true;
                          }


                          var target = (entryContext.EntryCandle.Ohlc.Close - entryContext.LCandle.Ohlc.Low) * (2d / 3);
                          Debug.Assert(target.Value > 0);
                          if (entryContext.EntryCandle.Ohlc.Close - candle.Ohlc.Low > target)
                          {
                              Console.WriteLine($"SOLD,{entryContext.EntryCandle.TimeStamp},{candle.TimeStamp}, {target.Value * 100000}");

                              context.Strategy.Close(
                                  new BuyToCoverOrder((ShortSellOrder)context.Strategy.OpenOrder,
                                    new OrderInfo(candle.TimeStamp, CurrencyPair.EURGBP, context.Strategy, target, 100000, candle)));
                              return true;
                          }

                          return false;
                      }

                      throw new Exception("Unexpected error");
                  })
        {
        }
    }


    public class FuncCondition<T>
       where T : IContext
    {
        private readonly Func<T, T> onFailureAction;
        private readonly Func<T, T> onSuccessAction;
        private readonly IEnumerable<Func<T, bool>> predicates;
        private readonly Func<FuncCondition<T>> onFailure;
        private readonly Func<FuncCondition<T>> onSuccess;


        public FuncCondition(
            Func<FuncCondition<T>> onSuccess,
            Func<FuncCondition<T>> onFailure,
            Func<T, bool> predicate)
            : this(onSuccess, onFailure, new List<Func<T, bool>> { predicate }, null, null)
        {
        }

        public FuncCondition(
            Func<FuncCondition<T>> onSuccess,
            Func<FuncCondition<T>> onFailure,
            List<Func<T, bool>> predicates)
            : this(onSuccess, onFailure, predicates, null, null)
        {
        }

        public FuncCondition(
            Func<FuncCondition<T>> onSuccess,
            Func<FuncCondition<T>> onFailure,
            List<Func<T, bool>> predicates,
            Func<T, T> onSuccessAction = null, 
            Func<T, T> onFailureAction = null)
        {
            this.onSuccess = onSuccess;
            this.onFailure = onFailure;
            this.predicates = predicates;

            if (onFailureAction == null)
                onFailureAction = _ => _;
            this.onFailureAction = onFailureAction;

            if (onSuccessAction == null)
                onSuccessAction = _ => _;
            this.onSuccessAction = onSuccessAction;
        }

        public Tuple<Func<FuncCondition<T>>, T> Evaluate(T context, Candle candle)
        {
            var newContext = (T)context.Add(candle);
            if (predicates.All(predicate => predicate(newContext)))
                return Tuple.Create(onSuccess, onSuccessAction(newContext));
            else
                return Tuple.Create(onFailure, onFailureAction(newContext));
        }

        public bool IsSuccess { get; }
    }
}
