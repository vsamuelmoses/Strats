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
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Visuals.Annotations;
using SciChart.Charting.Visuals.RenderableSeries;
using SciChart.Data.Model;

namespace Carvers.Charting.ViewModels
{
    public class TraderViewModel : ViewModel
    {
        private readonly MovingAverage _sma50 = new MovingAverage(50);
        private readonly MovingAverage _sma100 = new MovingAverage(100);
        private readonly MovingAverage _sma250 = new MovingAverage(250);
        private readonly MovingAverage _sma500 = new MovingAverage(500);
        private readonly MovingAverage _sma1000 = new MovingAverage(1000);
        private readonly MovingAverage _sma3600 = new MovingAverage(3600);
        private readonly ExponentialMovingAverage _ex3600 = new ExponentialMovingAverage(3600);


        private readonly double _barTimeFrame = TimeSpan.FromMinutes(1).TotalSeconds;
        private Candle _lastCandle;
        private IndexRange _xVisibleRange;
        private string _selectedSeriesStyle;
        private ObservableCollection<IRenderableSeries> _seriesViewModels;
        private Lookback lb;
        private XyDataSeries<DateTime, double> plSeries;
        private DateTime lastPlTimeStamp;

        public TraderViewModel(IObservable<Candle> candleFeed,
            IObservable<DateTimeEvent<Price>> profitLossFeed = null,
            IObservable<IEvent> eventsFeed = null)
        {
            lb = new Lookback(10, new ConcurrentQueue<Candle>());

            AnnotationCollection = new AnnotationCollection();

            _seriesViewModels = new ObservableCollection<IRenderableSeries>();
            var ds0 = new OhlcDataSeries<DateTime, double> { SeriesName = "Price Series" };
            _seriesViewModels.Add(new FastOhlcRenderableSeries
            {
                DataSeries = ds0,
            });

            var ds1 = new XyDataSeries<DateTime, double> { SeriesName = "50-Period SMA" };
            _seriesViewModels.Add(new FastLineRenderableSeries
            {
                DataSeries = ds1,
                Stroke = Colors.Orange
            });

            var ds2 = new XyDataSeries<DateTime, double> { SeriesName = "100-Period SMA" };
            _seriesViewModels.Add(new FastLineRenderableSeries
            {
                DataSeries = ds2,
                Stroke = Colors.Violet

            });

            var ds3 = new XyDataSeries<DateTime, double> { SeriesName = "3600-Period SMA" };
            _seriesViewModels.Add(new FastLineRenderableSeries
            {
                DataSeries = ds3,
                Stroke = Colors.Blue
            });

            var ds4 = new XyDataSeries<DateTime, double> { SeriesName = "250-Period SMA" };
            _seriesViewModels.Add(new FastLineRenderableSeries
            {
                DataSeries = ds4 ,
                Stroke = Colors.Green
            });

            var ds5 = new XyDataSeries<DateTime, double> { SeriesName = "500-Period SMA" };
            _seriesViewModels.Add(new FastLineRenderableSeries
            {
                DataSeries = ds5,
                Stroke = Colors.Yellow
            });

            var ds6 = new XyDataSeries<DateTime, double> { SeriesName = "1000-Period SMA" };
            _seriesViewModels.Add(new FastLineRenderableSeries
            {

                DataSeries = ds6,
                Stroke = Colors.Aqua
            });

            plSeries = new XyDataSeries<DateTime, double> { SeriesName = "ProfitLoss" };
            _seriesViewModels.Add(new FastLineRenderableSeries
            {
                DataSeries = plSeries,
                Stroke = Colors.GreenYellow,
                DrawNaNAs = LineDrawMode.ClosedLines,
                YAxisId = "PnL",
            });


            //var support = new XyDataSeries<DateTime, double> { SeriesName = "Support", AcceptsUnsortedData = true };
            //_seriesViewModels.Add(new LineRenderableSeriesViewModel { DataSeries = support, StyleKey = "LineStyle" });

            //var resistance = new XyDataSeries<DateTime, double> { SeriesName = "Resistance", AcceptsUnsortedData = true };
            //_seriesViewModels.Add(new LineRenderableSeriesViewModel { DataSeries = resistance, StyleKey = "LineStyle", });

            candleFeed
                .ObserveOnDispatcher()
                .Subscribe(OnNewPrice);

            profitLossFeed
                .ObserveOnDispatcher()
                .Subscribe(pl => OnPLChanged(pl));

            if (eventsFeed != null)
            {
                eventsFeed
                    .ObserveOnDispatcher()
                    .OfType<DateTimeEvent<IOrder>>()
                    .Where(e => e.Event is BuyOrder)
                    .Subscribe(e => AnnotationCollection.Add(
                        new BuyArrowAnnotation
                        {
                            X1 = e.DateTimeOffset.DateTime,
                            Y1 = e.Event.OrderInfo.Price.Value,


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
                            FillBrush = new SolidColorBrush(Colors.DarkOrange),


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
                            Y1 = e.Event.OrderInfo.Price.Value,


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
                            FillBrush = new SolidColorBrush(Colors.DarkOrange),
                        }
                    ));
            }
        }

        private double recentPl = 0d;
        private void OnPLChanged(DateTimeEvent<Price> pl)
        {
            var plS = (IXyDataSeries<DateTime, double>)_seriesViewModels[7].DataSeries;


            lastPlTimeStamp = pl.DateTimeOffset.DateTime;
            if (_lastCandle.TimeStamp.DateTime == lastPlTimeStamp)
                plS.Update(pl.DateTimeOffset.DateTime, pl.Event.Value);
            else
                plS.Append(pl.DateTimeOffset.DateTime, pl.Event.Value);

            recentPl = pl.Event.Value;

            if (XVisibleRange != null && XVisibleRange.Max > plS.Count)
            {
                var existingRange = _xVisibleRange;
                var newRange = new IndexRange(existingRange.Min + 1, existingRange.Max + 1);
                XVisibleRange = newRange;
            }
        }

        private void OnNewPrice(Candle candle)
        {
            // Ensure only one update processed at a time from multi-threaded timer
            //lock (this)
            {
                // Update the last price, or append? 
                var ds0 = (IOhlcDataSeries<DateTime, double>)_seriesViewModels[0].DataSeries;
                var ds1 = (IXyDataSeries<DateTime, double>)_seriesViewModels[1].DataSeries;
                var ds2 = (IXyDataSeries<DateTime, double>)_seriesViewModels[2].DataSeries;
                var ds3 = (IXyDataSeries<DateTime, double>)_seriesViewModels[3].DataSeries;
                var ds4 = (IXyDataSeries<DateTime, double>)_seriesViewModels[4].DataSeries;
                var ds5 = (IXyDataSeries<DateTime, double>)_seriesViewModels[5].DataSeries;
                var ds6 = (IXyDataSeries<DateTime, double>)_seriesViewModels[6].DataSeries;
                var pl = (IXyDataSeries<DateTime, double>)_seriesViewModels[7].DataSeries;


                if (lastPlTimeStamp != candle.TimeStamp.DateTime)
                    pl.Append(candle.TimeStamp.DateTime, recentPl);

                if (_lastCandle != null && _lastCandle.TimeStamp.DateTime == candle.TimeStamp)
                {
                    ds0.Update(candle.TimeStamp.DateTime, candle.Open, candle.High, candle.Low, candle.Close);
                    ds1.Update(candle.TimeStamp.DateTime, _sma50.Update(candle.Close).Current);
                    ds2.Update(candle.TimeStamp.DateTime, _sma100.Update(candle.Close).Current);
                    ds3.Update(candle.TimeStamp.DateTime, _sma3600.Update(candle.Close).Current);
                    ds4.Update(candle.TimeStamp.DateTime, _sma250.Update(candle.Close).Current);
                    ds5.Update(candle.TimeStamp.DateTime, _sma500.Update(candle.Close).Current);
                    //ds6.Update(candle.TimeStamp.DateTime, _sma1000.Update(candle.Close).Current);
                    ds6.Update(candle.TimeStamp.DateTime, _ex3600.Push(candle.Close));

                }
                else
                {
                    ds0.Append(candle.TimeStamp.DateTime, candle.Open, candle.High, candle.Low, candle.Close);
                    ds1.Append(candle.TimeStamp.DateTime, _sma50.Push(candle.Close).Current);
                    ds2.Append(candle.TimeStamp.DateTime, _sma100.Push(candle.Close).Current);
                    ds3.Append(candle.TimeStamp.DateTime, _sma3600.Push(candle.Close).Current);
                    ds4.Append(candle.TimeStamp.DateTime, _sma250.Push(candle.Close).Current);
                    ds5.Append(candle.TimeStamp.DateTime, _sma500.Push(candle.Close).Current);
                    //ds6.Append(candle.TimeStamp.DateTime, _sma1000.Push(candle.Close).Current);
                    ds6.Append(candle.TimeStamp.DateTime, _ex3600.Push(candle.Close));

                    //// If the latest appending point is inside the viewport (i.e. not off the edge of the screen)
                    //// then scroll the viewport 1 bar, to keep the latest bar at the same place
                    //if (XVisibleRange != null && XVisibleRange.Max > ds0.Count)
                    //{
                    //    var existingRange = _xVisibleRange;
                    //    var newRange = new IndexRange(existingRange.Min + 1, existingRange.Max + 1);
                    //    XVisibleRange = newRange;
                    //}
                }

                _lastCandle = candle;
            }
        }


        public ObservableCollection<IRenderableSeries> SeriesViewModels
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