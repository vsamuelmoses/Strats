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

            var agg = new AggreagateCandleFeed(feed.Stream.Select(c => new Timestamped<Candle>(c.TimeStamp, c)), TimeSpan.FromHours(1));

            RealtimeCandleStickViewModel = new TraderViewModel(agg.Stream.Select(c => c.Val), 
                eventsFeed:agg
                    .Stream
                    .Where(c => c.Val.TimeStamp.Hour == 9)
                    .Select(c => new MarketOpeningIndicator(c.Val.TimeStamp, c.Val)));

            DataContext = this;
        }

        public TraderViewModel RealtimeCandleStickViewModel { get; }
    }
}
