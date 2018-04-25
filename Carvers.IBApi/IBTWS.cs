using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading;
using Carvers.Infra.Extensions;
using IBApi;
using IBSampleApp.messages;

namespace Carvers.IBApi
{
    public class IBTWS : IEngine
    {
        private readonly Subject<RealTimeBarMessage> realTimeBarMsgSubject;
        public IObservable<RealTimeBarMessage> RealTimeBarStream => realTimeBarMsgSubject;

        private readonly Subject<IBTWSMessage> ibtwsMessageSubject;
        public IObservable<IBTWSMessage> IbtwsMessageStream => ibtwsMessageSubject;


        private readonly Subject<IBTWSErrorMessage> ibtwsErrorSubject;
        public IObservable<IBTWSErrorMessage> IbtwsErrorStream => ibtwsErrorSubject;

        private readonly Subject<IBTWSConnectionState> ibtwsConnectionStateSubject;
        public IObservable<IBTWSConnectionState> IbtwsConnectionStateStream => ibtwsConnectionStateSubject;

        public static readonly string LocalHost = "127.0.0.1";
        public static readonly int IBPaperTradingPort = 7496;
        public static readonly int ClientId = 1;
        public const int HISTORICAL_ID_BASE = 30000000;
        public const int RT_BARS_ID_BASE = 40000000;

        private readonly IBClient ibClient;
        private readonly EReaderMonitorSignal signal;

        public IBTWS()
        {
            signal = new EReaderMonitorSignal();
            ibClient = new IBClient(signal);

            ibtwsMessageSubject = new Subject<IBTWSMessage>();

            realTimeBarMsgSubject = new Subject<RealTimeBarMessage>();
            ibClient.RealtimeBar += message => realTimeBarMsgSubject.OnNext(message);

            ibtwsErrorSubject = new Subject<IBTWSErrorMessage>();
            ibClient.Error += (a1, a2, a3, e) => ibtwsErrorSubject.OnNext(new IBTWSErrorMessage(a1, a2, a3, e));

            ibtwsConnectionStateSubject = new Subject<IBTWSConnectionState>();
            ibClient.ConnectionClosed += () => ibtwsConnectionStateSubject.OnNext(new IBTWSConnectionState(false));
        }

        public bool IsConnected => ibClient.ClientSocket.IsConnected();


        private int? nextOrderId;

        public int NextOrderId
        {
            get
            {
                if (nextOrderId.HasValue)
                    return nextOrderId.Value;

                if(ibClient.NextOrderId == 0)
                    throw new Exception("not sure what the next order id should be");

                nextOrderId = ibClient.NextOrderId;

                return nextOrderId.Value;
            }
            private set
            {
                if (nextOrderId.HasValue)
                    nextOrderId = value;
            }   
        }

        public bool Connect(string host, int port, int clientId)
        {
            if (host == null || host.Equals(""))
                host = "127.0.0.1";
            ibClient.ClientId = clientId;
            ibClient.ClientSocket.eConnect(host, port, ibClient.ClientId);

            var reader = new EReader(ibClient.ClientSocket, signal);
            reader.Start();

            new Thread(() =>
            {
                while (ibClient.ClientSocket.IsConnected())
                {
                    signal.waitForSignal();
                    reader.processMsgs();
                }
            })
            { IsBackground = true }.Start();

            var isConnected = ibClient.ClientSocket.IsConnected();
            ibtwsConnectionStateSubject.OnNext(new IBTWSConnectionState(isConnected));
            

            return isConnected;
        }

        public void Disconnect()
        {
            if(ibClient.ClientSocket.IsConnected())
                ibClient.ClientSocket.eDisconnect();
        }

        public void AddRealtimeDataRequests(IEnumerable<Tuple<int, Contract>> requests)
        {
            requests.Foreach(req => AddRealtimeDataRequest(req.Item1, req.Item2));
        }

        public void AddRealtimeDataRequest(int tickerId, Contract contract)
        {
            ibClient.ClientSocket.reqRealTimeBars(tickerId + RT_BARS_ID_BASE, contract, 5, "MIDPOINT", false, null);
        }

        public void AddRealtimeDataRequest(int tickerId, Contract contract, string whatToShow, bool useRTH)
        {
            ibClient.ClientSocket.reqRealTimeBars(tickerId + RT_BARS_ID_BASE, contract, 5, whatToShow, useRTH, null);
        }

        public void AddHistoricalDataRequest(Contract contract, string endDateTime, string durationString, string barSizeSetting, string whatToShow, int useRTH, int dateFormat, bool keepUpToDate)
        {
            ibClient.ClientSocket.reqHistoricalData(1 + HISTORICAL_ID_BASE, contract, endDateTime, durationString, barSizeSetting, whatToShow, useRTH, 1, keepUpToDate, new List<TagValue>());
        }

        public void PlaceOrder(Contract contract, Order order)
        {
            ibClient.ClientSocket.placeOrder(order.OrderId, contract, order);
            NextOrderId++;
        }
    }

    public class IBTWSConnectionState
    {
        public bool IsConnected { get; }

        public IBTWSConnectionState(bool isConnected)
        {
            IsConnected = isConnected;
        }
    }

    public class IBTWSMessage
    {
        public IBTWSMessage(string message)
        {
            Message = message;
            TimeStamp = DateTimeOffset.Now;
        }

        public string Message { get; }
        public DateTimeOffset TimeStamp { get; }
    }



    public class IBTWSErrorMessage
    {
        public int Arg1 { get; }
        public int Arg2 { get; }
        public string Arg3 { get; }
        public Exception Ex { get; }

        public IBTWSErrorMessage(int arg1, int arg2, string arg3, Exception ex)
        {
            Arg1 = arg1;
            Arg2 = arg2;
            Arg3 = arg3;
            Ex = ex;
        }
    }
}
