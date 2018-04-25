using System;

namespace Carvers.TradingEngine
{
    public class EngineConfig
    {
        public EngineConfig(DateTimeOffset start, DateTimeOffset end, TimeSpan tickEventSpan, int spanScale = 1)
        {
            Start = start;
            End = end;
            TickEventSpan = tickEventSpan;
            SpanScale = spanScale;
        }

        public EngineConfig(DateTimeOffset start, TimeSpan tickEventSpan)
            : this(start, DateTimeOffset.Now, tickEventSpan)
        {
        }

        public TimeSpan TickEventSpan { get; } 
        public int SpanScale { get; }
        public DateTimeOffset Start { get; }
        public DateTimeOffset End { get; }
    }
}
