using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Transactions;
using System.Windows.Input;
using System.Windows.Media;
using Carvers.Charting.Annotations;
using Carvers.Infra.ViewModels;
using Carvers.Models;
using Carvers.Models.Events;
using Carvers.Models.Indicators;
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.ViewportManagers;
using SciChart.Charting.Visuals.Annotations;
using SciChart.Charting.Visuals.RenderableSeries;
using SciChart.Charting.Visuals.TradeChart;
using SciChart.Data.Model;
using Unity.Interception.Utilities;

namespace Carvers.Charting.ViewModels
{
    public class TraderViewModel : ViewModel, IChildPane
    {
        private readonly double _barTimeFrame = TimeSpan.FromMinutes(1).TotalSeconds;
        private Candle _lastCandle;
        private IndexRange _xVisibleRange;
        private string _selectedSeriesStyle;
        private ObservableCollection<IRenderableSeries> _chart1SeriesViewModels;
        private ObservableCollection<IRenderableSeries> _chart2SeriesViewModels;
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
            lb = new Lookback(10, new List<Candle>());

            AnnotationCollection = new AnnotationCollection();

            _chart1SeriesViewModels = new ObservableCollection<IRenderableSeries>();
            _chart2SeriesViewModels = new ObservableCollection<IRenderableSeries>();

            var stochasticIndicator = indicators
                .FirstOrDefault(i => i is StochasticIndicator);

            if (stochasticIndicator != null)
            {
                _chart2SeriesViewModels.Add(new FastLineRenderableSeries()
                {
                    DataSeries = new XyDataSeries<DateTime, double> {SeriesName = stochasticIndicator.Description},
                    Stroke = Colors.Red
                });
            }

            var ds0 = new OhlcDataSeries<DateTime, double> { SeriesName = "Price Series" };
            _chart1SeriesViewModels.Add(new FastOhlcRenderableSeries
            {
                DataSeries = ds0,
            });
            _series = indicators
                .Select(i => (i, new XyDataSeries<DateTime, double> {SeriesName = i.Description}))
                .ToDictionary(t => t.Item1, t => t.Item2);

            _series
                .Zip(_colors, (kvp, c) => (kvp, c)).ForEach(t =>
                _chart1SeriesViewModels.Add(new FastLineRenderableSeries
                {
                    DataSeries = t.Item1.Value,
                    Stroke = t.Item2
                }));

            plSeries = new XyDataSeries<DateTime, double> { SeriesName = "ProfitLoss" };
            _chart1SeriesViewModels.Add(new FastLineRenderableSeries
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
                    t.Item2.ForEach(it =>
                    {
                        if (it.Item1 is StochasticIndicator)
                        {
                            var ma50 = t.Item2.First(i => i.Item1 is StochasticIndicator);
                            ((XyDataSeries<DateTime, double>)Chart2SeriesViewModels[0].DataSeries).Append(t.Item1.TimeStamp.DateTime, ma50.Item2);
                        }
                        else
                            OnIndicator(it.Item1, t.Item1, it.Item2);
                    });


                    
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

            ViewportManager = new DefaultViewportManager();
            VerticalChartGroupId = Guid.NewGuid().ToString();
        }

        public string VerticalChartGroupId { get; }

        public IViewportManager ViewportManager { get; }

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
            var ds0 = (IOhlcDataSeries<DateTime, double>)_chart1SeriesViewModels[0].DataSeries;
            ds0.Append(candle.TimeStamp.DateTime, candle.Open, candle.High, candle.Low, candle.Close);

            if (lastPlTimeStamp != candle.TimeStamp.DateTime)
                plSeries.Append(candle.TimeStamp.DateTime, recentPl);
            _lastCandle = candle;
        }

        public ObservableCollection<IRenderableSeries> Chart2SeriesViewModels
        {
            get { return _chart2SeriesViewModels; }
            set
            {
                _chart2SeriesViewModels = value;
                OnPropertyChanged();
            }
        }


        public ObservableCollection<IRenderableSeries> Chart1SeriesViewModels
        {
            get { return _chart1SeriesViewModels; }
            set
            {
                _chart1SeriesViewModels = value;
                OnPropertyChanged();
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

        //IChildPane Implementation
        public void ZoomExtents()
        {
            throw new NotImplementedException();
        }

        public string Title { get; set; }
        public ICommand ClosePaneCommand { get; set; }
    }

    public class MultiTraderViewModel : ViewModel, IChildPane
    {
        public IEnumerable<TraderViewModel> TraderViewModels { get; }

        public MultiTraderViewModel(IEnumerable<TraderViewModel> traderViewModels)
        {
            TraderViewModels = traderViewModels;
            
        }

        public void ZoomExtents()
        {
        }

        public ViewportManagerBase ViewportManager { get; }

        public string Title { get; set; }
        public ICommand ClosePaneCommand { get; set; }
    }
}