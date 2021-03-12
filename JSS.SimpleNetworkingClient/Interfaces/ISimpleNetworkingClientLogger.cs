using System;

namespace JSS.SimpleNetworkingClient.Interfaces
{
    /// <summary>
    /// Logger interface used by one of the loggers in the JSS.SimpleNetworkingClient.Logging namespace to implement a specific logging infrastructure like eg; log4net
    /// </summary>
    public interface ISimpleNetworkingClientLogger
    {
        /// <summary>
        /// Logs a debug message to the configured logger
        /// </summary>
        void Debug(string message);

        /// <summary>
        /// logs a if verbose message if verbose logging has been enabled
        /// </summary>
        /// <param name="message"></param>
        void Verbose(string message);

        /// <summary>
        /// Logs a info message to the configured logger
        /// </summary>
        void Info(string message);

        /// <summary>
        /// Logs a warning message to the configured logger
        /// </summary>
        void Warn(string message, Exception ex = null);

        /// <summary>
        /// Logs a error message to the configured logger
        /// </summary>
        void Error(string message, Exception ex = null);

        /// <summary>
        /// Logs a fatal message to the configured logger
        /// </summary>
        void Fatal(string message, Exception ex = null);
    }
}
