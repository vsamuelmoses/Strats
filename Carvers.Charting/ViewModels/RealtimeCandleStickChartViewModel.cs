using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Carvers.Infra;
using Carvers.Infra.Extensions;
using Carvers.Infra.ViewModels;
using Carvers.Models;
using Carvers.Models.DataReaders;
using Carvers.Models.Indicators;
using SciChart.Charting.Model.ChartSeries;
using SciChart.Charting.Model.DataSeries;
using SciChart.Data.Model;

namespace Carvers.Charting.ViewModels
{
    public class RealtimeCandleStickChartViewModel : ViewModel
    {
        //private readonly IMarketDataService _marketDataService;
        private readonly MovingAverage _sma50 = new MovingAverage(50);
        private readonly double _barTimeFrame = TimeSpan.FromMinutes(5).TotalSeconds;
        private Candle _lastCandle;
        private IndexRange _xVisibleRange;
        private string _selectedSeriesStyle;
        private ObservableCollection<IRenderableSeriesViewModel> _seriesViewModels;

        public RealtimeCandleStickChartViewModel()
        {
            _seriesViewModels = new ObservableCollection<IRenderableSeriesViewModel>();

            // Market data service simulates live ticks. We want to load the chart with 150 historical bars
            // then later do real-time ticking as new data comes in
            //_marketDataService = new MarketDataService(new DateTime(2000, 08, 01, 12, 00, 00), 5, 20);

            // Add ChartSeriesViewModels for the candlestick and SMA series
            var ds0 = new OhlcDataSeries<DateTime, double> { SeriesName = "Price Series" };
            _seriesViewModels.Add(new OhlcRenderableSeriesViewModel { DataSeries = ds0, StyleKey = "BaseRenderableSeriesStyle" });

            var ds1 = new XyDataSeries<DateTime, double> { SeriesName = "50-Period SMA" };
            _seriesViewModels.Add(new LineRenderableSeriesViewModel { DataSeries = ds1, StyleKey = "LineStyle" });

            // Append 150 historical bars to data series
            //var prices = _marketDataService.GetHistoricalData(100);

            //var prices = CsvReader.ReadFile(Paths.SampleCsvData, CsvToModelCreators.CsvToFx1MinCandle, skip: 0)
            //    .Take(100);
            //ds0.Append(
            //    prices.Select(x => x.TimeStamp.DateTime),
            //    prices.Select(x => x.Open),
            //    prices.Select(x => x.High),
            //    prices.Select(x => x.Low),
            //    prices.Select(x => x.Close));

            //ds1.Append(prices.Select(x => x.TimeStamp.DateTime), prices.Select(y => _sma50.Push(y.Close).Current));


            var feed = new FileFeedService<Candle>(Paths.SampleCsvData, 
                str => CsvToModelCreators.CsvToFx1MinCandle(str.AsCsv()), TimeSpan.FromSeconds(1));

            var agg = new AggreagateCandleFeed(feed.Stream, TimeSpan.FromHours(1));

            agg.Stream.Subscribe(c =>
            {
                OnNewPrice(c); 
            });
            SelectedSeriesStyle = "Ohlc";
        }

        public ObservableCollection<IRenderableSeriesViewModel> SeriesViewModels
        {
            get { return _seriesViewModels; }
            set
            {
                _seriesViewModels = value;
                OnPropertyChanged("SeriesViewModels");
            }
        }

        public double BarTimeFrame { get { return _barTimeFrame; } }

        //public ICommand TickCommand
        //{
        //    get { return new RelayCommand(_ => OnNewPrice(_marketDataService.GetNextBar())); }
        //}

        //public ICommand StartUpdatesCommand { get { return new RelayCommand(_ => _marketDataService.SubscribePriceUpdate(OnNewPrice)); } }

        //public ICommand StopUpdatesCommand { get { return new RelayCommand(_ => _marketDataService.ClearSubscriptions()); } }

        public IEnumerable<string> SeriesStyles { get { return new[] { "OHLC", "Candle", "Line", "Mountain" }; } }

        public IEnumerable<int> StrokeThicknesses { get { return new[] { 1, 2, 3, 4, 5 }; } }

        public string SelectedSeriesStyle
        {
            get { return _selectedSeriesStyle; }
            set
            {
                _selectedSeriesStyle = value;
                OnPropertyChanged("SelectedSeriesStyle");

                if (_selectedSeriesStyle == "OHLC")
                {
                    SeriesViewModels[0] = new OhlcRenderableSeriesViewModel
                    {
                        DataSeries = SeriesViewModels[0].DataSeries,
                        StyleKey = "BaseRenderableSeriesStyle"
                    };
                }
                else if (_selectedSeriesStyle == "Candle")
                {
                    SeriesViewModels[0] = new CandlestickRenderableSeriesViewModel
                    {
                        DataSeries = SeriesViewModels[0].DataSeries,
                        StyleKey = "BaseRenderableSeriesStyle"
                    };
                }
                else if (_selectedSeriesStyle == "Line")
                {
                    SeriesViewModels[0] = new LineRenderableSeriesViewModel
                    {
                        DataSeries = SeriesViewModels[0].DataSeries,
                        StyleKey = "BaseRenderableSeriesStyle"
                    };
                }
                else if (_selectedSeriesStyle == "Mountain")
                {
                    SeriesViewModels[0] = new MountainRenderableSeriesViewModel
                    {
                        DataSeries = SeriesViewModels[0].DataSeries,
                        StyleKey = "BaseRenderableSeriesStyle"
                    };
                }
            }
        }

        public IndexRange XVisibleRange
        {
            get { return _xVisibleRange; }
            set
            {
                if (Equals(_xVisibleRange, value))
                    return;
                _xVisibleRange = value;
                OnPropertyChanged("XVisibleRange");
            }
        }

        private void OnNewPrice(Candle candle)
        {
            // Ensure only one update processed at a time from multi-threaded timer
            lock (this)
            {
                // Update the last price, or append? 
                var ds0 = (IOhlcDataSeries<DateTime, double>)_seriesViewModels[0].DataSeries;
                var ds1 = (IXyDataSeries<DateTime, double>)_seriesViewModels[1].DataSeries;

                if (_lastCandle != null && _lastCandle.TimeStamp.DateTime == candle.TimeStamp)
                {
                    ds0.Update(candle.TimeStamp.DateTime, candle.Open, candle.High, candle.Low, candle.Close);
                    ds1.Update(candle.TimeStamp.DateTime, _sma50.Update(candle.Close).Current);
                }
                else
                {
                    ds0.Append(candle.TimeStamp.DateTime, candle.Open, candle.High, candle.Low, candle.Close);
                    ds1.Append(candle.TimeStamp.DateTime, _sma50.Push(candle.Close).Current);

                    // If the latest appending point is inside the viewport (i.e. not off the edge of the screen)
                    // then scroll the viewport 1 bar, to keep the latest bar at the same place
                    if (XVisibleRange != null && XVisibleRange.Max > ds0.Count)
                    {
                        var existingRange = _xVisibleRange;
                        var newRange = new IndexRange(existingRange.Min + 1, existingRange.Max + 1);
                        XVisibleRange = newRange;
                    }
                }

                _lastCandle = candle;
            }
        }
    }
}
