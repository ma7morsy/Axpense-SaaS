namespace Axpense.Service.Common
{
    public enum ServiceErrorType
    {
        Validation,
        NotFound,
        Conflict
    }

    public sealed record ServiceError(ServiceErrorType Type, string Code, string Message, IReadOnlyDictionary<string, string>? Details = null);

    /// <summary>
    /// Outcome of a business operation. Services never throw for expected business failures;
    /// the API layer maps <see cref="ServiceErrorType"/> to the right HTTP status.
    /// </summary>
    public sealed class ServiceResult<T>
    {
        public bool Succeeded => Error is null;
        public T? Value { get; private init; }
        public ServiceError? Error { get; private init; }

        public static ServiceResult<T> Ok(T value) => new() { Value = value };
        public static ServiceResult<T> Fail(ServiceError error) => new() { Error = error };

        public static ServiceResult<T> NotFound(string message) =>
            Fail(new ServiceError(ServiceErrorType.NotFound, "RESOURCE_NOT_FOUND", message));

        public static ServiceResult<T> Conflict(string code, string message, string? field = null) =>
            Fail(new ServiceError(ServiceErrorType.Conflict, code, message,
                field is null ? null : new Dictionary<string, string> { [field] = message }));

        public static ServiceResult<T> Invalid(IReadOnlyDictionary<string, string> errors) =>
            Fail(new ServiceError(ServiceErrorType.Validation, "VALIDATION_FAILED",
                errors.Values.FirstOrDefault() ?? "The request is invalid.", errors));
    }
}
