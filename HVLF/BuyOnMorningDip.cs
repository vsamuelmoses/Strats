using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Subjects;
using Carvers.Models;

namespace HVLF
{
    public class BuyOnMorningDip : IStrategy
    {
        private readonly Func<DateTime, DailyScannerUniverse> universeGenerator;
        private readonly ConcurrentBag<IClosedOrder> closedOrders;
        private readonly Subject<IOrder> openOrders;
        private readonly Subject<IOrder> closeddOrders;
        private IOrder openOrder;

        public IObservable<IOrder> OpenOrders => openOrders;
        public IObservable<IOrder> CloseddOrders => closeddOrders;
        public IEnumerable<IClosedOrder> ClosedOrders => closedOrders;

        private Tuple<DateTime, DailyScannerUniverse> universe;

        public BuyOnMorningDip(Func<DateTime, DailyScannerUniverse> universeGenerator, BuyOnMorningDipOptions options)
        {
            this.universeGenerator = universeGenerator;

            openOrders = new Subject<IOrder>();
            closeddOrders = new Subject<IOrder>();
            closedOrders = new ConcurrentBag<IClosedOrder>();
        }


        public void Execute(DateTimeOffset dateTimeOffset)
        {
            if (universe == null || universe.Item1.Date != dateTimeOffset.Date)
            {
                universe = new Tuple<DateTime, DailyScannerUniverse>(dateTimeOffset.Date,
                    universeGenerator(dateTimeOffset.Date));
                universe.Item2.Initialise.Wait();
            }

            if (openOrder != null)
            {
                if (dateTimeOffset - openOrder.OrderInfo.TimeStamp > TimeSpan.FromHours(1))
                {
                    var openStock = universe.Item2.Stocks.Single(stock => stock.Symbol == openOrder.OrderInfo.Symbol);
                    var closedOrder = ((BuyOrder) openOrder).Close(new OrderInfo(dateTimeOffset, openOrder.OrderInfo.Symbol, this,
                        openStock.Candles[dateTimeOffset].Ohlc.Open));

                    closedOrders.Add(closedOrder);
                    closeddOrders.OnNext(closedOrder);
                }
            }

            var interestedSymbols = 
                universe.Item2
                .SymbolsAtTimeStamp.Where(kvp => kvp.Key < dateTimeOffset)
                .Select(kvp => kvp.Value)
                .SelectMany(item => item);

            var toTradeStock =
                interestedSymbols
                    .Select(symbol => universe.Item2.Stocks.Single(stock => stock.Symbol == symbol))
                    .First();
                //.FirstOrDefault(stockData => stockData.Candles.Values.PercentageDifferenceInDailyVolume(dateTimeOffset) > 500);

            if (toTradeStock == null)
                return;

            openOrder = new BuyOrder(new OrderInfo(dateTimeOffset, toTradeStock.Symbol, this, toTradeStock.Candles[dateTimeOffset].Ohlc.Open));
            openOrders.OnNext(openOrder);
        }

        public StockData Data => null;

        public void Stop()
        {
            if (openOrder != null)
                Debug.WriteLine(openOrder.OrderInfo.ToCsv());

            openOrders.OnCompleted();
            closeddOrders.OnCompleted();
        }

        public Price ProfitLoss => ClosedOrders.ProfitLoss();

    }
}