using Carvers.IBApi.Extensions;
using Carvers.Infra;
using Carvers.Infra.Extensions;
using Carvers.Models;
using IBApi;
using IBSampleApp.messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace Carvers.IBApi
{
    public class IBTWSSimulator : IEngine
    {
        private readonly Subject<RealTimeBarMessage> realTimeBarMsgSubject;
        public IObservable<RealTimeBarMessage> RealTimeBarStream => realTimeBarMsgSubject;

        private readonly Subject<IBTWSMessage> ibtwsMessageSubject;
        public IObservable<IBTWSMessage> IbtwsMessageStream => ibtwsMessageSubject;

        private readonly Subject<IBTWSErrorMessage> ibtwsErrorSubject;
        public IObservable<IBTWSErrorMessage> IbtwsErrorStream => ibtwsErrorSubject;

        private readonly Subject<IBTWSConnectionState> ibtwsConnectionStateSubject;
        public IObservable<IBTWSConnectionState> IbtwsConnectionStateStream => ibtwsConnectionStateSubject;

        private readonly DateTimeOffset startTime;
        private readonly DateTimeOffset endTime;
        private readonly Func<Symbol, DateTimeOffset, string> filePathGetter;


        public bool IsConnected { get; private set; }
        public int NextOrderId { get; }

        public readonly Subject<HistoricalDataMessage> historicalDataSubject;
        public IObservable<HistoricalDataMessage> HistoricalDataStream => historicalDataSubject;
        public IObservable<CashBalance> CashBalanceStream => Observable.Empty<CashBalance>();

        public IBTWSSimulator(Func<Symbol, DateTimeOffset, string> filePathGetter, DateTimeOffset startTime)
            : this(filePathGetter, startTime, DateTimeOffset.MaxValue)
        { }

        public IBTWSSimulator(Func<Symbol, DateTimeOffset, string> filePathGetter, DateTimeOffset startTime,
            DateTimeOffset endTime)
        {
            this.filePathGetter = filePathGetter;
            this.startTime = startTime;
            this.endTime = endTime;

            
            historicalDataSubject = new Subject<HistoricalDataMessage>();
            realTimeBarMsgSubject = new Subject<RealTimeBarMessage>();
            ibtwsErrorSubject = new Subject<IBTWSErrorMessage>();
            ibtwsMessageSubject = new Subject<IBTWSMessage>();
            ibtwsConnectionStateSubject = new Subject<IBTWSConnectionState>();
        }

        public bool Connect(string host, int port, int clientId)
        {
            IsConnected = true;
            ibtwsConnectionStateSubject.OnNext(new IBTWSConnectionState(true));

            return true;
        }

        public void Disconnect()
        {
            IsConnected = false;
            ibtwsConnectionStateSubject.OnNext(new IBTWSConnectionState(false));
        }

        public void AddRealtimeDataRequests(IEnumerable<Tuple<int, Contract>> requests)
        {
            Task.Factory.StartNew(() =>
            {
                var fileFeeds = requests
                    .Select(req => req.Item2.ToSymbol())
                    .Select(pair => new FileFeed<RealTimeBarMessage>(filePathGetter(pair, startTime), 
                                        line => IbApiUtility.ToRealTimeBarMessage(line.AsCsv(), pair.UniqueId)))
                    .ToList();

                while(true)
                {

                    var firstEntry = fileFeeds
                    .Select(feed => Tuple.Create(feed,feed.PeekLine()))
                    //.Where(tup => 
                    //{
                    //    if (tup.Item2 != null)
                    //    {
                    //        System.Diagnostics.Debug.WriteLine(tup.Item2.RequestId);
                    //        System.Diagnostics.Debug.WriteLine(tup.Item2.Timestamp.UnixEpochToLocalTime());
                    //    }

                    //    //return tup.Item2 != null && tup.Item2.Timestamp.UnixEpochToLocalTime() >= startTime;
                    //    return tup.Item2 != null && tup.Item2.Timestamp.UnixEpochToLocalTime();

                    //})
                    .Where(tup => !tup.Item1.EndOfFeed)
                    .OrderBy(tup => tup.Item2.Timestamp)
                    .FirstOrDefault();

                    if (firstEntry == null)
                        return;

                    var toSend = firstEntry.Item1.ReadNextLine();

                    if (toSend.TimeStamp > endTime)
                        return;

                    //Debug.WriteLine($"{System.Threading.Thread.CurrentThread.ManagedThreadId} - Publish on - {toSend.TimeStamp}");

                    if (toSend.TimeStamp >= startTime)    
                        realTimeBarMsgSubject.OnNext(toSend);
                }

            });
        }

        public void AddRealtimeDataRequest(int tickerId, Contract contract)
        {
            //var pair = CurrencyPair.Get(Currency.Get(contract.Symbol), Currency.Get(contract.Currency));

            //var path = filePathGetter(pair, startTime);
            //Task.Factory.StartNew(() =>
            //{
            //    ibtwsMessageSubject.OnNext(new IBTWSMessage($"Starting to read {path}"));
            //    var fileFeed = new FileFeed(path);
            //    ibtwsMessageSubject.OnNext(new IBTWSMessage($"Finished reading"));

            //    fileFeed.ReadLines().Foreach(line =>
            //    {
            //        var item = IbApiUtility.ToRealTimeBarMessage(line.AsCsv());

            //        if (item.Timestamp.UnixEpochToLocalTime() >= startTime)
            //        {
            //            item.RequestId = IBTWS.RT_BARS_ID_BASE + tickerId;
            //            realTimeBarMsgSubject.OnNext(item);
            //        }
            //    });
            //});
        }

        public void AddRealtimeDataRequest(int tickerId, Contract contract, string whatToShow, bool useRTH)
        {
            throw new NotImplementedException();
        }
        
        public void AddHistoricalDataRequest(int tickerId, Contract contract, string endDateTime, string durationString,
            string barSizeSetting, string whatToShow, int useRTH, int dateFormat, bool keepUpToDate)
        {
            throw new NotImplementedException();
        }

        public void AddHistoricalDataRequest(Contract contract, string endDateTime, string durationString, string barSizeSetting,
            string whatToShow, int useRTH, int dateFormat, bool keepUpToDate)
        {
            throw new NotImplementedException();
        }

        public void PlaceOrder(Contract contract, Order order)
        {
            
        }

        public IObservable<CashBalance> GetCashBalances()
        {
            throw new NotImplementedException();
        }
    }
}