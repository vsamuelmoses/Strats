using System.Collections.Generic;
using System.Linq;
using Carvers.Infra.Extensions;
using Carvers.Models;
using Carvers.Models.Extensions;

namespace SykesStrategies.ViewModels.Strategies
{
    public static class StrategyExtensions
    {
        public static Price ProfitLoss(this IStrategy strategy)
            => strategy.ClosedOrders.ProfitLoss();

        public static Price ProfitLoss(this IEnumerable<IStrategy> strategies)
            => strategies.Select(strategy => ProfitLoss((IStrategy) strategy)).Total();

    }
}