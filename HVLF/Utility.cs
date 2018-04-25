using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Carvers.Infra.Extensions;
using Carvers.Models;

namespace HVLF
{
    public static class Utility
    {
        public static DailyScannerUniverse DailyUniverseGenerator(DateTimeOffset date)
        {
            var path =
                $@"D:\Work\Taniwha Development\Runners\HLVFDataViewer\BacktestData\Scanners\{
                        date
                    :yyyyMMdd}\GainSinceOpenLarge_Scanner.csv";

            return new DailyScannerUniverse(path, ScannerFilePathToDateTimeConverter);
        }

        public static Dictionary<Symbol, List<DateTimeOffset>> ScannerFileParser(string scannerFilePath)
        {
            if (!File.Exists(scannerFilePath))
                return new Dictionary<Symbol, List<DateTimeOffset>>();

            var dictionary = new Dictionary<Symbol, List<DateTimeOffset>>();
            var entries =
                File.ReadAllLines(scannerFilePath)
                    .Select(line => line.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries))
                    .Select(arr => Tuple.Create(DateTimeOffset.ParseExact(arr.First(), "yyyyMMdd HH:mm:ss.fff", null),
                        arr.TakeWhile((str, index) => index != 0)))
                    .Select(tup => tup.Item2.Select(str => Tuple.Create(tup.Item1, new Symbol(str.Split(':')[1]))))
                    .SelectMany(item => item);

            entries.Foreach(tup =>
            {
                if (!dictionary.ContainsKey(tup.Item2))
                    dictionary.Add(tup.Item2, new List<DateTimeOffset>());

                dictionary[tup.Item2].Add(tup.Item1);
            });

            return dictionary;
        }

        public static MinuteCandle MinBarCandles(string[] values)
        {
            try
            {
                var index = 0;
                if (!DateTimeOffset.TryParseExact(values[index++], "yyyyMMdd", null, DateTimeStyles.AssumeUniversal,
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

        public static DateTimeOffset ScannerFilePathToDateTimeConverter(string path)
        {
            return DateTimeOffset.ParseExact(Path.GetFileName(Path.GetDirectoryName(path)), "yyyyMMdd", null);
        }
    }
}