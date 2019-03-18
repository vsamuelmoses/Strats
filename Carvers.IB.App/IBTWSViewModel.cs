using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Carvers.IB.App.ViewModels;
using Carvers.IBApi;
using Carvers.IBApi.Extensions;
using Carvers.Infra;
using Carvers.Infra.ViewModels;
using Carvers.Models;
using Carvers.Models.DataReaders;
using Carvers.Models.Indicators;
using Carvers.Utilities;

namespace Carvers.IB.App
{
    public class IBTWSViewModel : ViewModel
    {
        public int OrderId { get; private set; }

        private readonly IEngine ibtws;
        public List<CurrencyPair> CurrencyPairs { get; }

        public IBTWSViewModel(IEngine ibtws)
        {
            this.ibtws = ibtws;
            RealTimeBarDataViewModels = new ObservableCollection<RealTimeBarDataViewModel>();

            //ibtws.RealTimeBarStream
            //        .ObserveOnDispatcher()
            //        .Where(msg => RealTimeBarDataViewModels.All(vm => vm.RequestId != msg.RequestId))
            //        .Subscribe(msg =>
            //        {
            //            var pair = CurrencyPairs.Single(p=> p.UniqueId == msg.RequestId - IBTWS.RT_BARS_ID_BASE);
            //            RealTimeBarDataViewModels.Add(new RealTimeBarDataViewModel(ibtws, pair, msg.RequestId, TimeSpan.FromSeconds(5)));
            //        });

            ibtws.IbtwsErrorStream
                .Subscribe(msg => AddMessage($"{msg.Arg1}, {msg.Arg2},{msg.Arg3},{msg.Ex}"));

            ibtws.IbtwsMessageStream
                .ObserveOnDispatcher()
                .Subscribe(msg => AddMessage(msg.Message));

            ibtws.IbtwsConnectionStateStream
                .Subscribe(msg => AddMessage($"Connection State (Is Connected): {msg.IsConnected}"));

            CurrencyPairs = CurrencyPair.All();

            Messages = new ObservableCollection<string>();

            ConnectCommand = new RelayCommand(_ => ibtws.Connect(IBTWS.LocalHost, IBTWS.IBPaperTradingPort, 2));

            RequestHistoricalDataCommand = new RelayCommand(
                _ => SendHistoricalDataRequest(),
                _ => ibtws.IsConnected);

            DisconnectCommand = new RelayCommand(
                _ => ibtws.Disconnect(),
                _ => ibtws.IsConnected);

            RequestRealtimeDataCommand = new RelayCommand(
                pair => SendRealTimeDataRequest((CurrencyPair)pair),
                _ => ibtws.IsConnected);

            PlaceOrderCommand = new RelayCommand(
                _ => PlaceOrder(),
                _ => ibtws.IsConnected);

            DownloadFxDailyCandlesCmd = new RelayCommand(
                _ =>
                {
                    Download1DayCandles(new ConcurrentQueue<Symbol>(CurrencyPair.All()));
                    //Download1DayCandles(new ConcurrentQueue<Symbol>(new List<Symbol>() {CurrencyPair.AUDNZD}));
                }, 
                _ => ibtws.IsConnected);

            CreateDailyShadowCandlesCmd = new RelayCommand(
                _ =>
                {
                    ComputeShadowCandles(new ConcurrentQueue<Symbol>(CurrencyPair.All()));
                },
                _ => ibtws.IsConnected);
        }

        private void AddMessage(string message)
            => Messages.Add($"{DateTime.Now} {message}");

        private void PlaceOrder()
        {
            var contract = ContractCreator.GetContract(CurrencyPair.EURJPY);
            var order = OrderCreator.GetOrder(ibtws.NextOrderId, "BUY", "100000", "MKT", "", "DAY");
            ibtws.PlaceOrder(contract, order);
        }

        private void SendRealTimeDataRequest(Symbol pair)
        {
            var contract = ContractCreator.GetContract(pair);
            ibtws.AddRealtimeDataRequest(pair.UniqueId, contract, "MIDPOINT", false);
        }

        private void SendHistoricalDataRequest()
        {
            var contract = ContractCreator.GetContract(CurrencyPair.EURJPY);
            var endTime = DateTime.Now.ToString("yyyyMMdd HH:mm:ss");
            var duration = "10 D";
            var barSize = "1 hour";
            var whatToShow = "MIDPOINT";
            var outsideRTH = 0;

            ibtws.AddHistoricalDataRequest(CurrencyPair.EURJPY.UniqueId, contract, endTime, duration, barSize, whatToShow, outsideRTH, 1, false);
        }

        //private void IbClient_HistoricalDataEnd(HistoricalDataEndMessage obj)
        //{
        //    if (obj != null)
        //        AddMessage(obj.RequestId.ToString());
        //}

        //private void IbClient_HistoricalDataUpdate(HistoricalDataMessage obj)
        //{
        //    if (obj != null)
        //        AddMessage(obj.RequestId.ToString());
        //}

        //private void IbClient_HistoricalData(HistoricalDataMessage obj)
        //{
        //    if(obj != null)
        //        AddMessage(obj.RequestId.ToString());
        //}


        //private void OnError(int arg1, int arg2, string arg3, Exception arg4)
        //{
        //    AddMessage($"{arg1}, {arg2}, {arg3}, {arg4}");
        //}

        //private void OnConnectionClosed()
        //{
        //    AddMessage("Connection Closed");
        //}


        private void ComputeShadowCandles(ConcurrentQueue<Symbol> instruments)
        {
            Symbol instrument;
            if (!instruments.TryDequeue(out instrument))
            {
                AddMessage("Completed computing 1D Shadow candles for all instruments");
                return;
            }

            var shadowCandleFile = GlobalPaths.ShadowCandlesFor(instrument, "1D", liveData:true);

            if (!File.Exists(shadowCandleFile.FullName))
            {
                var directory = Path.GetDirectoryName(shadowCandleFile.FullName);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var stream = File.Create(shadowCandleFile.FullName);
                stream.Close();
            }

            var lastShadowCandle = CsvReader
                .ReadFile(shadowCandleFile, CsvToModelCreators.CsvToCarversShadowCandle, skip: 0)
                .ToList()
                .LastOrDefault();

            var lastShadowCandleDate = DateTime.MinValue;
            if (lastShadowCandle != null)
                lastShadowCandleDate = lastShadowCandle.TimeStamp.Date;

            var candleFile1D = GlobalPaths.CandleFileFor(instrument, "1D", liveData: true);

            var dailyCandles = CsvReader
                .ReadFile(candleFile1D, CsvToModelCreators.CsvToCarversDailyCandle, skip: 0)
                .Where(candle => candle.TimeStamp.Date > lastShadowCandleDate)
                .ToList();


            ShadowCandle shadowCandle = lastShadowCandle;
            foreach (var dailyCandle in dailyCandles)
            {
                if (shadowCandle == null || !(shadowCandle.Low < dailyCandle.Low && shadowCandle.High > dailyCandle.High))
                {
                    shadowCandle = new ShadowCandle(dailyCandle.Ohlc, dailyCandle.TimeStamp);
                    File.AppendAllLines(shadowCandleFile.FullName, new List<string>() { shadowCandle.ToCsv() });
                }
            }

            ComputeShadowCandles(instruments);
        }

        private void Download1DayCandles(ConcurrentQueue<Symbol> instruments)
        {
            Symbol instrument;
            if (!instruments.TryDequeue(out instrument))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => AddMessage("Completed Updating 1D candles for all instruments")));
                return; 
            }

            var candleFile1D = GlobalPaths.CandleFileFor(instrument, "1D", liveData: true);

            if (!File.Exists(candleFile1D.FullName))
            {
                var directory = Path.GetDirectoryName(candleFile1D.FullName);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var stream = File.Create(candleFile1D.FullName);
                stream.Close();
            }

            var lastCandle = CsvReader
                .ReadFile(candleFile1D, CsvToModelCreators.CsvToCarversDailyCandle, skip: 0)
                .ToList()
                .LastOrDefault();

            var lastCandleDate = lastCandle?.TimeStamp.Date ?? DateTimeUtility.UtcDateBefore(30.Days());

            var estTime = DateTime.UtcNow.ToEst();
            var incompleteDate = estTime.Date;

            if (estTime.Hour >= 17)
                incompleteDate = incompleteDate.AddDays(1);

            var completeDate = incompleteDate.Subtract(1.Days());

            if (lastCandleDate == completeDate)
            {
                Download1DayCandles(instruments);
                return;
            }

            //if (lastCandle != null && lastCandleDate == DateTimeUtility.YesterdayUtcDate.ToEst().Date)
            //{
            //    Download1DayCandles(instruments);
            //    return;
            //}

            var missingNumberOfDays = (incompleteDate - lastCandleDate).Days;


            //if (missingNumberOfDays == 0)
            //{
            //    Download1DayCandles(instruments);
            //    return;
            //}

            var contract = ContractCreator.GetContract(instrument);
            var endTime = DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + " GMT";
            var duration = $"{missingNumberOfDays} D";
            var barSize = "1 day";
            var whatToShow = "MIDPOINT";
            var outsideRTH = 0;

            ibtws.HistoricalDataStream
                .ObserveOn(Scheduler.Default)
                .Subscribe(histData =>
                {
                    if (histData.IsForCurrencyPair(instrument))
                    {


                        //var estTime = DateTime.UtcNow.ToEst();
                        //var incompleteDate = estTime.Date;

                        //if (estTime.Hour >= 17)
                        //    incompleteDate = incompleteDate.AddDays(1);

                        var historicalCandle = histData.ToDailyCandle();
                        if (historicalCandle.TimeStamp.Date > lastCandleDate &&
                            historicalCandle.TimeStamp.Date < incompleteDate) // we only want to update the 1D candles after the day is completed
                        {
                            File.AppendAllLines(candleFile1D.FullName, new List<string>() { historicalCandle.ToCsv() });
                        }
                    }
                }, e => { },
                    () =>
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => AddMessage($"Completed {instrument}")));
                        Download1DayCandles(instruments);
                    }, CancellationToken.None);

            ibtws.AddHistoricalDataRequest(instrument.UniqueId, contract, endTime, duration, barSize, whatToShow, outsideRTH, 2, false);
        }

        public ObservableCollection<RealTimeBarDataViewModel> RealTimeBarDataViewModels { get; }

        public ObservableCollection<string> Messages { get; }

        public ICommand RequestRealtimeDataCommand { get; }
        public ICommand RequestHistoricalDataCommand { get; }
        public ICommand PlaceOrderCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand ConnectCommand { get; }

        public ICommand CreateDailyShadowCandlesCmd { get; }
        public ICommand DownloadFxDailyCandlesCmd { get; }
    }
}