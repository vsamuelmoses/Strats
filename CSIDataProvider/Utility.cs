using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Carvers.Infra;
using Carvers.Models;
using Carvers.Models.Extensions;

namespace CSIDataProvider
{
    public static class Utility
    {
        public static void CreateSummaryFile(string inputPath, string outputPath)
        {
            var text = Directory.EnumerateFiles(inputPath)
                .Select(file => new FileInfo(file))
                .Select(file => Tuple.Create(file, CsvReader.ReadFile(file, CSIFormat, skip: 0)))
                .Select(tup => new CandlesSummary(tup.Item1.Name.AsSymbol(), tup.Item2).ToCsv());
            File.WriteAllLines(outputPath, text);
        }

        public static DailyCandle CSIFormat(string[] values)
        {
            try
            {
                DateTimeOffset timestamp;
                if (!DateTimeOffset.TryParseExact(values[0], "yyyyMMdd", null, DateTimeStyles.AssumeUniversal, out timestamp))
                    return DailyCandle.Null;

                double open;
                if (!double.TryParse(values[1], out open))
                    return DailyCandle.Null;

                double high;
                if (!double.TryParse(values[2], out high))
                    return DailyCandle.Null;

                double low;
                if (!double.TryParse(values[3], out low))
                    return DailyCandle.Null;

                double close;
                if (!double.TryParse(values[4], out close))
                    return DailyCandle.Null;

                double volume;
                if (!double.TryParse(values[5], out volume))
                    return DailyCandle.Null;

                return new DailyCandle(new Ohlc(open / 100, high / 100, low / 100, close / 100, volume * 100), timestamp);
            }
            catch (Exception)
            {
                return DailyCandle.Null;
            }
        }

        public static CandlesSummary CSISummary(string[] values)
        {
            var symbol = values[0].AsSymbol();

            var firsttimestamp = DateTimeOffset.Parse(values[1]);
            var lastimestamp = DateTimeOffset.Parse(values[2]);

            var firstOpen = double.Parse(values[3]);
            var firstClose = double.Parse(values[4]);
            var lastOpen = double.Parse(values[5]);
            var lastClose = double.Parse(values[6]);
            var max = double.Parse(values[7]);
            var min = double.Parse(values[8]);
            var maxVol = double.Parse(values[9]);
            var minVol = double.Parse(values[10]);

            return new CandlesSummary(symbol, firsttimestamp, lastimestamp, firstOpen, firstClose, lastOpen, lastClose, max, min, maxVol, minVol);
        }

        public static void CreateSykesUniverse(string inputPath, string outputPath)
        {
            var text = Directory.EnumerateFiles(inputPath)
                .Select(file => new FileInfo(file))
                .Select(file => Tuple.Create(file, CsvReader.ReadFile(file, CSIFormat, skip: 0)))
                .Where(tup => tup.Item2.Any(candle => candle.Ohlc.Open >= 5d.USD() && candle.Ohlc.Open <= 10d.USD()))
                .Select(tup => Path.GetFileNameWithoutExtension(tup.Item1.Name));
            File.WriteAllLines(outputPath, text);
        }
    }
}