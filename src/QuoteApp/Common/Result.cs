namespace QuoteApp.Common;

public abstract class Result<T>
{
    private Result() { }

    public abstract TOut Match<TOut>(Func<T, TOut> onSuccess, Func<AppError, TOut> onFailure);

    public sealed class Success : Result<T>
    {
        public T Value { get; }

        internal Success(T value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value), "A successful Result must carry a value.");
            Value = value;
        }

        public override TOut Match<TOut>(Func<T, TOut> onSuccess, Func<AppError, TOut> onFailure) => onSuccess(Value);
    }

    public sealed class Failure : Result<T>
    {
        public AppError Error { get; }

        internal Failure(AppError error)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error), "A failed Result must carry an error.");
        }

        public override TOut Match<TOut>(Func<T, TOut> onSuccess, Func<AppError, TOut> onFailure) => onFailure(Error);
    }

    public override string ToString() =>
        Match(v => $"Success({v})", e => $"Failure({e})");
}

public static class Result
{
    public static Result<T> Ok<T>(T value) => new Result<T>.Success(value);
    public static Result<T> Fail<T>(AppError error) => new Result<T>.Failure(error);
}

public static class ResultExtensions
{
    public static Result<U> Map<T, U>(this Result<T> result, Func<T, U> f) =>
        result.Match(
            value => Result.Ok(f(value)),
            error => Result.Fail<U>(error));

    public static Result<U> Bind<T, U>(this Result<T> result, Func<T, Result<U>> f) =>
        result.Match(
            value => f(value),
            error => Result.Fail<U>(error));
}
