using System;

namespace KubeMQ.SDK.csharp.Results
{
    /// <summary>
    /// Represents the base class for all result classes in the KubeMQ Unified SDK.
    /// </summary>
    public class Result
    {
        /// <summary>
        /// Gets a value indicating whether the operation was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the error message associated with the result.
        /// </summary>
        public string ErrorMessage { get; set; }

        
        public Result()
        {
            IsSuccess = true;
            ErrorMessage = string.Empty;
        }
        
        public Result(string errorMessage)
        {
            IsSuccess = false;
            ErrorMessage = errorMessage;
        }
        
        public Result(Exception e)
        {
            IsSuccess = false;
            ErrorMessage = e.Message;
        }
        
    }
}