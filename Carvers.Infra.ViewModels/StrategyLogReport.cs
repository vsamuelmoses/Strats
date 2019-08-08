using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using Carvers.Models;
using Carvers.Models.Indicators;

namespace Carvers.Infra.ViewModels
{
    public static class Paths
    {
        public static string Reports = @"..\..\Logs";
    }

    public class StrategyLogReport
    {
        private List<string> Log = new List<string>();
        
        public StrategyLogReport(IEnumerable<IStrategy> strategies, string logName)
        {
            //var closedOrders = strategies.Select(strat => strat.CloseddOrders)
            //    .Merge()
            //    .Subscribe(info => Log.Add(info.ToCsv()), 
            //    () =>
            //    {

            //        if (!Directory.Exists(Paths.Reports))
            //            Directory.CreateDirectory(Paths.Reports);

            //        var logs = strategies.First().ClosedOrders
            //            .GroupBy(order => order.OrderInfo.TimeStamp.Date)
            //            .OrderBy(g => g.Key)
            //            .Select(grp => $"{grp.Key.ToShortDateString()}, {(grp.Sum(g => g.ProfitLoss.Value))/(20000) }")
            //            .ToList();



            //        File.WriteAllLines(Path.Combine(Paths.Reports, $"{DateTime.Now:yyyyMMddHHmmss}.{logName}.csv"), logs);
            //        });

            var closedOrders = strategies.Select(strat => strat.CloseddOrders)
                .Merge()
                .Subscribe(info => Log.Add(info.ToCsv()),
                    () =>
                    {

                        if (!Directory.Exists(Paths.Reports))
                            Directory.CreateDirectory(Paths.Reports);

                        File.WriteAllLines(Path.Combine(Paths.Reports, $"{DateTime.Now:yyyyMMddHHmmss}.{logName}.csv"), Log);
                    });

        }
    }
}