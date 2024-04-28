namespace KubeMQ.SDK.csharp.Unified.Results
{
    /// <summary>
    /// Represents the base class for all result classes in the KubeMQ Unified SDK.
    /// </summary>
    public class BaseResult
    {
        /// <summary>
        /// Gets a value indicating whether the operation was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the error message associated with the result.
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}