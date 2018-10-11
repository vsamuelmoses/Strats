using System;

namespace Carvers.Infra.Result
{
    public class Result<T>
    {
        private readonly T _value;
        private readonly Exception _error;

        private Result(T value)
        {
            _value = value;
            IsFailure = false;
        }

        private Result(Exception error)
        {
            _error = error;
            IsFailure = true;
        }

        public T Value
        {
            get
            {
                if(IsFailure)
                    throw new Exception("Cannot retrieve 'Value' for a failure result");

                return _value;
            }
        }

        public bool IsFailure { get; }
        public bool IsSuccess => !IsFailure;

        public Exception Error
        {
            get
            {
                if(!IsFailure)
                    throw new Exception("Cannot retrieve 'Error' for a successful Result");
                return _error;
            }
        }

        public static Result<T> ToSuccess(T value)
            => new Result<T>(value);

        public static Result<T> ToFailure(Exception error)
            => new Result<T>(error);

        public static Result<T> Try(Func<T> func)
        {
            try
            {
                return func().ToSuccess();
            }
            catch (Exception e)
            {
                return Result<T>.ToFailure(e);
            }
        }
    }
}
