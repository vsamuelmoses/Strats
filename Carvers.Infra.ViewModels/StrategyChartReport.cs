using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Threading;
using Carvers.Models;
using Carvers.Models.Extensions;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace Carvers.Infra.ViewModels
{
    public class StrategyChartReport : ViewModel
    {
        public ObservableCollection<Timestamped<Price>> CumPL { get; }
        public PlotModel PlotModel { get; private set; }

        public StrategyChartReport(IEnumerable<IStrategy> strategies, Dispatcher dispatcher)
        {
            CumPL = new ObservableCollection<Timestamped<Price>>();
            SetupModel();
            strategies.Select(strategy => strategy.CloseddOrders)
                .Merge()
                .Cast<IClosedOrder>()
                .SubscribeOn(dispatcher)
                .Aggregate(Tuple.Create(DateTimeOffset.Now, 0d.USD()),
                    (prevOrder, currentOrder) =>
                    {
                        var tup = Tuple.Create(currentOrder.OrderInfo.TimeStamp, prevOrder.Item2 + currentOrder.ProfitLoss);
                        ((LineSeries)PlotModel.Series[0]).Points.Add(new DataPoint(CumPL.Count, tup.Item2.Value));
                        CumPL.Add(new Timestamped<Price>(tup.Item1, tup.Item2));
                        OnPropertyChanged("CumPL");
                        return tup;
                    })
                .Throttle(TimeSpan.FromSeconds(2))
                .Subscribe(tup => {
                    if (CumPL.Any())
                    {
                        var axis = PlotModel.Axes.Single(a => a.Position == AxisPosition.Left);
                        axis.Maximum = CumPL.Max(price => price.Val.Value);
                        axis.Minimum = CumPL.Min(price => price.Val.Value);
                    }

                    PlotModel.InvalidatePlot(true);
                });


            //Observable.Interval(TimeSpan.FromSeconds(2))
            //    .SubscribeOn(dispatcher)
            //    .Subscribe(_ => {

            //        if (CumPL.Any())
            //        {
            //            var axis = PlotModel.Axes.Single(a => a.Position == AxisPosition.Left);
            //            axis.Maximum = CumPL.Max(price => price.Val.Value);
            //            axis.Minimum = CumPL.Min(price => price.Val.Value);
            //        }

            //        PlotModel.InvalidatePlot(true);
            //    });
        }

        private void SetupModel()
        {
            PlotModel = new PlotModel();
            PlotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Minimum = -50, Maximum = 50 });
            PlotModel.Series.Add(new LineSeries { LineStyle = LineStyle.Solid, StrokeThickness=2 });
        }
    }

}