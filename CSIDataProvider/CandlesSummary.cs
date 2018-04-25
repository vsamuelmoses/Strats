using System;
using System.Collections.Generic;
using System.Linq;
using Carvers.Models;

namespace CSIDataProvider
{

    public class CandlesSummary
    {
        public CandlesSummary(Symbol symbol, DateTimeOffset startTimeStamp, DateTimeOffset endTimeStamp, 
            double firstOpen, double firstClose, 
            double max, double min, 
            double lastOpen, double lastClose, 
            double maxVolume, double minVolume)
        {
            Symbol = symbol;
            StartTimeStamp = startTimeStamp;
            EndTimeStamp = endTimeStamp;
            FirstOpen = firstOpen;
            FirstClose = firstClose;
            Max = max;
            Min = min;
            LastOpen = lastOpen;
            LastClose = lastClose;
            MaxVolume = maxVolume;
            MinVolume = minVolume;
        }

        public CandlesSummary(Symbol symbol, IEnumerable<Candle> candles)
        {
            Symbol = symbol;
            var enumerable = candles as Candle[] ?? candles.ToArray();
            StartTimeStamp = enumerable.Min(candle => candle.TimeStamp);
            EndTimeStamp = enumerable.Max(candle => candle.TimeStamp);
            FirstOpen = enumerable.Single(candle => candle.TimeStamp == StartTimeStamp).Ohlc.Open.Value;
            FirstClose = enumerable.Single(candle => candle.TimeStamp == StartTimeStamp).Ohlc.Close.Value;
            LastOpen = enumerable.Single(candle => candle.TimeStamp == EndTimeStamp).Ohlc.Open.Value;
            LastClose = enumerable.Single(candle => candle.TimeStamp == EndTimeStamp).Ohlc.Close.Value;
            Max = enumerable.Max(candle => candle.Ohlc.High).Value;
            Min = enumerable.Max(candle => candle.Ohlc.Low).Value;
            MaxVolume = enumerable.Max(candle => candle.Ohlc.Volume);
            MinVolume = enumerable.Max(candle => candle.Ohlc.Volume);
        }



        public string ToCsv()
        {
            return $"{Symbol},{StartTimeStamp},{EndTimeStamp},{FirstOpen},{FirstClose},{LastOpen},{LastClose},{Max},{Min},{MaxVolume},{MinVolume}";
        }

        public Symbol Symbol { get; }

        public DateTimeOffset StartTimeStamp { get; }
        public DateTimeOffset EndTimeStamp { get; }
        public double FirstOpen { get; }
        public double FirstClose { get; }
        public double Max { get; }
        public double Min { get; }
        public double LastOpen { get; }
        public double LastClose { get; }
        public double MaxVolume { get; }
        public double MinVolume { get; }
    }
}
