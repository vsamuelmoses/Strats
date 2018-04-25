using System;
using Carvers.Models;

namespace FxTrendFollowing
{
    public class TrendFollowingOptions : IStrategyOptions
    {
        public TrendFollowingOptions(TimeSpan lookbackPeriod, int groupByCount, TimeSpan holdPeriod,
            TimeSpan candleFeedInterval,
            bool shouldCacheCandleFeed)
        {
            LookbackPeriod = lookbackPeriod;
            GroupByCount = groupByCount;
            HoldPeriod = holdPeriod;
            CandleFeedInterval = candleFeedInterval;
            ShouldCacheCandleFeed = shouldCacheCandleFeed;
        }

        public TimeSpan LookbackPeriod { get; }
        public int GroupByCount { get; }
        public TimeSpan HoldPeriod { get; }
        public TimeSpan CandleFeedInterval { get; }
        public bool ShouldCacheCandleFeed { get; }

        public override string ToString()
            => $"{LookbackPeriod}.{HoldPeriod.TotalMinutes}";
    }
}