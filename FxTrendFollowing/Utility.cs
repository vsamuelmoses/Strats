using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Carvers.Infra.Extensions;
using Carvers.Models;
using Carvers.Models.Indicators;

namespace FxTrendFollowing
{
    public static class Utility
    {
        public static MinuteCandle CsvToFx1MinCandle(string[] values)
        {
            try
            {
                var index = 0;
                if (!DateTimeOffset.TryParseExact(values[index++], "yyyy.MM.dd", null, DateTimeStyles.AssumeUniversal,
                    out var date))
                    return MinuteCandle.NullMinuteCandle;

                if (!TimeSpan.TryParseExact(values[index++], "g", CultureInfo.CurrentCulture, out var time))
                    return MinuteCandle.NullMinuteCandle;

                var timestamp = date.Add(time);

                if (!double.TryParse(values[index++], out var open))
                    return MinuteCandle.NullMinuteCandle;

                if (!double.TryParse(values[index++], out var high))
                    return MinuteCandle.NullMinuteCandle;

                if (!double.TryParse(values[index++], out var low))
                    return MinuteCandle.NullMinuteCandle;

                if (!double.TryParse(values[index++], out var close))
                    return MinuteCandle.NullMinuteCandle;

                if (!double.TryParse(values[index++], out var volume))
                    return MinuteCandle.NullMinuteCandle;

                return new MinuteCandle(new Ohlc(open, high, low, close, volume), timestamp);
            }
            catch (Exception)
            {
                return MinuteCandle.NullMinuteCandle;
            }
        }


        public static Candle IBDataCandleReader(string[] values, TimeSpan span)
        {
            try
            {
                var index = 0;
                if (!DateTimeOffset.TryParseExact(values[index++], "yyyy.MM.dd", null, DateTimeStyles.AssumeUniversal,
                    out var date))
                    return Candle.Null;

                if (!TimeSpan.TryParseExact(values[index++], "g", CultureInfo.CurrentCulture, out var time))
                    return Candle.Null;

                var timestamp = date.Add(time);

                if (!double.TryParse(values[index++], out var open))
                    return Candle.Null;

                if (!double.TryParse(values[index++], out var high))
                    return Candle.Null;

                if (!double.TryParse(values[index++], out var low))
                    return Candle.Null;

                if (!double.TryParse(values[index++], out var close))
                    return Candle.Null;

                if (!double.TryParse(values[index++], out var volume))
                    return Candle.Null;

                return new Candle(new Ohlc(open, high, low, close, volume), timestamp, span);
            }
            catch (Exception)
            {
                return Candle.Null;
            }
        }


        /* 
         * The original formula worked only on the D1 TF, and the currently forming candle. It checked where price currently was, in terms of the candle high and low, expressed as a percentage. For example, if price was currently at the high, the value returned was 100%; at the low, 0%; three quarters of the way up the candle, 75%; and so forth.

            Then it converted this into a value between 0 and 9, thus:
            Above 97%, value = 9
            Between 90% and 97%, value = 8
            Between 75% and 90%, value = 7
            Between 60% and 75%, value = 6
            Between 50% and 60%, value = 5
            Between 40% and 50%, value = 4
            Between 25% and 40%, value = 3
            Between 10% and 25%, value = 2
            Between 3% and 10%, value = 1
            Less than 3%, value = 0

            It processed all of the candles for each currency pair. Let’s say the pair currently being processed is USDCHF, and price is currently 64% of the way up the candle. Then it would assign a value of 6 for USD, and 9 minus 6 = 3 for CHF. Then it would repeat the same for all currency pairs (as specified in CurrencyPairs), average the results for each currency (not pair), and plot the values.

            CSM extends this concept in three different ways:

            1. You can specify NumberOfCandles. If set to 1, then this would work as the Giraia indy did, i.e. operate on the currently forming candle. If set to 2, it includes both the currently forming candle, and also the one immediately to the left; and so on. It uses the highest and lowest points across the 2 candle interval as 100% and 0%, respectively. Hence the value reflects where price currently is, relative to the highest and lowest point of the last n candles, where n is the NumberOfCandles setting.

            2. The above assumes that ApplySmoothing = false. Setting this to true means that the entire process will be repeated, over the last NumberOfCandles candles, and the result averaged. For example, suppose NumberOfCandles is set to 4. Then if ApplySmoothing = true, the entire calculation (as explained previously) is run for each of NumberOfCandles = 1, 2, 3 and 4, and the four results averaged. This effectively means that, if ApplySmoothing = true, there is a higher weighting applied to the most recent candles. If set to false, it is simply where price is, relative to the high and low of the last n candles. 

            3. Instead of being restricted to the D1 TF, separate plots may be generated depending on the user’s choice of TimeFrames. Each TF is calculated completely independently of the others.
         * 
         */
        public static Tuple<DateTimeOffset, double, double> ToCurrencyStrength(this Candle candle, TimeSpan candleSpan)
        {
            var bodyLength = candle.Ohlc.High - candle.Ohlc.Low;
            var currentPosition = candle.Ohlc.Close - candle.Ohlc.Low;

            var percentMove = currentPosition.Value.PercentageOf(bodyLength.Value);

            if (bodyLength.Value == 0d)
                percentMove = 0;

            Debug.Assert(!double.IsNaN(percentMove));

            return Tuple.Create(candle.TimeStamp.Add(candleSpan), percentMove, 100 - percentMove);
        }

        public static Tuple<DateTimeOffset, double, double> ToCurrencyStrength(this Candle candle)
        {
            return ToCurrencyStrength(candle, candle.Span);
        }


        public static Symbol Fx1MinFileNameToSymbolConverter(string fileName)
        {
            var targetCur = fileName.Split('_')[2].Substring(0, 3);
            var baseCur = fileName.Split('_')[2].Substring(3, 3);

            return CurrencyPair.Get(Currency.Get(targetCur), Currency.Get(baseCur));
        }

        public static string SymbolFilePathGetter(Symbol symbol, DateTimeOffset dateTime)
        {
            switch (symbol)
            {
                case CurrencyPair pair:
                {
                    return Path.Combine(Paths.HistoricalData, dateTime.Year.ToString(),
                        $"DAT_MT_{pair.ToString()}_M1_{dateTime.Year.ToString()}.csv");
                }

                case Index index:
                {
                    return Path.Combine(Paths.HistoricalData, dateTime.Year.ToString(),
                        $"{index.ToString()}_M1_{dateTime.Year.ToString()}.csv");
                }

                default:
                    throw new Exception("Unknown Symbol");
            }
        }

        public static string FxEURUSDFilePathGetter(DateTimeOffset dateTime)
        {
                return Path.Combine(Paths.HistoricalData, dateTime.Year.ToString(),
                    $"DAT_MT_EURUSD_M1_{dateTime.Year.ToString()}02.csv");
        }


        public static string FxIBDATAPathGetter(CurrencyPair cxPair)
        {
            return Path.Combine(Paths.IBData,$"{cxPair.ToString()}.csv");
        }

        public static string CSFileGetter(Currency currency)
        {
            return Path.Combine(Paths.FxStrenghts, $"{currency.Symbol}.csv");
        }
    }

    public static class Paths
    {
        public const string IBData = Data + "IBData\\";
        //public const string Data = @"C:\Users\Senior\Documents\Strats\FxTrendFollowing";
        public const string HistoricalData = @"..\..\";
        public const string Data = @"..\..\Data\";
        public const string FxStrenghts = Data + "FxStrength.Data";
        public const string FxStrenghtsAll = Data + @"FxStrength.Data\All.csv";
        public const string StrategyLogs = Data + "Strategies\\";
        public static FileInfo StrategySummaryFile(Strategy strategy, Symbol symbol)
        {
            return new FileInfo(StrategyLogs + $"{strategy.StrategyName}\\{symbol}.txt");
        }

        public static FileInfo IBDataCandlesFor(Symbol instrument, string span)
        {
            return new FileInfo(IBData + $@"{instrument}.{span}.csv");
        }

        public static FileInfo ShadowCandlesFor(Symbol instrument, string span)
        {
            return new FileInfo(IBData + $@"ShadowCandles\{instrument}.Shadow.{span}.csv");
        }
    }
}