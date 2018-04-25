using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using Carvers.Infra.Extensions;
using Carvers.Models;
using Carvers.Models.Extensions;
using System.Diagnostics;

namespace SykesStrategies.ViewModels.Strategies
{
    public class SellOnMax : IStrategy
    {
        private Candle currentMaxInLB;
        public SellOnMax(
            StockData data, 
            SellOnDayMaxStrategyOptions stratOptions)
        {
            Data = data;
            StratOptions = stratOptions;
            closedOrders = new List<IClosedOrder>();
            openOrders = new Subject<IOrder>();
            closeddOrders = new Subject<IOrder>();
        }

        // return 16.8801
        //public void Execute(DateTimeOffset dateTimeOffset)
        //{
        //    Candle thisCandle;
        //    if(!Data.Candles.TryGetValue(dateTimeOffset, out thisCandle))
        //        return;

        //    var last30dayMax = day30Max;
        //    day30Max = day30Max != Candle.Null
        //           ? day30Max = new[] { day30Max, thisCandle }.MaxClose()
        //           : Data.MaxClose(dateTimeOffset, StratOptions.Lookback);

        //    if (last30dayMax == null)
        //        return;

        //    if (openOrder != null)
        //    {
        //        var closedOrder = openOrder.Close(new SellOnMaxOrderInfo(dateTimeOffset,Data.Symbol,this,thisCandle,thisCandle.Ohlc.Close));
        //        //Debug.WriteLine(
        //        //    $"{dateTimeOffset},{Data.Symbol},{openOrder.Description},{openOrder.Price},{closedOrder.Price},{closedOrder.ProfitLoss}");

        //        closedOrders.Add(closedOrder);
        //        closeddOrders.OnNext(closedOrder);
        //        openOrder = null;
        //        return;
        //    }

        //    if (openOrder == null)
        //    {
        //        if (thisCandle.Ohlc.Close.IsInRange(StratOptions.Range)
        //            && Data.Candles.Any(cand =>  cand.Key >= dateTimeOffset.Subtract(TimeSpan.FromDays(StratOptions.Lookback)) && cand.Key <= dateTimeOffset)
        //            && thisCandle.IsMaxIn(can => can.Ohlc.Close, Data.Candles.Values.Between(dateTimeOffset.Subtract(TimeSpan.FromDays(StratOptions.Lookback)), dateTimeOffset))
        //            && Data.Candles.ContainsKey(dateTimeOffset.Subtract(TimeSpan.FromDays(1)))
        //            && thisCandle.PercentageChangeIn(can => can.Ohlc.Close.Value, last30dayMax).IsInRange(StratOptions.PercentageChange))
        //           // && thisCandle.Ohlc.Volume.IsInRange(500000,1500000))
        //        {
        //            openOrder = new ShortSellOrder(new SellOnMaxOrderInfo(dateTimeOffset, Data.Symbol, this, thisCandle, thisCandle.Ohlc.Close));
        //            openOrders.OnNext(openOrder);
        //        }
        //    }
        //}

        public void Execute(DateTimeOffset dateTimeOffset)
        {
            if (dateTimeOffset.Date == new DateTime(2007, 02, 12))
                Console.WriteLine("here");


            Candle thisCandle;
            if (!Data.Candles.TryGetValue(dateTimeOffset, out thisCandle))
                return;

            var lookbackDaysAgo = dateTimeOffset.Subtract(TimeSpan.FromDays(StratOptions.Lookback));
            var previousMaxInLB = currentMaxInLB;
            currentMaxInLB = Data.MaxClose(dateTimeOffset, StratOptions.Lookback);

            if (previousMaxInLB == null)
                return;

            if (shouldOpen && openOrder == null)
            {
                if (thisCandle.Ohlc.Open.IsInRange(StratOptions.Range))
                {
                    if (thisCandle.Ohlc.High >= (thisCandle.Ohlc.Open + 0.30d.USD()))
                    {
                        openOrder = new ShortSellOrder(new SellOnMaxOrderInfo(info, dateTimeOffset, Data.Symbol, this, thisCandle, thisCandle.Ohlc.Open + 0.3.USD()));
                        openOrders.OnNext(openOrder);
                    }
                }
                
                shouldOpen = false;
            }

            if (openOrder != null)
            {
                var closedOrder = openOrder.Close(new SellOnMaxOrderInfo(dateTimeOffset, Data.Symbol, this, thisCandle, thisCandle.Ohlc.Close));
                closedOrders.Add(closedOrder);
                closeddOrders.OnNext(closedOrder);
                openOrder = null;
                info = null;
                return;
            }

            if (openOrder == null)
            {
                if (thisCandle.Ohlc.Close.IsInRange(StratOptions.Range)
                    && thisCandle.Ohlc.Volume > 500000
                    && Data.Candles.Any(cand => cand.Key >= dateTimeOffset.Subtract(TimeSpan.FromDays(StratOptions.Lookback)) && cand.Key <= dateTimeOffset)
                    && thisCandle.IsMaxIn(can => can.Ohlc.Close, Data.Candles.Values.Between(lookbackDaysAgo, dateTimeOffset))
                    //&& Data.Candles.ContainsKey(dateTimeOffset.Subtract(TimeSpan.FromDays(1)))
                    && thisCandle.PercentageChangeIn(can => can.Ohlc.Close.Value, previousMaxInLB).IsInRange(StratOptions.PercentageChange)
                    && thisCandle.TimeStamp - previousMaxInLB.TimeStamp >= StratOptions.SpanBetweenMax)
                    //&& thisCandle.Ohlc.High.Value.PercentageRise(thisCandle.Ohlc.Open.Value) < 20)
                // && thisCandle.Ohlc.Volume.IsInRange(500000,1500000))
                {
                    //openOrder = new ShortSellOrder(new SellOnMaxOrderInfo(dateTimeOffset, Data.Symbol, this, thisCandle, thisCandle.Ohlc.Close));
                    //openOrders.OnNext(openOrder);
                    info = new StrategyDecisionInfo(Data.Symbol, previousMaxInLB, Data.CandleBefore(thisCandle), thisCandle);
                    shouldOpen = true;
                }
            }
        }



        public void Stop()
        {
            if (openOrder != null)
                Debug.WriteLine(openOrder.OrderInfo.ToCsv());

            openOrders.OnCompleted();
            closeddOrders.OnCompleted();
        }

        public Price ProfitLoss => ClosedOrders.ProfitLoss();

        private readonly List<IClosedOrder> closedOrders;

        private ShortSellOrder openOrder;
        private readonly Subject<IOrder> openOrders;
        private readonly Subject<IOrder> closeddOrders;
        private bool shouldOpen;
        private StrategyDecisionInfo info;

        public IObservable<IOrder> OpenOrders => openOrders;
        public IObservable<IOrder> CloseddOrders => closeddOrders;

        public IEnumerable<IClosedOrder> ClosedOrders => closedOrders;

        public StockData Data { get; }
        public SellOnDayMaxStrategyOptions StratOptions { get; }
    }



}