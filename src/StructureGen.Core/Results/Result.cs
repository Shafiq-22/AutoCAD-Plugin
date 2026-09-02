using System;

namespace StructureGen.Core.Results
{
    /// <summary>
    /// The outcome of an operation that either succeeds or fails with a user-facing reason.
    /// </summary>
    public class Result
    {
        /// <summary>Initialises a new instance of the <see cref="Result"/> class.</summary>
        /// <param name="isSuccess">Whether the operation succeeded.</param>
        /// <param name="errorCode">The stable failure code, or <see langword="null"/> on success.</param>
        /// <param name="errorMessage">The user-facing failure message, or <see langword="null"/> on success.</param>
        /// <param name="report">An optional validation report carrying detail.</param>
        protected Result(bool isSuccess, string? errorCode, string? errorMessage, ValidationReport? report)
        {
            IsSuccess = isSuccess;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            Report = report;
        }

        /// <summary>Gets a value indicating whether the operation succeeded.</summary>
        public bool IsSuccess { get; }

        /// <summary>Gets a value indicating whether the operation failed.</summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>Gets the stable failure code, or <see langword="null"/> on success.</summary>
        public string? ErrorCode { get; }

        /// <summary>Gets the user-facing failure message, or <see langword="null"/> on success.</summary>
        public string? ErrorMessage { get; }

        /// <summary>Gets an optional validation report carrying additional detail.</summary>
        public ValidationReport? Report { get; }

        /// <summary>Creates a successful result.</summary>
        /// <returns>The result.</returns>
        public static Result Success() => new(true, null, null, null);

        /// <summary>Creates a failed result.</summary>
        /// <param name="code">The stable failure code.</param>
        /// <param name="message">The user-facing failure message.</param>
        /// <param name="report">An optional validation report carrying detail.</param>
        /// <returns>The result.</returns>
        public static Result Failure(string code, string message, ValidationReport? report = null) =>
            new(false, code, message, report);

        /// <summary>Describes the failure, or reports success.</summary>
        /// <returns>A short human-readable description.</returns>
        public override string ToString() => IsSuccess ? "Success" : $"[{ErrorCode}] {ErrorMessage}";
    }

    /// <summary>The outcome of an operation that yields a value when it succeeds.</summary>
    /// <typeparam name="T">The type of the produced value.</typeparam>
    public sealed class Result<T> : Result
    {
        private readonly T _value;

        private Result(bool isSuccess, T value, string? errorCode, string? errorMessage, ValidationReport? report)
            : base(isSuccess, errorCode, errorMessage, report)
        {
            _value = value;
        }

        /// <summary>Gets the produced value.</summary>
        /// <exception cref="InvalidOperationException">The operation failed, so no value exists.</exception>
        public T Value => IsSuccess
            ? _value
            : throw new InvalidOperationException($"No value is available because the operation failed: {ErrorMessage}");

        /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
        /// <param name="value">The produced value.</param>
        /// <returns>The result.</returns>
        public static Result<T> Success(T value) => new(true, value, null, null, null);

        /// <summary>Creates a failed result.</summary>
        /// <param name="code">The stable failure code.</param>
        /// <param name="message">The user-facing failure message.</param>
        /// <param name="report">An optional validation report carrying detail.</param>
        /// <returns>The result.</returns>
        public static new Result<T> Failure(string code, string message, ValidationReport? report = null) =>
            new(false, default!, code, message, report);

        /// <summary>Returns the value on success, or <paramref name="fallback"/> on failure.</summary>
        /// <param name="fallback">The value to return when the operation failed.</param>
        /// <returns>The value or the fallback.</returns>
        public T ValueOr(T fallback) => IsSuccess ? _value : fallback;
    }
}
