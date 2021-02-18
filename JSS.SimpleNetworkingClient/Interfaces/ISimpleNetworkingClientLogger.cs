using System;
using System.Collections.Generic;
using System.Text;

namespace JSS.SimpleNetworkingClient.Interfaces
{
    public interface ISimpleNetworkingClientLogger
    {
        /// <summary>
        /// Logs a debug message to the configured logger
        /// </summary>
        void Debug(string message);

        /// <summary>
        /// logs a if verbose message if verbose logging has been enabled
        /// </summary>
        /// <param name="verboseAction"></param>
        void Verbose(Func<string> verboseAction);

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
