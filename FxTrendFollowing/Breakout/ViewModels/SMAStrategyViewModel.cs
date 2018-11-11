using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Input;
using System.Windows.Threading;
using Carvers.Charting.MultiPane;
using Carvers.Charting.ViewModels;
using Carvers.IB.App;
using Carvers.IBApi;
using Carvers.IBApi.Extensions;
using Carvers.Infra.ViewModels;
using Carvers.Models;
using Carvers.Models.DataReaders;
using Carvers.Models.Events;
using Carvers.Models.Indicators;
using IBApi;

namespace FxTrendFollowing.Breakout.ViewModels
{
    public class SMAStrategyViewModel : ViewModel
    {
        private string _status;
        private Subject<SMAContext> _contextStream;
        private TraderViewModel _chartVm;
        private MultiTraderViewModel _multiTraderVm;
        private CreateMultiPaneStockChartsViewModel _traderChart;

        public SMAStrategyViewModel()
        {

            Ibtws = new IBTWSSimulator(Utility.FxFilePathGetter,
                new DateTimeOffset(2017, 01, 15, 0, 0, 0, TimeSpan.Zero));
                //new DateTimeOffset(2017, 07, 30, 0, 0, 0, TimeSpan.Zero));
                //new DateTimeOffset(2017, 01, 31, 0, 0, 0, TimeSpan.Zero));
            //Ibtws = new IBTWSSimulator((cxPair, dt) => Utility.FxIBDATAPathGetter(cxPair), new DateTimeOffset(2018, 04, 24, 0, 0, 0, TimeSpan.Zero));
            IbtwsViewModel = new IBTWSViewModel(Ibtws);

            var interestedPairs = new[] { CurrencyPair.GBPUSD };

            _contextStream = new Subject<SMAContext>();
            
            Strategy = new Strategy("Simple Breakout");

            var nextCondition = SMACrossOverStrategy.Strategy;

            var minuteFeed = Ibtws.RealTimeBarStream.Select(msg => MessageExtensions.ToCandle(msg, TimeSpan.FromMinutes(1)));
            var candleFeed = new AggreagateCandleFeed(minuteFeed, TimeSpan.FromHours(1)).Stream;

            var maStreaming = new MovingAverageStreamingService(Indicators.CloseSma5, candleFeed, candle => candle.Close, 5);
            var ma14Streaming = new MovingAverageStreamingService(Indicators.CloseSma14, candleFeed, candle => candle.Close, 14);
            var candleBodyLengthMaStreaming = new MovingAverageStreamingService(Indicators.CandleBodySma5, candleFeed, candle => Math.Abs(candle.Close - candle.Open), 5);

            var context = new SMAContext(Strategy, new List<MovingAverage>
            {
                maStreaming.MovingAverage,
                candleBodyLengthMaStreaming.MovingAverage
            }, Candle.Null);

            //TODO: for manual feed, change the candle span
            candleFeed.Zip(
                    maStreaming.Stream, 
                    ma14Streaming.Stream, 
                    candleBodyLengthMaStreaming.Stream, 
                    Tuple.Create)
                .Subscribe(tup =>
                {
                    var candle = tup.Item1;
                    Status = $"Executing {candle.TimeStamp}";
                    var newcontext = new SMAContext(context.Strategy, new List<MovingAverage> { tup.Item2, tup.Item3, tup.Item4}, candle);
                    (nextCondition, context) = nextCondition().Evaluate(newcontext, candle);
                    _contextStream.OnNext(context);
                });

            StartCommand = new RelayCommand(_ =>
            {
                Ibtws.AddRealtimeDataRequests(interestedPairs
                    .Select(pair => Tuple.Create<int, Contract>(pair.UniqueId, ContractCreator.GetCurrencyPairContract(pair)))
                    .ToList());
            });

            StopCommand = new RelayCommand(_ => { Strategy.Stop(); });


            var logReport = new StrategyLogReport(new[] { Strategy }, logName: "MoBo");
            var chartReport = new StrategyChartReport(new[] { Strategy }, Dispatcher.CurrentDispatcher);
            var summaryReport = new StrategySummaryReport(new[] { Strategy });
            Reporters = new Carvers.Infra.ViewModels.Reporters(logReport, chartReport, summaryReport);

            var eventsFeed = Strategy.OpenOrders
                .Select(order => (IEvent) new OrderExecutedEvent(order.OrderInfo.TimeStamp, order))
                .Merge(Strategy.CloseddOrders
                    .Select(order => (IEvent) new OrderExecutedEvent(order.OrderInfo.TimeStamp, order)));

            var priceChart = candleFeed.Zip(_contextStream,
                (candle, ctx) => (candle,
                    ctx.Indicators
                        .Where(i => i.Key != Indicators.CandleBodySma5)
                        .Select(i => i.Value)
                        .OfType<MovingAverage>()
                        .Select(i => ((IIndicator)i, i.Value))));

           TraderChart = new CreateMultiPaneStockChartsViewModel(priceChart, 
                new List<Dictionary<IIndicator, IObservable<(IIndicator, DateTime, double)>>>() { }, eventsFeed);
        }

        public CreateMultiPaneStockChartsViewModel TraderChart
        {
            get => _traderChart;
            set
            {
                _traderChart = value; 
                OnPropertyChanged();
            }
        }

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public IBTWSSimulator Ibtws { get; }
        public IBTWSViewModel IbtwsViewModel { get; }
        public Strategy Strategy { get; }

        public string Status
        {
            get { return _status; }
            private set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public Carvers.Infra.ViewModels.Reporters Reporters { get; }

        public TraderViewModel ChartVm
        {
            get
            {
                return _chartVm;
            }

            private set
            {
                _chartVm = value;
                OnPropertyChanged();
            }
        }
    }
}