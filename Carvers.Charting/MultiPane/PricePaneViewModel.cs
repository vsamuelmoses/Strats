// *************************************************************************************
// SCICHART® Copyright SciChart Ltd. 2011-2017. All rights reserved.
//  
// Web: http://www.scichart.com
//   Support: support@scichart.com
//   Sales:   sales@scichart.com
// 
// PricePaneViewModel.cs is part of the SCICHART® Examples. Permission is hereby granted
// to modify, create derivative works, distribute and publish any part of this source
// code whether for commercial, private or personal use. 
// 
// The SCICHART® examples are distributed in the hope that they will be useful, but
// without any warranty. It is provided "AS IS" without warranty of any kind, either
// expressed or implied. 
// *************************************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Carvers.Charting.Annotations;
using Carvers.Models;
using Carvers.Models.Events;
using Carvers.Models.Indicators;
using SciChart.Charting.Model.ChartSeries;
using SciChart.Charting.Model.DataSeries;
using SciChart.Charting.Visuals.Annotations;
using Unity.Interception.Utilities;

namespace Carvers.Charting.MultiPane
{
    public class PricePaneViewModel : BaseChartPaneViewModel
    {
        private readonly IEnumerable<Color> _colors = new List<Color>() { Colors.Violet, Colors.Brown, Colors.Blue, Colors.CadetBlue, Colors.Yellow, Colors.DarkOrange };
        private Dictionary<string, XyDataSeries<DateTime, double>> _series;

        public static async Task<PricePaneViewModel> ConstructPricePaneViewModel(
            CreateMultiPaneStockChartsViewModel parentViewModel,
            IObservable<(Candle, IEnumerable<(IIndicator, double)>)> candleAndIndicatorFeed,
            IObservable<IEvent> eventsFeed = null)
        {
            var candleAndIndicators = await candleAndIndicatorFeed.FirstAsync();
            var indicators = candleAndIndicators.Item2.Select(tup => tup.Item1);
            return new PricePaneViewModel(parentViewModel, indicators, candleAndIndicatorFeed, eventsFeed);
        }

        private PricePaneViewModel(CreateMultiPaneStockChartsViewModel parentViewModel, 
            IEnumerable<IIndicator> indicators,
            IObservable<(Candle, IEnumerable<(IIndicator, double)>)> candleAndIndicatorFeed,
            IObservable<IEvent> eventsFeed)
            : base(parentViewModel)
        {
            // We can add Series via the SeriesSource API, where SciStockChart or SciChartSurface bind to IEnumerable<IChartSeriesViewModel>
            // Alternatively, you can delcare your RenderableSeries in the SciStockChart and just bind to DataSeries
            // A third method (which we don't have an example for yet, but you can try out) is to create an Attached Behaviour to transform a collection of IDataSeries into IRenderableSeries

            // Add the main OHLC chart

            var stockPrices = new OhlcDataSeries<DateTime, double>() {SeriesName = "OHLC"};
            ChartSeriesViewModels.Add(new CandlestickRenderableSeriesViewModel
            {
                DataSeries = stockPrices,
                AntiAliasing = false,
            });

            _series = indicators
                .Select(i => (i, new XyDataSeries<DateTime, double> {SeriesName = i.Description}))
                .ToDictionary(t => t.Item1.Description, t => t.Item2);

            _series
                .Zip(_colors, (kvp, c) => (kvp, c)).ForEach(t =>
                    ChartSeriesViewModels.Add(new LineRenderableSeriesViewModel {
                        DataSeries = t.Item1.Value,
                        Stroke = t.Item2
                    }));

            candleAndIndicatorFeed.Subscribe(tup =>
            {
                var candle = tup.Item1;

                if (candle == null)
                    return;

                stockPrices.Append(candle.TimeStamp.DateTime, candle.Open, candle.High, candle.Low, candle.Close);
                tup.Item2.ForEach(it => OnIndicator(it.Item1, tup.Item1, it.Item2));
            });

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


            //// Add a moving average
            //var maLow = new XyDataSeries<DateTime, double>() { SeriesName = "Low Line" };
            //maLow.Append(prices.TimeData, prices.CloseData.MovingAverage(50));
            //ChartSeriesViewModels.Add(new LineRenderableSeriesViewModel
            //{
            //    DataSeries = maLow,
            //    StyleKey = "LowLineStyle",
            //});

            //// Add a moving average
            //var maHigh = new XyDataSeries<DateTime, double>() { SeriesName = "High Line" };
            //maHigh.Append(prices.TimeData, prices.CloseData.MovingAverage(200));
            //ChartSeriesViewModels.Add(new LineRenderableSeriesViewModel
            //{
            //    DataSeries = maHigh,
            //    StyleKey = "HighLineStyle",
            //});

            YAxisTextFormatting = "$0.0000";
        }

        private void OnIndicator(IIndicator indicator, Candle candle, double value)
        {
            _series[indicator.Description].Append(candle.TimeStamp.DateTime, value);
        }
    }
}
