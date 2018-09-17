using System;
using System.Collections.Concurrent;
using Carvers.Infra.Extensions;
using Carvers.Models.Indicators;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Carvers.Models.Tests
{
    [TestClass]
    [DeploymentItem("Microsoft.VisualStudio.TestPlatform.TestFramework.Extensions.dll")]
    public class TrendContinuationTests
    {
       [TestMethod]
        public void WhenCandle1AndCandle2AreGreenAndCandle2ClosesHigherThanCandle1High()
        {
            var startTime = new DateTime(2017, 8, 13, 6, 0, 0);

            var queue = new ConcurrentQueue<Candle>();

            queue.Enqueue(new Candle(new Ohlc(10, 15, 5, 13, 0), startTime, TimeSpan.FromMinutes(1)));
            queue.Enqueue(new Candle(new Ohlc(13, 20, 5, 18, 0), startTime, TimeSpan.FromMinutes(1)));

            Assert.IsTrue(BooleanIndicators.TrendContinuation(queue));

            queue.Enqueue(new Candle(new Ohlc(13, 20, 5, 18, 0), startTime, TimeSpan.FromMinutes(1)));

            Assert.IsFalse(BooleanIndicators.TrendContinuation(queue));

            queue.Enqueue(new Candle(new Ohlc(18, 15, 10, 13, 0), startTime, TimeSpan.FromMinutes(1)));

            Assert.IsFalse(BooleanIndicators.TrendContinuation(queue));

            queue.Enqueue(new Candle(new Ohlc(13, 15, 5, 6, 0), startTime, TimeSpan.FromMinutes(1)));
            Assert.IsTrue(BooleanIndicators.TrendContinuation(queue.TakeLast(2)));

            queue.Enqueue(new Candle(new Ohlc(5, 15, 5, 5, 0), startTime, TimeSpan.FromMinutes(1)));
            Assert.IsFalse(BooleanIndicators.TrendContinuation(queue.TakeLast(2)));
        }
    }
}
