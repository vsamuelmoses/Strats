using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Windows.Media;
using Carvers.Charting.Annotations;
using Carvers.Infra.ViewModels;
using Carvers.Models;
using Carvers.Models.Events;
using Carvers.Models.Indicators;
using SciChart.Charting.Model.ChartSeries;
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Visuals.Annotations;
using SciChart.Data.Model;

namespace Carvers.Charting.ViewModels
{
    public class RealtimeCandleStickChartViewModel : ViewModel
    {
        private readonly MovingAverage _sma50 = new MovingAverage(50);
        private readonly MovingAverage _sma100 = new MovingAverage(100);
        private readonly MovingAverage _sma250 = new MovingAverage(250);
        private readonly MovingAverage _sma3600 = new MovingAverage(3600);

        private readonly double _barTimeFrame = TimeSpan.FromMinutes(5).TotalSeconds;
        private Candle _lastCandle;
        private IndexRange _xVisibleRange;
        private string _selectedSeriesStyle;
        private ObservableCollection<IRenderableSeriesViewModel> _seriesViewModels;
        private Lookback lb;
        public RealtimeCandleStickChartViewModel(IObservable<Candle> candleFeed,
            IObservable<IEvent> eventsFeed = null)
        {
            lb = new Lookback(10, new ConcurrentQueue<Candle>());

            AnnotationCollection = new AnnotationCollection();

            _seriesViewModels = new ObservableCollection<IRenderableSeriesViewModel>();
            var ds0 = new OhlcDataSeries<DateTime, double> { SeriesName = "Price Series" };
            _seriesViewModels.Add(new OhlcRenderableSeriesViewModel { DataSeries = ds0, StyleKey = "BaseRenderableSeriesStyle" });

            var ds1 = new XyDataSeries<DateTime, double> { SeriesName = "50-Period SMA" };
            _seriesViewModels.Add(new LineRenderableSeriesViewModel { DataSeries = ds1, StyleKey = "LineStyle" });

            var ds2 = new XyDataSeries<DateTime, double> { SeriesName = "100-Period SMA" };
            _seriesViewModels.Add(new LineRenderableSeriesViewModel { DataSeries = ds2, StyleKey = "WhiteLineStyle" });

            var ds3 = new XyDataSeries<DateTime, double> { SeriesName = "3600-Period SMA" };
            _seriesViewModels.Add(new LineRenderableSeriesViewModel { DataSeries = ds3, StyleKey = "AquaLineStyle" });

            var ds4 = new XyDataSeries<DateTime, double> { SeriesName = "250-Period SMA" };
            _seriesViewModels.Add(new LineRenderableSeriesViewModel { DataSeries = ds4, StyleKey = "LightYellowLineStyle" });


            //var support = new XyDataSeries<DateTime, double> { SeriesName = "Support", AcceptsUnsortedData = true };
            //_seriesViewModels.Add(new LineRenderableSeriesViewModel { DataSeries = support, StyleKey = "LineStyle" });

            //var resistance = new XyDataSeries<DateTime, double> { SeriesName = "Resistance", AcceptsUnsortedData = true };
            //_seriesViewModels.Add(new LineRenderableSeriesViewModel { DataSeries = resistance, StyleKey = "LineStyle", });

            candleFeed
                .ObserveOnDispatcher()
                .Subscribe(OnNewPrice);

            if (eventsFeed != null)
            {
                eventsFeed
                    .ObserveOnDispatcher()
                    .Subscribe(e => Console.WriteLine($"{e.ToString()}"));


                eventsFeed
                    .ObserveOnDispatcher()
                    .OfType<DateTimeEvent<IOrder>>()
                    .Where(e => e.Event is BuyOrder)
                    .Subscribe(e => AnnotationCollection.Add(
                        new BuyArrowAnnotation
                        {
                            X1 = e.DateTimeOffset.DateTime,
                            Y1 = e.Event.OrderInfo.Price.Value
                        }
                    ));

                eventsFeed
                    .ObserveOnDispatcher()
                    .OfType<DateTimeEvent<IOrder>>()
                    .Where(e => e.Event is BuyToCoverOrder)
                    .Subscribe(e => AnnotationCollection.Add(
                        new UpArrowAnnotation
                        {
                            X1 = e.DateTimeOffset.DateTime,
                            Y1 = e.Event.OrderInfo.Price.Value,
                            FillBrush = new SolidColorBrush(Colors.DarkOrange)
                        }
                    ));


                eventsFeed
                    .ObserveOnDispatcher()
                    .OfType<DateTimeEvent<IOrder>>()
                    .Where(e => e.Event is ShortSellOrder)
                    .Subscribe(e => AnnotationCollection.Add(
                        new SellArrowAnnotation
                        {
                            X1 = e.DateTimeOffset.DateTime,
                            Y1 = e.Event.OrderInfo.Price.Value
                        }));

                eventsFeed
                    .ObserveOnDispatcher()
                    .OfType<DateTimeEvent<IOrder>>()
                    .Where(e => e.Event is SellOrder)
                    .Subscribe(e => AnnotationCollection.Add(
                        new DownArrowAnnotation()
                        {
                            X1 = e.DateTimeOffset.DateTime,
                            Y1 = e.Event.OrderInfo.Price.Value,
                            FillBrush = new SolidColorBrush(Colors.DarkOrange)
                        }
                    ));


                //eventsFeed
                //    .ObserveOnDispatcher()
                //    .OfType<MarketOpeningIndicator>()
                //    .Subscribe(e => AnnotationCollection.Add(
                //        new LineAnnotation()
                //        {
                //            X1 = e.DateTimeOffset.DateTime,
                //            Y1 = e.Event.High + 1d,

                //            X2 = e.DateTimeOffset.DateTime,
                //            Y2 = e.Event.Low - 1d,

                //            Background = Brushes.Pink
                //        }));
            }

            SelectedSeriesStyle = "Ohlc";
        }
        private void OnNewPrice(Candle candle)
        {
            // Ensure only one update processed at a time from multi-threaded timer
            lock (this)
            {
                // Update the last price, or append? 
                var ds0 = (IOhlcDataSeries<DateTime, double>)_seriesViewModels[0].DataSeries;
                var ds1 = (IXyDataSeries<DateTime, double>)_seriesViewModels[1].DataSeries;
                var ds2 = (IXyDataSeries<DateTime, double>)_seriesViewModels[2].DataSeries;
                var ds3 = (IXyDataSeries<DateTime, double>)_seriesViewModels[3].DataSeries;
                var ds4 = (IXyDataSeries<DateTime, double>)_seriesViewModels[4].DataSeries;
                //var support = (IXyDataSeries<DateTime, double>)_seriesViewModels[2].DataSeries;
                //var resistance = (IXyDataSeries<DateTime, double>)_seriesViewModels[3].DataSeries;

                //lb = lb.Add(candle);
                //if (lb.IsComplete())
                //{
                //    var sr = SupportResistance.Calculate(lb);

                //    support.Append(sr.Support.Point1.X.DateTime, sr.Support.Point1.Y);
                //    support.Append(sr.Support.Point2.X.DateTime, sr.Support.Point2.Y);

                //    resistance.Append(sr.Resistance.Point1.X.DateTime, sr.Resistance.Point1.Y);
                //    resistance.Append(sr.Resistance.Point2.X.DateTime, sr.Resistance.Point2.Y);
                //}

                if (_lastCandle != null && _lastCandle.TimeStamp.DateTime == candle.TimeStamp)
                {
                    ds0.Update(candle.TimeStamp.DateTime, candle.Open, candle.High, candle.Low, candle.Close);
                    ds1.Update(candle.TimeStamp.DateTime, _sma50.Update(candle.Close).Current);
                    ds2.Update(candle.TimeStamp.DateTime, _sma100.Update(candle.Close).Current);
                    ds3.Update(candle.TimeStamp.DateTime, _sma3600.Update(candle.Close).Current);
                    ds4.Update(candle.TimeStamp.DateTime, _sma250.Update(candle.Close).Current);
                }
                else
                {
                    ds0.Append(candle.TimeStamp.DateTime, candle.Open, candle.High, candle.Low, candle.Close);
                    ds1.Append(candle.TimeStamp.DateTime, _sma50.Push(candle.Close).Current);
                    ds2.Append(candle.TimeStamp.DateTime, _sma100.Push(candle.Close).Current);
                    ds3.Append(candle.TimeStamp.DateTime, _sma3600.Push(candle.Close).Current);
                    ds4.Append(candle.TimeStamp.DateTime, _sma250.Push(candle.Close).Current);

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

        
        public AnnotationCollection AnnotationCollection { get; }
    }
}
