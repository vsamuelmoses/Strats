using System;
using System.Collections.Generic;
using IBApi;
using IBSampleApp.messages;

namespace Carvers.IBApi
{
    public interface IEngine
    {
        IObservable<RealTimeBarMessage> RealTimeBarStream { get; }
        IObservable<IBTWSMessage> IbtwsMessageStream { get; }
        IObservable<IBTWSErrorMessage> IbtwsErrorStream { get; }
        IObservable<IBTWSConnectionState> IbtwsConnectionStateStream { get; }

        bool IsConnected { get; }
        int NextOrderId { get; }
        bool Connect(string host, int port, int clientId);
        void Disconnect();
        void AddRealtimeDataRequests(IEnumerable<Tuple<int, Contract>> requests);
        void AddRealtimeDataRequest(int tickerId, Contract contract);
        void AddRealtimeDataRequest(int tickerId, Contract contract, string whatToShow, bool useRTH);
        void AddHistoricalDataRequest(Contract contract, string endDateTime, string durationString, string barSizeSetting, string whatToShow, int useRTH, int dateFormat, bool keepUpToDate);
        void PlaceOrder(Contract contract, Order order);
    }
}