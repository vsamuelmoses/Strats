using System;
using System.Reactive.Linq;
using System.Windows;
using Carvers.Charting.ViewModels;
using Carvers.Infra;
using Carvers.Infra.Extensions;
using Carvers.Models;
using Carvers.Models.DataReaders;
using Carvers.Models.Events;

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

            RealtimeCandleStickViewModel = new TraderViewModel(agg.Stream, 
                eventsFeed:agg
                    .Stream
                    .Where(c => c.TimeStamp.Hour == 9)
                    .Select(c => new MarketOpeningIndicator(c.TimeStamp, c)));

            DataContext = this;
        }

        public TraderViewModel RealtimeCandleStickViewModel { get; }
    }
}
