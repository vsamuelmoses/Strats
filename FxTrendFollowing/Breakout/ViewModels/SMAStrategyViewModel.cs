using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using Carvers.Charting.ViewModels;
using Carvers.IB.App;
using Carvers.IBApi;
using Carvers.IBApi.Extensions;
using Carvers.Infra.ViewModels;
using Carvers.Models;
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

        public SMAStrategyViewModel()
        {

            Ibtws = new IBTWSSimulator(Utility.FxFilePathGetter,
                new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero));
                //new DateTimeOffset(2017, 01, 31, 0, 0, 0, TimeSpan.Zero));
            //Ibtws = new IBTWSSimulator((cxPair, dt) => Utility.FxIBDATAPathGetter(cxPair), new DateTimeOffset(2018, 04, 24, 0, 0, 0, TimeSpan.Zero));
            IbtwsViewModel = new IBTWSViewModel(Ibtws);

            var interestedPairs = new[] { CurrencyPair.EURUSD };

            _contextStream = new Subject<SMAContext>();
            
            Strategy = new Strategy("Simple Breakout");
            var context = new SMAContext(Strategy, 
                new MovingAverage("SMA 50", 50, 20), 
                new MovingAverage("SMA 100", 100, 180), 
                new MovingAverage("SMA 250", 250, 3), 
                new MovingAverage("SMA 500", 500, 3), 
                new MovingAverage("SMA 1000", 1000, 3), 
                new MovingAverage("SMA 3600", 3600, 3600), 
                new ExponentialMovingAverage("EMA 3600", 3600, 3),
                new ExponentialMovingAverage("EMA 50", 50, 10),
                new Lookback(180, new ConcurrentQueue<Candle>()), 
                new EmptyContext());

            var nextCondition = SMACrossOverStrategy.Strategy;

            var candleStream = Ibtws.RealTimeBarStream.Select(msg => MessageExtensions.ToCandle(msg, TimeSpan.FromMinutes(1)));

            //TODO: for manual feed, change the candle span
            Ibtws.RealTimeBarStream.Subscribe(msg =>
            {
                var candle = msg.ToCandle(TimeSpan.FromMinutes(1));
                Status = $"Executing {candle.TimeStamp}";
                (nextCondition, context) = nextCondition().Evaluate(context, candle);
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


            TraderViewModel.ConstructTraderViewModel(
                    candleStream.Zip(_contextStream,
                        (candle, ctx) => (candle,
                            (IEnumerable<(IIndicator, double)>) new List<(IIndicator, double)>()
                            {
                                (ctx.Sma50, ctx.Sma50.Current),
                                (ctx.ExMa3600, ctx.ExMa3600.Current),
                                (ctx.Sma250, ctx.Sma3600.Current - 0.00120),
                                (ctx.Sma100, ctx.Sma3600.Current + 0.00120),
                                (ctx.Sma3600, ctx.Sma3600.Current),
                                (ctx.ExMa50, ctx.ExMa50.Current)
                            })),
                    summaryReport.ProfitLossStream,
                    Strategy.OpenOrders
                        .Select(order => (IEvent) new OrderExecutedEvent(order.OrderInfo.TimeStamp, order))
                        .Merge(Strategy.CloseddOrders
                            .Select(order => (IEvent) new OrderExecutedEvent(order.OrderInfo.TimeStamp, order))))
                .ContinueWith(t => ChartVm = t.Result);
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