namespace QuoteApp.Common;

public abstract class Maybe<T>
{
    private Maybe() { }

    public abstract TOut Match<TOut>(Func<T, TOut> onSome, Func<TOut> onNone);

    public sealed class Some : Maybe<T>
    {
        public T Value { get; }

        internal Some(T value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value), "Some must carry a value; use None for absence.");
            Value = value;
        }

        public override TOut Match<TOut>(Func<T, TOut> onSome, Func<TOut> onNone) => onSome(Value);
    }

    public sealed class None : Maybe<T>
    {
        internal None() { }
        public override TOut Match<TOut>(Func<T, TOut> onSome, Func<TOut> onNone) => onNone();
    }

    public override string ToString() => Match(v => $"Some({v})", () => "None");
}

public static class Maybe
{
    public static Maybe<T> Some<T>(T value) => new Maybe<T>.Some(value);
    public static Maybe<T> None<T>() => new Maybe<T>.None();
}

public static class MaybeExtensions
{
    public static Maybe<U> Map<T, U>(this Maybe<T> maybe, Func<T, U> f) =>
        maybe.Match(v => Maybe.Some(f(v)), Maybe.None<U>);

    public static Result<T> ToResult<T>(this Maybe<T> maybe, Func<AppError> onMissing) =>
        maybe.Match(Result.Ok, () => Result.Fail<T>(onMissing()));
}
