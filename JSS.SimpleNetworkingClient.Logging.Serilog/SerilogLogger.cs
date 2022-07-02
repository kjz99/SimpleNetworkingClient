using JSS.SimpleNetworkingClient.Interfaces;
using System;
using Serilog;

namespace JSS.SimpleNetworkingClient.Logging.Serilog
{
    /// <summary>
    /// Initializes a new Serilog logger instance for use with the SimpleNetworkingClient
    /// </summary>
    /// <remarks>
    /// See the readme.md for sample usage scenarios
    /// </remarks>
    public class SerilogLogger: ISimpleNetworkingClientLogger
    {
        private readonly ILogger _logger;

        public SerilogLogger(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Logs a debug message to the configured logger
        /// </summary>
        public void Debug(string message)
        {
            _logger.Debug(message);
        }

        /// <summary>
        /// logs a if verbose message if verbose logging has been enabled
        /// </summary>
        /// <param name="message"></param>
        public void Verbose(string message)
        {
            _logger.Verbose(message);
        }

        /// <summary>
        /// Logs a info message to the configured logger
        /// </summary>
        public void Info(string message)
        {
            _logger.Information(message);
        }

        /// <summary>
        /// Logs a warning message to the configured logger
        /// </summary>
        public void Warn(string message, Exception ex = null)
        {
            _logger.Warning(ex, message);
        }

        /// <summary>
        /// Logs a error message to the configured logger
        /// </summary>
        public void Error(string message, Exception ex = null)
        {
            _logger.Error(ex, message);
        }

        /// <summary>
        /// Logs a fatal message to the configured logger
        /// </summary>
        public void Fatal(string message, Exception ex = null)
        {
            _logger.Fatal(ex, message);
        }
    }
}
