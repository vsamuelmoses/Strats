﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
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
using Unity.Interception.Utilities;

namespace Carvers.Charting.ViewModels
{
    public class TraderViewModel : ViewModel
    {
        //private readonly MovingAverage _sma50 = new MovingAverage(50);
        //private readonly MovingAverage _sma100 = new MovingAverage(100);
        //private readonly MovingAverage _sma250 = new MovingAverage(250);
        //private readonly MovingAverage _sma500 = new MovingAverage(500);
        //private readonly MovingAverage _sma1000 = new MovingAverage(1000);
        //private readonly MovingAverage _sma3600 = new MovingAverage(3600);
        //private readonly ExponentialMovingAverage _ex3600 = new ExponentialMovingAverage(3600);


        private readonly double _barTimeFrame = TimeSpan.FromMinutes(1).TotalSeconds;
        private Candle _lastCandle;
        private IndexRange _xVisibleRange;
        private string _selectedSeriesStyle;
        private ObservableCollection<IRenderableSeries> _seriesViewModels;
        private Lookback lb;
        private XyDataSeries<DateTime, double> plSeries;
        private DateTime lastPlTimeStamp;

        public static async Task<TraderViewModel> ConstructTraderViewModel(
            IObservable<(Candle, IEnumerable<(IIndicator, double)>)> candleAndIndicatorFeed,
            IObservable<DateTimeEvent<Price>> profitLossFeed = null,
            IObservable<IEvent> eventsFeed = null)
        {
            var candleAndIndicators = await candleAndIndicatorFeed.FirstAsync();
            var indicators = candleAndIndicators.Item2.Select(tup => tup.Item1);
            return new TraderViewModel(indicators, candleAndIndicatorFeed, profitLossFeed, eventsFeed);
        }

        public TraderViewModel(IObservable<Candle> candleFeed,
            IObservable<DateTimeEvent<Price>> profitLossFeed = null,
            IObservable<IEvent> eventsFeed = null)
        : this(Enumerable.Empty<IIndicator>(),
            candleFeed.Select(f => (f, Enumerable.Empty<(IIndicator, double)>())),
            profitLossFeed, eventsFeed)
        {
        }

        private IEnumerable<Color> _colors = new List<Color>() {Colors.Violet, Colors.Brown, Colors.Blue, Colors.LawnGreen, Colors.Yellow, Colors.DarkOrange};

        private TraderViewModel(
            IEnumerable<IIndicator> indicators,
            IObservable<(Candle, IEnumerable<(IIndicator, double)>)> candleAndIndicatorFeed,
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

            _series = indicators
                .Select(i => (i, new XyDataSeries<DateTime, double> {SeriesName = i.Description}))
                .ToDictionary(t => t.Item1, t => t.Item2);

            _series.ForEach(s =>
                _seriesViewModels.Add(new FastLineRenderableSeries
                {
                    DataSeries = s.Value,
                    Stroke = Colors.Orange
                }));

            plSeries = new XyDataSeries<DateTime, double> { SeriesName = "ProfitLoss" };
            _seriesViewModels.Add(new FastLineRenderableSeries
            {
                DataSeries = plSeries,
                Stroke = Colors.GreenYellow,
                DrawNaNAs = LineDrawMode.ClosedLines,
                YAxisId = "PnL",
            });

            candleAndIndicatorFeed
                .ObserveOnDispatcher()
                .Subscribe(t =>
                {
                    OnNewPrice(t.Item1);
                    t.Item2.ForEach(it => OnIndicator(it.Item1, t.Item1, it.Item2));
                });

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
        private Dictionary<IIndicator, XyDataSeries<DateTime, double>> _series;

        private void OnPLChanged(DateTimeEvent<Price> pl)
        {
            lastPlTimeStamp = pl.DateTimeOffset.DateTime;
            if (_lastCandle.TimeStamp.DateTime == lastPlTimeStamp)
                plSeries.Update(pl.DateTimeOffset.DateTime, pl.Event.Value);
            else
                plSeries.Append(pl.DateTimeOffset.DateTime, pl.Event.Value);

            recentPl = pl.Event.Value;
        }

        private void OnIndicator(IIndicator indicator, Candle candle, double value)
        {
            _series[indicator].Append(candle.TimeStamp.DateTime, value);
        }

        private void OnNewPrice(Candle candle)
        {
            var ds0 = (IOhlcDataSeries<DateTime, double>)_seriesViewModels[0].DataSeries;
            ds0.Append(candle.TimeStamp.DateTime, candle.Open, candle.High, candle.Low, candle.Close);

            if (lastPlTimeStamp != candle.TimeStamp.DateTime)
                plSeries.Append(candle.TimeStamp.DateTime, recentPl);
            _lastCandle = candle;
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