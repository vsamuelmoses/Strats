using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Carvers.Models.Indicators
{
    public interface IIndicator
    {
        string Description { get; }
        bool HasValidValue { get; }
    }

    public static class IndicatorExtensions
    {
        public static T OfType<T>(this IIndicator indicator)
            => (T) indicator;
        

    }

    public abstract class BaseIndicator<TOut> : IIndicator
    {
        protected BaseIndicator(string description,
            TOut value, 
            DateTimeOffset timeStamp)
        {
            Description = description;
            Value = value;
            TimeStamp = timeStamp;
        }
        public string Description { get; }
        public abstract bool HasValidValue { get; }
        public TOut Value { get; protected set; }
        public DateTimeOffset TimeStamp { get; }
    }

    public abstract class CandleBasedIndicator<TOut> : BaseIndicator<TOut>
    {
        public Candle Candle { get; }

        protected CandleBasedIndicator(string description, 
            TOut value,
            Candle candle) 
            : base(description, value, candle.TimeStamp)
        {
            Candle = candle;
        }
    }

    public interface IIndicatorStreamingService<T>
        where T : IIndicator
    {
        IObservable<T> Stream { get; }
    }



    public class MovingAverageStreamingService 
        : IIndicatorStreamingService<MovingAverage>
    {
        private readonly Subject<MovingAverage> stream;
        public MovingAverageStreamingService(
            string description,
            IObservable<Candle> candleStream,
            Func<Candle, double> candleToValue, 
            int length)
        {
            stream = new Subject<MovingAverage>();
            MovingAverage = MovingAverage.Construct(description, length);

            candleStream.Subscribe(candle =>
            {
                MovingAverage = MovingAverage.Push(MovingAverage, candleToValue(candle), candle.TimeStamp);
                stream.OnNext(MovingAverage);
            });
        }

        public MovingAverage MovingAverage { get; private set; }
        public IObservable<MovingAverage> Stream => stream;
    }
}
