using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using Carvers.IB.App.ViewModels;
using Carvers.IBApi;
using Carvers.Infra.ViewModels;
using Carvers.Models;

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
                .Subscribe(msg => Messages.Add($"{msg.Arg1}, {msg.Arg2},{msg.Arg3},{msg.Ex}"));

            ibtws.IbtwsMessageStream
                .ObserveOnDispatcher()
                .Subscribe(msg => Messages.Add(msg.Message));

            ibtws.IbtwsConnectionStateStream
                .Subscribe(msg => Messages.Add($"Connection State (Is Connected): {msg.IsConnected}"));

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
        }

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

            ibtws.AddHistoricalDataRequest(contract, endTime, duration, barSize, whatToShow, outsideRTH, 1, false);
        }

        //private void IbClient_HistoricalDataEnd(HistoricalDataEndMessage obj)
        //{
        //    if (obj != null)
        //        Messages.Add(obj.RequestId.ToString());
        //}

        //private void IbClient_HistoricalDataUpdate(HistoricalDataMessage obj)
        //{
        //    if (obj != null)
        //        Messages.Add(obj.RequestId.ToString());
        //}

        //private void IbClient_HistoricalData(HistoricalDataMessage obj)
        //{
        //    if(obj != null)
        //        Messages.Add(obj.RequestId.ToString());
        //}


        //private void OnError(int arg1, int arg2, string arg3, Exception arg4)
        //{
        //    Messages.Add($"{arg1}, {arg2}, {arg3}, {arg4}");
        //}

        //private void OnConnectionClosed()
        //{
        //    Messages.Add("Connection Closed");
        //}

        public ObservableCollection<RealTimeBarDataViewModel> RealTimeBarDataViewModels { get; }

        public ObservableCollection<string> Messages { get; }

        public ICommand RequestRealtimeDataCommand { get; }
        public ICommand RequestHistoricalDataCommand { get; }
        public ICommand PlaceOrderCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand ConnectCommand { get; }
    }
}