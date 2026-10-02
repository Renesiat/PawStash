namespace PawStash.BLL.Results
{
    public class ServiceResult<T>
    {
        private ServiceResult(
            T? data,
            ServiceErrorType? errorType,
            string? errorMessage,
            IDictionary<string, string[]>? fieldErrors)
        {
            Data = data;
            ErrorType = errorType;
            ErrorMessage = errorMessage;
            FieldErrors = fieldErrors ?? new Dictionary<string, string[]>();
        }

        public T? Data { get; }

        public ServiceErrorType? ErrorType { get; }

        public string? ErrorMessage { get; }

        public IDictionary<string, string[]> FieldErrors { get; }

        public bool IsSuccess => ErrorType is null;

        public static ServiceResult<T> Success(T data)
        {
            return new ServiceResult<T>(data, null, null, null);
        }

        public static ServiceResult<T> Fail(ServiceErrorType errorType, string errorMessage)
        {
            return new ServiceResult<T>(default, errorType, errorMessage, null);
        }

        public static ServiceResult<T> Invalid(IDictionary<string, string[]> fieldErrors)
        {
            return new ServiceResult<T>(default, ServiceErrorType.Validation, null, fieldErrors);
        }
    }
}
