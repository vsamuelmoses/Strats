using System;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Carvers.TradingEngine.Tests
{
    [TestClass]
    public class EngineGeneratorTests
    {
        private static readonly DateTimeOffset Start = new DateTimeOffset(1986,08,13,0,0,0,TimeSpan.FromHours(5));

        [TestMethod]
        public void StartEvents()
        {
            var wait1 = new ManualResetEventSlim();
            var count1 = 0;
            var generator = EventGenerator.Ticks(new EngineConfig(Start, TimeSpan.FromMinutes(1)));
            var subscription1 = generator
                .Take(5)
                .Subscribe(val => {
                    Debug.WriteLine("1. DTO: " + val);
                    count1++;
                    if(count1 >= 5)
                        wait1.Set();
                }, () => Debug.WriteLine("1. Completed"));

            var disposable = generator.Connect();

            var count2 = 0;
            var maxCount2 = 1000;
            var wait2 = new ManualResetEventSlim();
            var subscription2 = generator.Take(maxCount2)
                .Subscribe(val => {
                    Debug.WriteLine("2. DTO: " + val);
                    count2++;
                    if (count2 >= maxCount2)
                        wait2.Set();
                }, () => Debug.WriteLine("2. Completed"));

            wait1.Wait();
            wait2.Wait();

            subscription1.Dispose();
            subscription2.Dispose();
            disposable.Dispose();
        }
    }


    public static class Log
    {
        public static void DebugDumpThread(string text)
        {
            Debug.WriteLine($"{text}:{Thread.CurrentThread.ManagedThreadId}");
        }
    }
}
