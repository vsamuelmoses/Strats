using System;

namespace Carvers.Models
{
    public interface IOrderInfo
    {
        Symbol Symbol { get; }
        Price Price { get; }
        DateTimeOffset TimeStamp { get; }
        IStrategy Strategy { get; }
        int Size { get; }
        Candle Candle { get; }
        string ToCsv();
    }

    public class OrderInfo : IOrderInfo
    {
        public OrderInfo(DateTimeOffset timestamp, Symbol symbol, IStrategy strategy, Price price)
            : this(timestamp, symbol, strategy, price, 0)
        {
        }

        public OrderInfo(DateTimeOffset timestamp, Symbol symbol, IStrategy strategy, Price price, int size, Candle candle)
            : this(timestamp, symbol, strategy, price, size)
        {
            Candle = candle;
        }

        public OrderInfo(DateTimeOffset timestamp, Symbol symbol, IStrategy strategy, Price price, int size)
        {
            Strategy = strategy;
            TimeStamp = timestamp;
            Price = price;
            Symbol = symbol;
            Size = size;
        }

        public int Size { get; }
        public IStrategy Strategy { get; }
        public DateTimeOffset TimeStamp { get; }
        public Price Price { get; }
        public Symbol Symbol { get; }
        public Candle Candle { get; }

        public string ToCsv()
        {
            return $"{Symbol},{TimeStamp},{Price}";
        }
    }

}