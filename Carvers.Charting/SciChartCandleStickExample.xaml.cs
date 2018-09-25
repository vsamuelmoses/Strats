using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Carvers.Infra;
using Carvers.Models.DataReaders;
using SciChart.Charting.Model.DataSeries;

namespace Carvers.Charting
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class SciChartCandleStickExample : UserControl
    {
        public SciChartCandleStickExample()
        {
            InitializeComponent();

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Create a dataset of type x=DateTime, y=Double
            var dataSeries = new OhlcDataSeries<DateTime, double>();

            // Prices are in the format Time, Open, High, Low, Close (all IList)
            //var prices = DataManager.Instance.GetPriceData(Instrument.Indu.Value, TimeFrame.Daily);

            var candles = CsvReader.ReadFile(Paths.SampleCsvData, CsvToModelCreators.CsvToFx1MinCandle, skip: 0);

            // Append data to series. SciChart automatically redraws
            dataSeries.Append(
                candles.Select(c => c.TimeStamp.DateTime),
                candles.Select(c => c.Open),
                candles.Select(c => c.High),
                candles.Select(c => c.Low),
                candles.Select(c => c.Close));

            sciChart.RenderableSeries[0].DataSeries = dataSeries;

            // Zoom Extents - necessary as we have AutoRange=False
            sciChart.ZoomExtents();
        }

    }

    public static class Paths
    {
        public static FileInfo SampleCsvData = new FileInfo("SampleData/DAT_MT_EURGBP_M1_2017.csv");
    }
}
