using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Carvers.Models
{
    public abstract class Universe
    {
        protected readonly ConcurrentDictionary<Symbol, StockData> stocks;

        protected Universe()
        {
            stocks = new ConcurrentDictionary<Symbol, StockData>();
        }

        public abstract Task Initialise { get; }

        public IEnumerable<StockData> Stocks => stocks.Values;
    }
}