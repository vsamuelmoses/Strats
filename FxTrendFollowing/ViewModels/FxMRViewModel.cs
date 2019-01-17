using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Windows.Input;
using System.Windows.Threading;
using Carvers.IB.App;
using Carvers.IBApi;
using Carvers.IBApi.Extensions;
using Carvers.Infra.ViewModels;
using Carvers.Models;
using Carvers.Infra.Extensions;
using Carvers.Models.Extensions;
using Carvers.Utilities;

namespace FxTrendFollowing.ViewModels
{
    public class FxMRViewModel : ViewModel, IStrategy
    {
        public IEngine Ibtws { get; }
        public IBTWSViewModel IbtwsViewModel { get; }

        public IEnumerable<HourlyCurrencyPairData> CurrencyPairDataVMs { get; }
        public CSIFeedViewModel CsiFeedViewModel { get; }
        public TrendFollowingOptions options;

        private readonly ConcurrentBag<IClosedOrder> closedOrders;
        private readonly Subject<IOrder> openOrders;
        private readonly Subject<IOrder> closeddOrders;

        public FxMRViewModel()
        {
            options = new TrendFollowingOptions(
                lookbackPeriod: TimeSpan.FromHours(3), 
                groupByCount:1, 
                holdPeriod: TimeSpan.FromHours(2), 
                candleFeedInterval:TimeSpan.FromMinutes(1), 
                shouldCacheCandleFeed:false);

            //Live
            //options = new TrendFollowingOptions(
            //    lookbackPeriod: TimeSpan.FromHours(3),
            //    groupByCount: 1,
            //    holdPeriod: TimeSpan.FromHours(2),
            //    candleFeedInterval: TimeSpan.FromSeconds(5),
            //    shouldCacheCandleFeed: true);


            var lastLookbackPeriodCandles = CurrencyPair.All()
                 .Select(pair => new KeyValuePair<CurrencyPair, IEnumerable<Candle>>(pair, Enumerable.Empty<Candle>()))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);


            CurrencyPairDataVMs = CurrencyPair.All()
                .Select(pair => new HourlyCurrencyPairData(pair, lastLookbackPeriodCandles[pair],  
                    GlobalPaths.FxHistoricalData, options.ShouldCacheCandleFeed, (int)(options.LookbackPeriod.TotalSeconds / options.CandleFeedInterval.TotalSeconds)))
                .ToList();

            CsiFeedViewModel = new CSIFeedViewModel(CurrencyPairDataVMs);

            var logger = new CSLogger(Paths.CSFileGetter, Paths.FxStrenghtsAll);

            AllCurrencyStrength.CurrencyStrengthStream.Subscribe(val => Execute(val));

            //Ibtws = new IBTWS();
            Ibtws = new IBTWSSimulator(Utility.SymbolFilePathGetter, new DateTimeOffset(2017,01,01,0,0,0, TimeSpan.Zero));
            //Ibtws = new IBTWSSimulator((cxPair, dt) => Utility.FxIBDATAPathGetter(cxPair), new DateTimeOffset(2018, 04, 24, 0, 0, 0, TimeSpan.Zero));
            IbtwsViewModel = new IBTWSViewModel(Ibtws);


            //TODO: for manual feed, change the candle span
            Ibtws.RealTimeBarStream.Subscribe(msg =>
            {
                CurrencyPairDataVMs.Foreach(pairVm => pairVm.Add(msg, options.CandleFeedInterval));
                TryCloseOpenOrder(msg.Timestamp.UnixEpochToLocalTime());

            });

            StartCommand = new RelayCommand(_ =>
            {
                Ibtws.AddRealtimeDataRequests(CurrencyPairDataVMs
                    .Select(pairVm =>
                        Tuple.Create(pairVm.Pair.UniqueId, ContractCreator.GetContract(pairVm.Pair)))
                    .ToList());
            });


            StopCommand = new RelayCommand(_ => Stop());

            openOrders = new Subject<IOrder>();
            closeddOrders = new Subject<IOrder>();
            closedOrders = new ConcurrentBag<IClosedOrder>();


            var logReport = new StrategyLogReport(new[] { this }, logName: $"FxMr");
            var chartReport = new StrategyChartReport(new[] { this }, Dispatcher.CurrentDispatcher);
            var summaryReport = new StrategySummaryReport(new[] { this });
            Reporters = new Carvers.Infra.ViewModels.Reporters(logReport, chartReport, summaryReport);
        }

        public Carvers.Infra.ViewModels.Reporters Reporters { get; }

        private IOrder openOrder;
        private string status;
        public IObservable<IOrder> OpenOrders => openOrders;
        public IObservable<IOrder> CloseddOrders => closeddOrders;
        public IEnumerable<IClosedOrder> ClosedOrders => closedOrders;
        public StockData Data => null;

        public Price ProfitLoss => ClosedOrders.ProfitLoss();

        public void Stop()
        {
            if (openOrder != null)
                Debug.WriteLine(openOrder.OrderInfo.ToCsv());

            openOrders.OnCompleted();
            closeddOrders.OnCompleted();
        }


        private void TryCloseOpenOrder(DateTimeOffset dateTimeOffset)
        {
            if (openOrder != null)
            {
                var data = CurrencyPairDataVMs.Single(pairVm => Equals(pairVm.Pair, openOrder.OrderInfo.Symbol));

                var openTime = openOrder.OrderInfo.TimeStamp;


                double currentMovement = 0;

                //var currentCandle = data.HourlyCandles[dateTimeOffset.Subtract(TimeSpan.FromHours(1))];
                var currentCandle = data.LatestCandle;

                currentMovement = Math.Abs(currentCandle.Ohlc.Close.Value - openOrder.OrderInfo.Price.Value);
                //currentMovement = currentCandle.Ohlc.Close.Value - openOrder.OrderInfo.Price.Value;

                //if (dateTimeOffset - openOrder.OrderInfo.TimeStamp >= options.HoldPeriod
                //        || currentMovement <= -1 * target)

                if (currentMovement > target)
                {
                    IClosedOrder closedOrder;
                    if (openOrder is BuyOrder buyOrder)
                    {
                        closedOrder = buyOrder.Close(new OrderInfo(dateTimeOffset, data.Pair, this, currentCandle.Ohlc.Close), CsiFeedViewModel.GetExchangeRate);
                        Ibtws.PlaceOrder(ContractCreator.GetContract(data.Pair), OrderCreator.GetOrder(Ibtws.NextOrderId, "SELL", "100000", "MKT", "", "DAY"));
                    }
                    else
                    {
                        var shortSellOrder = openOrder as ShortSellOrder;
                        closedOrder = shortSellOrder.Close(new OrderInfo(dateTimeOffset, data.Pair, this, currentCandle.Ohlc.Close), CsiFeedViewModel.GetExchangeRate);
                        Ibtws.PlaceOrder(ContractCreator.GetContract(data.Pair), OrderCreator.GetOrder(Ibtws.NextOrderId, "BUY", "100000", "MKT", "", "DAY"));
                    }

                    target = null;
                    openOrder = null;
                    closedOrders.Add(closedOrder);
                    closeddOrders.OnNext(closedOrder);

                    return;
                }

                return;
            }
        }

        private double? target;


        public void Execute(AllCurrencyStrength acs)
        {
            var dateTimeOffset = acs.TimeStamp;
            Status = $"Executing {dateTimeOffset}";

            if (openOrder != null)
            {
                TryCloseOpenOrder(dateTimeOffset);
                return;
            }

            if (dateTimeOffset < new DateTimeOffset(2016, 1, 4, 0, 0, 0, TimeSpan.Zero))
                return;


            if (dateTimeOffset.DayOfWeek == DayOfWeek.Friday || dateTimeOffset.DayOfWeek == DayOfWeek.Monday)
                return;

            var strong = new List<Tuple<Currency, double>>();
            var weak = new List<Tuple<Currency, double>>();
            var strength = new List<double>();
            foreach (var curr in acs.IndividualStrengths)
            {

                var numberOfLBHours = (int) options.LookbackPeriod.TotalHours;
                strength =
                    SingleCurrencyStrength
                        .PastStrength[curr.Key]
                        .TakeWhile(s => s.TimeStamp <= dateTimeOffset)
                        .TakeLastInReverse(numberOfLBHours * options.GroupByCount)
                        .Select(kvp => kvp.AverageValue)
                        .GroupBy(options.GroupByCount)
                        .Select(grp => grp.Average())
                        .ToList();


                if (strength.Count < numberOfLBHours)
                    return;

                // the strength collection will have values in the reverse order
                strength = strength.Take(numberOfLBHours).ToList();


                var dipped = true;

                // using IsDescending because, we used TakeLast in retrieving the strength, 
                // which will return the last elements in reverse order
                //if (strength.IsInDescending() && dipped)

                if (strength.Aggregate((true, double.MaxValue), (isInc, val) => (isInc.Item1 && ((val) <= isInc.Item2 / 2), val)).Item1)
                {
                    strong.Add(Tuple.Create(curr.Key, strength.Average()));
                }

                //if (strength.IsInAscending())

                if (strength.Take(2).TakeLastInReverse(2).Aggregate((true, double.MaxValue), (isInc, val) => (isInc.Item1 && ((val) <= isInc.Item2 / 2), val)).Item1)
                {
                    weak.Add(Tuple.Create(curr.Key, strength.Average()));
                }
            }

            if (strong.Any() || weak.Any())
            {
                //var currentStrongest = CsiFeedViewModel.AllCurrencyStrength.IndividualStrengths.OrderByDescending(kvp => kvp.Value.AverageValue).First();
                //var currentWeakest = CsiFeedViewModel.AllCurrencyStrength.IndividualStrengths.OrderByDescending(kvp => kvp.Value.AverageValue).Last();


                var strongestTup = strong.OrderByDescending(val => val.Item2).FirstOrDefault();
                var weakestTup = weak.OrderByDescending(val => val.Item2).LastOrDefault();


                if (strongestTup == null)
                {
                    var currentStrongest = acs.IndividualStrengths.OrderByDescending(kvp => kvp.Value.AverageValue).First();
                    strongestTup = Tuple.Create(currentStrongest.Key, currentStrongest.Value.AverageValue);
                }

                if (weakestTup == null)
                {
                    var currentWeakest = acs.IndividualStrengths.OrderByDescending(kvp => kvp.Value.AverageValue).Last();
                    weakestTup = Tuple.Create(currentWeakest.Key, currentWeakest.Value.AverageValue);
                }


                if (strongestTup.Item2 < weakestTup.Item2)
                    return;

                var strongest = strongestTup.Item1;
                var weakest = weakestTup.Item1;

                //if (strongest != currentStrongest.Key || weakest != currentWeakest.Key)
                //    return;

                var buy = CurrencyPair.Contains(strongest, weakest);

                CurrencyPair pair;
                pair = buy ? CurrencyPair.Get(strongest, weakest) : CurrencyPair.Get(weakest, strongest);

                var data = CurrencyPairDataVMs.Single(pairVm => Equals(pairVm.Pair, pair));

                if (!buy)
                {
                    var openPrice = data.Candles.SingleOrDefault(candle => candle.TimeStamp == dateTimeOffset).Ohlc.Open;

                    openOrder = new BuyOrder(new OrderInfo(dateTimeOffset, pair, this, openPrice, 100000));
                    Ibtws.PlaceOrder(ContractCreator.GetContract(pair), OrderCreator.GetOrder(Ibtws.NextOrderId, "BUY", "100000", "MKT", "", "DAY"));
                }
                else
                {
                    var openPrice = data.Candles.SingleOrDefault(candle => candle.TimeStamp == dateTimeOffset).Ohlc.Open;
                    openOrder = new ShortSellOrder(new OrderInfo(dateTimeOffset, pair, this, openPrice, 100000));
                    Ibtws.PlaceOrder(ContractCreator.GetContract(pair), OrderCreator.GetOrder(Ibtws.NextOrderId, "SELL", "100000", "MKT", "", "DAY"));
                }

                var pastCandle = data.Candles.
                    Where(candle =>
                        candle.TimeStamp <= openOrder.OrderInfo.TimeStamp
                        && candle.TimeStamp >= openOrder.OrderInfo.TimeStamp.Subtract(TimeSpan.FromHours(1)))
                    .ToSingleCandle(TimeSpan.FromHours(1));

                var averageHourlyMovement = pastCandle.CandleLength();
                target = averageHourlyMovement;//2;
                //target = averageHourlyMovement/2;

                openOrders.OnNext(openOrder);
            }




            //if (strong.Any() && weak.Any())
            //{
            //    //var currentStrongest = CsiFeedViewModel.AllCurrencyStrength.IndividualStrengths.OrderByDescending(kvp => kvp.Value.AverageValue).First();
            //    //var currentWeakest = CsiFeedViewModel.AllCurrencyStrength.IndividualStrengths.OrderByDescending(kvp => kvp.Value.AverageValue).Last();


            //    var strongestTup = strong.OrderByDescending(val => val.Item2).First();
            //    var weakestTup = weak.OrderByDescending(val => val.Item2).Last();


            //    if (strongestTup.Item2 < weakestTup.Item2)
            //        return;


            //    var strongest = strongestTup.Item1;
            //    var weakest = weakestTup.Item1;

            //    var snapshoptStrongest = CsiFeedViewModel.AllCurrencyStrength.IndividualStrengths
            //        .Select(kvp => kvp.Value)
            //        .OrderByDescending(s => s.AverageValue)
            //        .First().Currency;


            //    var snapshoptWeakest = CsiFeedViewModel.AllCurrencyStrength.IndividualStrengths
            //        .Select(kvp => kvp.Value)
            //        .OrderByDescending(s => s.AverageValue)
            //        .Last().Currency;

            //    //if (strongest == snapshoptStrongest)
            //    //    return;


            //    var weakest2 = CsiFeedViewModel.AllCurrencyStrength.IndividualStrengths
            //        .Where(kvp => kvp.Key != strongest)
            //        .Select(kcp => Tuple.Create(kcp.Key, kcp.Value.StrengthAgainstCurrency[strongest]))
            //        .OrderByDescending(tup => tup.Item2)
            //        .Last()
            //        .Item1;


            //    var weakest3 = CsiFeedViewModel.AllCurrencyStrength.IndividualStrengths
            //        .Select(kvp => kvp.Value)
            //        .Single(s => s.Currency == strongest)
            //        .StrengthAgainstCurrency
            //        .OrderByDescending(kvp => kvp.Value)
            //        .First().Key;

            //    //if (weakest1 == weakest)
            //    //    return;

            //    //if (weakest1 == weakest3)
            //    //    return;

            //    //var weakest = weakest3;


            //    //if (strongest != currentStrongest.Key || weakest != currentWeakest.Key)
            //    //    return;


            //    if (snapshoptWeakest != weakest)
            //        return;

            //    //weakest = weakest3;







            //    var buy = CurrencyPair.Contains(strongest, weakest);

            //    CurrencyPair pair;
            //    pair = buy ? CurrencyPair.Get(strongest, weakest) : CurrencyPair.Get(weakest, strongest);

            //    var data = CurrencyPairDataVMs.Single(pairVm => Equals(pairVm.Pair, pair));

            //    if (!buy)
            //    {

            //        //var allIsRed = data.HourlyCandles
            //        //    .Where(kvp => kvp.Key <= dateTimeOffset.Subtract(TimeSpan.FromHours(1)))
            //        //    .TakeLast(options.LookbackPeriod)
            //        //    .All(candle => candle.Value.IsRed());

            //        //if (allIsRed)
            //        //    return;


            //        openOrder = new BuyOrder(new OrderInfo(dateTimeOffset, pair, this, data.LatestCandle.Ohlc.Close, 100000));
            //        Ibtws.PlaceOrder(ContractCreator.GetCurrencyPairContract(pair), OrderCreator.GetOrder(Ibtws.NextOrderId, "BUY", "100000", "MKT", "", "DAY"));
            //    }
            //    else
            //    {
            //        //var allIsGreen = data.HourlyCandles
            //        //    .Where(kvp => kvp.Key <= dateTimeOffset.Subtract(TimeSpan.FromHours(1)))
            //        //    .TakeLast(options.LookbackPeriod)
            //        //    .All(candle => candle.Value.IsGreen());

            //        //if (allIsGreen)
            //        //    return;

            //        openOrder = new ShortSellOrder(new OrderInfo(dateTimeOffset, pair, this, data.LatestCandle.Ohlc.Close, 100000));
            //        Ibtws.PlaceOrder(ContractCreator.GetCurrencyPairContract(pair), OrderCreator.GetOrder(Ibtws.NextOrderId, "SELL", "100000", "MKT", "", "DAY"));
            //    }


            //    var pastCandle = data.Candles.
            //        Where(candle =>
            //            candle.TimeStamp <= openOrder.OrderInfo.TimeStamp
            //            && candle.TimeStamp >= openOrder.OrderInfo.TimeStamp.Subtract(TimeSpan.FromHours(options.LookbackPeriod)))
            //        .ToSingleCandle(TimeSpan.FromHours(options.LookbackPeriod));


            //    var averageHourlyMovement = pastCandle.CandleLength() / options.LookbackPeriod;

            //    target = averageHourlyMovement * 4;




            //    openOrders.OnNext(openOrder);
            //}
        }

        public void Execute(DateTimeOffset dateTimeOffset)
        {
        }

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }

        public string Status
        {
            get => status;
            private set
            {
                status = value; OnPropertyChanged();
            }
        }
    }

    public static class Paths
    {
        public const string FxStrenghtsAll = @"../../Logs/FxStrengthsAll.csv";

        public static string CSFileGetter(Currency currency)
        {
            return Path.Combine(Paths.FxStrenghtsAll, $"{currency.Symbol}.csv");
        }
    }
}

