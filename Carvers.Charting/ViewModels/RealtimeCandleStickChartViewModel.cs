using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using System.Windows.Media;
using Carvers.Charting.Annotations;
using Carvers.Infra;
using Carvers.Infra.Extensions;
using Carvers.Infra.ViewModels;
using Carvers.Models;
using Carvers.Models.DataReaders;
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
        private readonly double _barTimeFrame = TimeSpan.FromMinutes(5).TotalSeconds;
        private Candle _lastCandle;
        private IndexRange _xVisibleRange;
        private string _selectedSeriesStyle;
        private ObservableCollection<IRenderableSeriesViewModel> _seriesViewModels;
        private ObservableCollection<IAnnotationViewModel> _annotations;

        public RealtimeCandleStickChartViewModel(
            IObservable<Candle> candleFeed,
            IObservable<IEvent> eventsFeed = null)
        {
            _annotations = new ObservableCollection<IAnnotationViewModel>();
            Annotations = new AnnotationsSourceCollection(_annotations);
            AnnotationCollection = new AnnotationCollection();

            _seriesViewModels = new ObservableCollection<IRenderableSeriesViewModel>();
            var ds0 = new OhlcDataSeries<DateTime, double> { SeriesName = "Price Series" };
            _seriesViewModels.Add(new OhlcRenderableSeriesViewModel { DataSeries = ds0, StyleKey = "BaseRenderableSeriesStyle" });

            var ds1 = new XyDataSeries<DateTime, double> { SeriesName = "50-Period SMA" };
            _seriesViewModels.Add(new LineRenderableSeriesViewModel { DataSeries = ds1, StyleKey = "LineStyle" });

            candleFeed.ObserveOnDispatcher().Subscribe(c =>
            {
                OnNewPrice(c);
                //AnnotationCollection.Add(new SellArrowAnnotation { X1 = c.TimeStamp.DateTime, Y1 = c.Low });
            });

            if (eventsFeed != null)
            {
                eventsFeed
                    .ObserveOnDispatcher()
                    .OfType<DateTimeEvent<IOrder>>()
                    .Where(e => e.Event is BuyOrder || e.Event is BuyToCoverOrder)
                    .Subscribe(e => AnnotationCollection.Add(
                        new BuyArrowAnnotation {
                            X1 = e.DateTimeOffset.DateTime,
                            Y1 = e.Event.OrderInfo.Price.Value
                        }));

                eventsFeed
                    .ObserveOnDispatcher()
                    .OfType<DateTimeEvent<IOrder>>()
                    .Where(e => e.Event is ShortSellOrder || e.Event is SellOrder)
                    .Subscribe(e => AnnotationCollection.Add(
                        new SellArrowAnnotation
                        {
                            X1 = e.DateTimeOffset.DateTime,
                            Y1 = e.Event.OrderInfo.Price.Value
                        }));
            }


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

        public AnnotationsSourceCollection Annotations { get; }
        public AnnotationCollection AnnotationCollection { get; }
    }
}
