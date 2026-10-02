namespace PawStash.BLL.Results
{
    public class ServiceResult
    {
        protected ServiceResult(
            ServiceErrorType? errorType,
            string? errorMessage,
            IDictionary<string, string[]>? fieldErrors)
        {
            ErrorType = errorType;
            ErrorMessage = errorMessage;
            FieldErrors = fieldErrors ?? new Dictionary<string, string[]>();
        }

        public ServiceErrorType? ErrorType { get; }

        public string? ErrorMessage { get; }

        public IDictionary<string, string[]> FieldErrors { get; }

        public bool IsSuccess => ErrorType is null;

        public static ServiceResult Success()
        {
            return new ServiceResult(null, null, null);
        }

        public static ServiceResult Fail(ServiceErrorType errorType, string errorMessage)
        {
            return new ServiceResult(errorType, errorMessage, null);
        }

        public static ServiceResult Invalid(IDictionary<string, string[]> fieldErrors)
        {
            return new ServiceResult(ServiceErrorType.Validation, null, fieldErrors);
        }
    }

    public class ServiceResult<T> : ServiceResult
    {
        private ServiceResult(
            T? data,
            ServiceErrorType? errorType,
            string? errorMessage,
            IDictionary<string, string[]>? fieldErrors)
            : base(errorType, errorMessage, fieldErrors)
        {
            Data = data;
        }

        public T? Data { get; }

        public static ServiceResult<T> Success(T data)
        {
            return new ServiceResult<T>(data, null, null, null);
        }

        public static new ServiceResult<T> Fail(ServiceErrorType errorType, string errorMessage)
        {
            return new ServiceResult<T>(default, errorType, errorMessage, null);
        }

        public static new ServiceResult<T> Invalid(IDictionary<string, string[]> fieldErrors)
        {
            return new ServiceResult<T>(default, ServiceErrorType.Validation, null, fieldErrors);
        }

        public static ServiceResult<T> From(ServiceResult failure)
        {
            return new ServiceResult<T>(default, failure.ErrorType, failure.ErrorMessage, failure.FieldErrors);
        }
    }
}
