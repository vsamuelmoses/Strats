using System.Collections.Generic;
using System.Linq;
using Carvers.Models.Extensions;

namespace Carvers.Models
{
    public static class OrderExtensions
    {
        public static Price ProfitLoss(this IEnumerable<IClosedOrder> orders)
            => orders.Select(order => order.ProfitLoss).Total();
    }
}