using System;
using System.IO;
using System.Linq;
using Carvers.Infra;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CSIDataProvider.Tests
{
    [TestClass]
    public class UtilityTests
    {
        [TestMethod]
        public void Given_TheDirectoryOfData_Then_SummaryFileIsGenerated()
        {
            Utility.CreateSummaryFile(Paths.CsiData, "CSI_Summary.csv");
        }

        [TestMethod]
        public void Given_TheDirectoryOfData_Then_SykesUniverseFileIsGenerated()
        {
            Utility.CreateSykesUniverse(Paths.CsiData, "SykesUniverse.csv");
        }

        [TestMethod]
        public void GetSykesUniverseCount()
        {
            var summary = CsvReader.ReadFile(new FileInfo(Paths.CsiSummary), Utility.CSISummary, skip: 0);
            Console.WriteLine(summary.Count(stock =>
               stock.EndTimeStamp >= new DateTimeOffset(2006, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        }
    }
}
