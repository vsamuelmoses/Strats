using System;
using System.Windows;
using Carvers.Charting.ViewModels;
using Carvers.Infra;
using Carvers.Infra.Extensions;
using Carvers.Models;
using Carvers.Models.DataReaders;

namespace Carvers.Charting.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var feed = new FileFeedService<Candle>(Paths.SampleCsvData,
                str => CsvToModelCreators.CsvToFx1MinCandle(str.AsCsv()));

            var agg = new AggreagateCandleFeed(feed.Stream, TimeSpan.FromHours(1));

            RealtimeCandleStickViewModel = new RealtimeCandleStickChartViewModel(agg.Stream);

            DataContext = this;
        }

        public RealtimeCandleStickChartViewModel RealtimeCandleStickViewModel { get; }
    }
}
