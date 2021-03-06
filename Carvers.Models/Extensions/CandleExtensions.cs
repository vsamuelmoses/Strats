﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using Carvers.Infra.Extensions;

namespace Carvers.Models.Extensions
{
    public static class CandleExtensions
    {
        public static bool IsAtStartOfHour(this Candle candle)
        {
            return (candle.TimeStamp.Minute == 0 && candle.TimeStamp.Second == 0);
        }

        public static bool StartsAtXMinsPastHour(this Candle candle, int minutes)
        {
            return (candle.TimeStamp.Minute == minutes && candle.TimeStamp.Second == 0);
        }

        public static bool EndsAtXMinsPastHour(this Candle candle, int minutes)
        {
            var endTimestamp = candle.TimeStamp.Add(candle.Span);
            return (endTimestamp.Minute == minutes && endTimestamp.Second == 0);
        }


        public static Candle Add(this Candle candle1, Candle candle2)
        {
            var ohlc = new Ohlc(candle1.Ohlc.Open.Value, 
                Math.Max(candle1.Ohlc.High.Value, candle2.Ohlc.High.Value), 
                Math.Min(candle1.Ohlc.Low.Value, candle2.Ohlc.Low.Value),
                candle2.Ohlc.Close.Value,
                candle1.Ohlc.Volume + candle2.Ohlc.Volume);
            return new Candle(ohlc, candle1.TimeStamp, candle1.Span + candle2.Span);
        }

        public static IEnumerable<Candle> ToHourCandles(this IEnumerable<MinuteCandle> minuteCandles)
        {
            return 
            minuteCandles
                .GroupBy(candle => candle.TimeStamp.Subtract(TimeSpan.FromMinutes(candle.TimeStamp.Minute)))
                .Select(candleGroup => candleGroup.Cast<Candle>().ToSingleCandle(TimeSpan.FromHours(1)));
        }

        public static IEnumerable<DailyCandle> ToDailyCandles(this IEnumerable<Candle> candles)
        {
            return
                candles
                    .GroupBy(candle => candle.TimeStamp.Date)
                    .Select(candleGroup => new DailyCandle(candleGroup.Cast<Candle>().ToSingleOhlc(), candleGroup.First().TimeStamp));
        }

        public static IObservable<Candle> ToDailyCandleStream(this IObservable<Candle> candles)
        {
            return candles
                .Buffer(candles.Select(c1 => c1.TimeStamp.Date).DistinctUntilChanged())
                .Select(c => c.ToDailyCandles().Single());
        }

        public static Ohlc ToSingleOhlc(this IEnumerable<Candle> candles)
        {

            return new Ohlc(candles.First().Ohlc.Open,
                candles.Max(c => c.Ohlc.High),
                candles.Min(c => c.Ohlc.Low),
                candles.Last().Ohlc.Close,
                candles.Sum(c => c.Ohlc.Volume));
        }

        public static Candle ToSingleCandle(this IEnumerable<Candle> candles, TimeSpan span)
        {

            var ohlc = candles.ToSingleOhlc();
            return new Candle(ohlc, candles.First().TimeStamp, span);
        }

        public static Candle AdjustTime(this Candle candle, Func<DateTime, DateTime> timeAdjustment)
            => new Candle(candle.Ohlc, timeAdjustment(candle.TimeStamp.DateTime), candle.Span);
        

        public static bool PassedThroughPrice(this Candle candle, double value)
        {
            return candle.High > value && candle.Low < value;
        }

        public static double PercentageDifferenceInDailyVolume(this IEnumerable<Candle> candles, DateTimeOffset dt)
        {

            var twoCandles =
                candles.ToDailyCandles()
                    .TakeWhile(candle => candle.TimeStamp.Date <= dt.Date)
                    .TakeLastInReverse(2);

            var dailyCandles = twoCandles.ToList();
            return dailyCandles.Last().Ohlc.Volume.PercentageRise(dailyCandles.First().Ohlc.Volume);
        }

        public static bool IsRed(this Candle candle)
            => candle.Ohlc.Open > candle.Ohlc.Close;

        public static bool IsGreen(this Candle candle)
            => candle.Ohlc.Open < candle.Ohlc.Close;

        public static double CandleLength(this Candle candle)
            => candle.Ohlc.High.Value - candle.Ohlc.Low.Value;

        public static double AbsBodyLength(this Candle candle)
            => Math.Abs(candle.Ohlc.Close.Value - candle.Ohlc.Open.Value);

        public static bool IsHigherThan(this Candle candle1, Candle candle2)
            => candle1.Ohlc.High.Value > candle2.Ohlc.High.Value;

        public static bool IsLowerThan(this Candle candle1, Candle candle2)
            => candle1.Ohlc.Low.Value < candle2.Ohlc.Low.Value;


        public static bool ClosedAboveHigh(this Candle candle1, Candle candle2)
            => candle1.Ohlc.Close > candle2.Ohlc.High;

        public static bool ClosedBelowLow(this Candle candle1, Candle candle2)
            => candle1.Ohlc.Close < candle2.Ohlc.Low;

        public static double MiddlePoint(this Candle candle)
            => candle.CandleLength()/2 + candle.Low;
    }


    public static class CandleStickPatternsExtensions
    {
        public static bool IsInverterHammer(this Candle candle, int parts)
        {
            var totalLength = candle.High - candle.Low;
            var fifthPart = totalLength / parts;
            var lastPart = candle.Low + fifthPart;

            return candle.Open < lastPart && candle.Close < lastPart;
        }

        public static bool IsHammer(this Candle candle, int parts)
        {
            var totalLength = candle.High - candle.Low;
            var fifthPart = totalLength / parts;
            var lastPart = candle.High - fifthPart;

            return candle.Open > lastPart && candle.Close > lastPart;
        }

    }
}
