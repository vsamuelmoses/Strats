using System;

namespace Carvers.Infra.Result
{
    public static class ResultExtensibility
    {
        public static Result<T> ToSuccess<T>(this T value)
            => Result<T>.ToSuccess(value);

        public static Result<T> ToFailure<T>(this Exception error)
            => Result<T>.ToFailure(error);

        public static Result<T> Match<T>(this Result<T> result, Action<T> onSuccess, Action<Exception> onFailure)
        {
            if (result.IsFailure)
                onFailure(result.Error);
            else
                onSuccess(result.Value);

            return result;
        }

        public static Result<string> EnsureNotNull(this string value)
        {
            return value == null 
                ? Result<string>.ToFailure(new Exception("String value is null.")) 
                : Result<string>.ToSuccess(value);
        }

        public static Result<T> EnsureNotNull<T>(this T value)
        {
            return value == null
                ? Result<T>.ToFailure(new Exception("Value is null."))
                : Result<T>.ToSuccess(value);
        }
        public static Result<T> TryCast<T>(this object value)
            where T : class
        {
            var valueAfterCast = value as T;
            return valueAfterCast == null
                ? Result<T>.ToFailure(new Exception($"Error casting value to type {typeof(T)}"))
                : Result<T>.ToSuccess(valueAfterCast);
        }
        public static Result<TOut> Select<TIn, TOut>(this Result<TIn> input, Func<TIn, TOut> map, Func<Exception, Exception> onError = null)
        {
            if (onError == null)
                onError = e => e;

            return input.IsSuccess
                ? Result<TOut>.Try(() => map(input.Value))
                : Result<TOut>.ToFailure(onError(new Exception($"Error applying map function to failed Result type {input}")));
        }

        public static Result<TOut> Cast<TIn, TOut>(this Result<TIn> value)
            where TIn : class 
            where TOut : class
        {
            return value.IsSuccess 
                ? value.Value.TryCast<TOut>() 
                : Result<TOut>.ToFailure(new Exception($"Error casting failed result to type {typeof(TOut)}"));
        }

        public static Result<Tuple<T1, T2>> Aggregate<T1, T2>(this Result<T1> value1, Result<T2> value2)
        {
            if (value1.IsSuccess && value2.IsSuccess)
                return Tuple.Create(value1.ValueOrDefault(), value2.ValueOrDefault()).ToSuccess();

            return Result<Tuple<T1, T2>>.ToFailure(new Exception("Aggregation of results failed"));
        }

        public static Result<Tuple<T1, T2>> Aggregate<T1, T2>(this Result<T1> value1, Result<T2> value2,
            Func<T1, T2, bool> condition)
        {
            var aggregate = value1.Aggregate(value2);
            var isAggregateSuccess = aggregate.Select(tup => condition(tup.Item1, tup.Item2)).ValueOrDefault();

            return isAggregateSuccess 
                ? aggregate
                : Result<Tuple<T1, T2>>.ToFailure(new Exception("Aggregation of results failed"));
        }

        public static Result<T3> Aggregate<T1, T2, T3>(this Result<T1> value1, Result<T2> value2,
            Func<T1, T2, T3> composer)
        {
            return value1
                .Aggregate(value2)
                .Select(tup => composer(tup.Item1, tup.Item2));
        }

        public static bool IsValueEqual<T>(this Result<T> result, T value)
            where T: class
        {
            return result.IsSuccess && result.ValueOrDefault() == value;
        }


        public static T ValueOrDefault<T>(this Result<T> input, T defaultOutput = default(T))
        {
            return input.IsSuccess ? input.Value : defaultOutput;
        }

        public static T ValueOrDefault<T>(this Result<T> input, Func<T> defaultOutput)
        {
            return input.IsSuccess ? input.Value : defaultOutput();
        }
    }
}