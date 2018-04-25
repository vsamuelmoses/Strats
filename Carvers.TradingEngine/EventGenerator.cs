using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Carvers.TradingEngine
{
    public static class EventGenerator
    {
        public static IConnectableObservable<DateTimeOffset> Ticks(EngineConfig config)
        {
            var startTime = config.Start;
            var span = config.End - config.Start;
            var numOfEvents = (int)(span.Ticks/config.TickEventSpan.Ticks);

            return Observable
                .Range(0, numOfEvents, TaskPoolScheduler.Default)
                .Select(val => new DateTimeOffset(startTime.Ticks + val*config.TickEventSpan.Ticks, TimeSpan.Zero))
                .Publish();
        }
    }
}