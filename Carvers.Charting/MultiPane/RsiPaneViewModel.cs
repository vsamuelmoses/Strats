// *************************************************************************************
// SCICHART® Copyright SciChart Ltd. 2011-2017. All rights reserved.
//  
// Web: http://www.scichart.com
//   Support: support@scichart.com
//   Sales:   sales@scichart.com
// 
// RsiPaneViewModel.cs is part of the SCICHART® Examples. Permission is hereby granted
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
using Carvers.Models.Indicators;
using SciChart.Charting.Model.ChartSeries;
using SciChart.Charting.Model.DataSeries;
using Unity.Interception.Utilities;

namespace Carvers.Charting.MultiPane
{
    public class RsiPaneViewModel : BaseChartPaneViewModel
    {
        private Dictionary<IIndicator, XyDataSeries<DateTime, double>> _series;

        public RsiPaneViewModel(CreateMultiPaneStockChartsViewModel parentViewModel, 
            Dictionary<IIndicator, IObservable<(IIndicator, DateTime, double)>> indicatorFeed)
            : base(parentViewModel)
        {
            _series = indicatorFeed
                .ToDictionary(i => i.Key, i => new XyDataSeries<DateTime, double> {SeriesName = i.Key.Description});
            _series
                .ForEach(s => ChartSeriesViewModels.Add(new LineRenderableSeriesViewModel {DataSeries = s.Value}));

            indicatorFeed
                .Select(i => i.Value)
                .ForEach(feed => feed.Subscribe((t) => _series[t.Item1].Append(t.Item2, t.Item3)));

            YAxisTextFormatting = "0.0";

            Height = 100;
        }
    }
}