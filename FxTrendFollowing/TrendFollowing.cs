using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Carvers.Infra.Extensions;
using Carvers.Models;
using Carvers.Models.Extensions;

namespace FxTrendFollowing
{
    public class TrendFollowing : IStrategy
    {
        private readonly Universe universe;
        private readonly CurrencyStrengthFeed strengthFeed;
        private readonly TrendFollowingOptions options;
        private readonly ConcurrentBag<IClosedOrder> closedOrders;
        private readonly Subject<IOrder> openOrders;
        private readonly Subject<IOrder> closeddOrders;
        private IOrder openOrder;


        public TrendFollowing(Universe universe, CurrencyStrengthFeed strengthFeed, TrendFollowingOptions options)
        {
            this.universe = universe;
            this.strengthFeed = strengthFeed;
            this.options = options;

            openOrders = new Subject<IOrder>();
            closeddOrders = new Subject<IOrder>();
            closedOrders = new ConcurrentBag<IClosedOrder>();
        }

        public IObservable<IOrder> OpenOrders => openOrders;
        public IObservable<IOrder> CloseddOrders => closeddOrders;
        public IEnumerable<IClosedOrder> ClosedOrders => closedOrders;
        public StockData Data => null;

        public Price ProfitLoss => ClosedOrders.ProfitLoss();

        public void Execute(DateTimeOffset dateTimeOffset)
        {
            var lbHours = (int) options.LookbackPeriod.TotalHours;

            if (openOrder != null)
            {
                var data = universe.Stocks.Single(stock => stock.Symbol == openOrder.OrderInfo.Symbol);


                var openTime = openOrder.OrderInfo.TimeStamp;
                var averageHourlyMovement = data.Candles
                    .Where(kvp => kvp.Key < openTime)
                    .Select(kvp => kvp.Value)
                    .Cast<MinuteCandle>()
                    .ToHourCandles()
                    .TakeLastInReverse(lbHours)
                    .Average(candle => candle.Ohlc.High.Value - candle.Ohlc.Low.Value);

                var target = averageHourlyMovement / 2;

                double currentMovement = 0;
                if(data.Candles.ContainsKey(dateTimeOffset))
                    currentMovement = Math.Abs(data.Candles[dateTimeOffset].Ohlc.Open.Value - openOrder.OrderInfo.Price.Value);

                if (dateTimeOffset - openOrder.OrderInfo.TimeStamp >= options.HoldPeriod ||
                    currentMovement >= target)
                {
                    

                    if (!data.Candles.ContainsKey(dateTimeOffset))
                        return;

                    IClosedOrder closedOrder;
                    if(openOrder is BuyOrder buyOrder)
                        closedOrder = buyOrder.Close(new OrderInfo(dateTimeOffset, data.Symbol, this, data.Candles[dateTimeOffset].Ohlc.Open));
                    else
                    {
                        var shortSellOrder = openOrder as ShortSellOrder;
                        closedOrder = shortSellOrder.Close(new OrderInfo(dateTimeOffset, data.Symbol, this, data.Candles[dateTimeOffset].Ohlc.Open));
                    }

                    openOrder = null;
                    closedOrders.Add(closedOrder);
                    closeddOrders.OnNext(closedOrder);

                    return;
                }

                return;
            }


            if (dateTimeOffset < new DateTimeOffset(2016, 1, 4, 0, 0, 0, TimeSpan.Zero))
                return;
                 
            if (dateTimeOffset.DayOfWeek == DayOfWeek.Friday)
                return;



            var strong = new List<Tuple<Currency, double>>();
            var weak = new List<Tuple<Currency, double>>();
            var strength = new List<double>();
            foreach (var curr in strengthFeed.Feed)
            {
                strength = 
                    curr.Value
                        .TakeWhile(kvp => kvp.Key < dateTimeOffset)
                        .TakeLastInReverse(lbHours * options.GroupByCount)
                        .Select(kvp => kvp.Value)
                        .GroupBy(options.GroupByCount)
                        .Select(grp => grp.Average(tup => tup.Item2))
                        .ToList(); 

                if (strength.Count < lbHours)
                    return;
                // the strength collection will have values in the reverse order

                //var last2 = strength.TakeLast(2);

                //var dipped = (last2.Count() == 2) && last2.First() < last2.Last();
                //var raised = (last2.Count() == 2) && last2.First() > last2.Last();

                var dipped = true;
                var raised = true;

                strength = strength.Take(lbHours - 1).ToList();

                if (dipped && 
                    strength.SequenceEqual(strength.OrderByDescending(s => s)) &&
                    !strength.AnyConsecutiveItemsEqual())
                {
                    //Debug.Assert(!strong.Any());
                    strong.Add(Tuple.Create(curr.Key, strength.Average()));
                }

                if (raised && 
                    strength.SequenceEqual(strength.OrderBy(s => s)) &&
                    !strength.AnyConsecutiveItemsEqual())
                    weak.Add(Tuple.Create(curr.Key, strength.Average()));
            }

            if (strong.Any() && weak.Any())
            {

                var strongestTup = strong.OrderByDescending(val => val.Item2).First();
                var weakestTup = weak.OrderByDescending(val => val.Item2).Last();

                if (strongestTup.Item2 < weakestTup.Item2)
                    return;

                var strongest = strongestTup.Item1;
                var weakest = weakestTup.Item1;

                var buy = CurrencyPair.Contains(strongest, weakest);

                CurrencyPair pair;
                pair = buy ? CurrencyPair.Get(strongest, weakest) : CurrencyPair.Get(weakest, strongest);

                var data = universe.Stocks.Single(stock => ((CurrencyPair)stock.Symbol) == pair);

                if (!data.Candles.ContainsKey(dateTimeOffset))
                    return;

                if(!buy)
                    openOrder =  new BuyOrder(new OrderInfo(dateTimeOffset, data.Symbol, this, data.Candles[dateTimeOffset].Ohlc.Open));
                else
                    openOrder = new ShortSellOrder(new OrderInfo(dateTimeOffset, data.Symbol, this, data.Candles[dateTimeOffset].Ohlc.Open));


                openOrders.OnNext(openOrder);
            }
        }

        public void Stop()
        {
            if (openOrder != null)
                Debug.WriteLine(openOrder.OrderInfo.ToCsv());

            openOrders.OnCompleted();
            closeddOrders.OnCompleted();
        }
    }

    public abstract class Indicator
    {
        public DateTimeOffset TimeStamp { get; protected set; }
    }

    public class CurrencyStrengthIndicator : Indicator
    {
        public CurrencyStrengthIndicator(DateTimeOffset timestamp, Dictionary<Currency, double> value)
        {
            TimeStamp = timestamp;
            Value = value;
        }

        public Dictionary<Currency, double> Value { get; private set; }

    }

    public abstract class IndicatorStore<T>
        where T: Indicator
    {
        private ConcurrentDictionary<DateTimeOffset, T> _indicators = new ConcurrentDictionary<DateTimeOffset, T>();

        public abstract T GetValue(DateTimeOffset timestamp);
    }

    public class CurrencyStrengthIndicatorStore : IndicatorStore<CurrencyStrengthIndicator>
    {
        public Dictionary<Currency, Dictionary<DateTimeOffset, Tuple<int, double>>> Feed { get; private set; }

        private CurrencyStrengthIndicatorStore(IEnumerable<StockData> stocks, TimeSpan feedSpan)
        {
            Feed = new Dictionary<Currency, Dictionary<DateTimeOffset, Tuple<int, double>>>();

            foreach (var stock in stocks)
            {
                IEnumerable<Tuple<DateTimeOffset, double, double>> strength;
                if (feedSpan == TimeSpan.FromMinutes(1))
                    strength = stock.Candles
                            .Select(candle => candle.Value.ToCurrencyStrength());
                else
                    strength = stock.Candles
                            .Values
                            .Cast<MinuteCandle>()
                            .ToHourCandles()
                            .Select(candle => candle.ToCurrencyStrength());

                strength.Foreach(val =>
                {
                    Add(((CurrencyPair)stock.Symbol).TargetCurrency, val.Item1, val.Item2);
                    Add(((CurrencyPair)stock.Symbol).BaseCurrency, val.Item1, val.Item3);
                });
            }
        }

        public void Add(Currency currency, DateTimeOffset timestamp, double value)
        {
            if (!Feed.ContainsKey(currency))
                Feed.Add(currency, new Dictionary<DateTimeOffset, Tuple<int, double>>());

            if (!Feed[currency].ContainsKey(timestamp))
            {
                Feed[currency].Add(timestamp, Tuple.Create(1, value));
                return;
            }

            var val = Feed[currency][timestamp];
            Feed[currency][timestamp] =
                Tuple.Create(val.Item1 + 1, (value + (val.Item2 * val.Item1)) / (val.Item1 + 1));
        }


        public static CurrencyStrengthIndicatorStore CurrencyStrengthFeedForMinute(IEnumerable<StockData> stocks)
        {
            return new CurrencyStrengthIndicatorStore(stocks, TimeSpan.FromMinutes(1));
        }

        public static CurrencyStrengthIndicatorStore CurrencyStrengthFeedForHour(IEnumerable<StockData> stocks)
        {
            return new CurrencyStrengthIndicatorStore(stocks, TimeSpan.FromHours(1));
        }

        public override CurrencyStrengthIndicator GetValue(DateTimeOffset timestamp)
        {
            return 
                new CurrencyStrengthIndicator(timestamp, 
                    Feed.Where(kvp => kvp.Value.ContainsKey(timestamp))
                        .Select(kvp => new {currency = kvp.Key, strength = kvp.Value[timestamp].Item2})
                        .ToDictionary(val => val.currency, val => val.strength));
        }
    }


    public class IndicatorFeed<T> : IObservable<T>
        where T: Indicator
    {
        private readonly IndicatorStore<T> indicatorStore;
        private readonly Subject<T> indicatorFeed;

        public DateTimeOffset CurrentFeedTimeStamp { get; private set; }
        public DateTimeOffset FeedStartTime { get; private set; }

        public IndicatorFeed(IndicatorStore<T> indicatorStore, 
            TimeSpan feedInterval, 
            TimeSpan intervalProjection,
            DateTimeOffset startTime)
        {
            this.indicatorStore = indicatorStore;
            this.indicatorFeed = new Subject<T>();

            FeedStartTime = startTime;
            CurrentFeedTimeStamp = FeedStartTime;

            Observable
                .Interval(feedInterval)
                .Subscribe(stamp =>
                {
                    CurrentFeedTimeStamp = CurrentFeedTimeStamp.Add(intervalProjection);
                    indicatorFeed.OnNext(indicatorStore.GetValue(CurrentFeedTimeStamp));
                });
        }



        public IDisposable Subscribe(IObserver<T> observer)
        {
            return indicatorFeed.Subscribe(observer);
        }
    }
}