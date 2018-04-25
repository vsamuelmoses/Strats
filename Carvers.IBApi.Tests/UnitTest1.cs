using System;
using System.Threading;
using System.Threading.Tasks;
using Carvers.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Carvers.IBApi.Tests
{
   
    [TestClass]
    public class UnitTest1
    {
    
        [TestInitialize]
        public void Setup()
        {
            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
        }

        private bool isHistoricalDataHit = false;
        private bool isHistoricalDataEndHit = false;
        private bool isHistoricalDataUpdateHit = false;

        [TestMethod]
        public void TestHistoricalMarketData()
        {
            var connection = new IBTWS().Connect(IBTWS.LocalHost, IBTWS.IBPaperTradingPort, IBTWS.ClientId, IbClient_Error, IbClient_ConnectionClosed);
            Assert.IsTrue(connection);



            ibClient.Error += IbClient_Error;
            ibClient.ConnectionClosed += IbClient_ConnectionClosed;

            var contract = ContractCreator.GetCurrencyPairContract(CurrencyPair.EURJPY);
            var endTime = DateTime.Now.ToString("yyyyMMdd HH:mm:ss");
            var duration = "10 D";
            var barSize = "1 hour";
            var whatToShow = "MIDPOINT";
            var outsideRTH = 0;

            isHistoricalDataHit = false;
            isHistoricalDataEndHit = false;
            isHistoricalDataUpdateHit = false;

            ibClient.HistoricalData += IbClient_HistoricalData;
            ibClient.HistoricalDataUpdate += IbClient_HistoricalDataUpdate; ;
            ibClient.HistoricalDataEnd += IbClient_HistoricalDataEnd; ;

            IBTWS.AddHistoricalDataRequest(ibClient, contract, endTime, duration, barSize, whatToShow, outsideRTH, 1, false);

            Task.Delay(TimeSpan.FromSeconds(30)).Wait();

            ibClient.ClientSocket.eDisconnect();
            
            Assert.IsTrue(isHistoricalDataHit);
            //Assert.IsTrue(isHistoricalDataUpdateHit);
            Assert.IsTrue(isHistoricalDataEndHit);
        }

        private void IbClient_Error(int arg1, int arg2, string arg3, Exception arg4)
        {
            Console.WriteLine($"Error {arg1.ToString()}, {arg2.ToString()}, {arg3.ToString()}, {arg4}");
        }

        private void IbClient_ConnectionClosed()
        {
            Console.WriteLine("Disconnected");
        }

        private void IbClient_HistoricalData(IBSampleApp.messages.HistoricalDataMessage obj)
        {
            isHistoricalDataHit = true;
        }

        private void IbClient_HistoricalDataEnd(IBSampleApp.messages.HistoricalDataEndMessage obj)
        {
            isHistoricalDataEndHit = true;
        }

        private void IbClient_HistoricalDataUpdate(IBSampleApp.messages.HistoricalDataMessage obj)
        {
            isHistoricalDataUpdateHit = true;
        }
    }

    
}
