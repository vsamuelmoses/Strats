using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using Carvers.Models;

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
            var closedOrders = strategies.Select(strat => strat.CloseddOrders)
                .Merge()
                .Subscribe(info => Log.Add(info.ToCsv()), 
                () => {

                    File.WriteAllLines(Path.Combine(Paths.Reports, $"{DateTime.Now:yyyyMMddHHmmss}.{logName}.csv"), Log);
                    });
        }
    }
}