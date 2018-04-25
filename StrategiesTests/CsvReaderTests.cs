using System;
using Carvers.Infra.Extensions;
using Carvers.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SykesStrategies;
using SykesStrategies.Utilities;

namespace StrategiesTests
{
    [TestClass]
    public class CsvReaderTests
    {
        [TestMethod]
        public void ConstructDailyCandleForGoogleDataFormat()
        {
            //Date, Open, High, Low, Close, Volume
            //19-May-17,7.46,7.5,7.41,7.43,9563

            var line = "19-May-17,7.46,7.5,7.41,7.43,9563";
            var expected = new DailyCandle(new Ohlc(7.46, 7.5, 7.41, 7.43, 9563), new DateTime(2017, 05, 19, 0, 0, 0, DateTimeKind.Utc));

            Assert.AreEqual(expected, CandleCreator.GoogleFormat(line.AsCsv()));
        }

        [TestMethod]
        public void ConstructDailyCandleFromQuoteMedia()
        {
            var line = "02/08/2016,7.44,7.4678,7.3,7.32,310401,-0.127,-1.88%,6.6657,2284078.31,1087";
            var expected = new DailyCandle(new Ohlc(7.44, 7.4678, 7.3, 7.32, 310401), new DateTime(2016, 08, 02, 0,0,0,DateTimeKind.Utc));

            Assert.AreEqual(expected, CandleCreator.QuoteMediaFormat(line.AsCsv()));
        }

    }
}
