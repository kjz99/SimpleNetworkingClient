using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JSS.SimpleNetworkingClient.Interfaces;
using log4net;
using log4net.Config;
using log4net.Repository;
using log4net.Util;

namespace JSS.SimpleNetworkingClient.Logging.Log4net
{
    /// <summary>
    /// Initializes a new Log4net logger instance for use with the SimpleNetworkingClient
    /// </summary>
    /// <remarks>
    /// See the readme.md for sample usage scenarios
    /// </remarks>
    public class Log4netLogger : ISimpleNetworkingClientLogger
    {
        private readonly ILog _logger;

        public Log4netLogger(string repository, string loggerName)
        {
            _logger = LogManager.GetLogger(repository, loggerName);
        }

        public Log4netLogger(string loggerName)
        {
            if (!LogManager.GetAllRepositories().Any())
                throw new InvalidOperationException("LogManager has no initialized repositories. Please call XmlConfigurator.Configure(\"Logging repo\", \"loggingSublevel\") before using this constructor");

            _logger = LogManager.GetLogger(LogManager.GetAllRepositories().First().Name, loggerName);
        }

        public void Debug(string message)
        {
            _logger.Debug(message);
        }

        public void Verbose(Func<string> verboseAction)
        {
            if (_logger.IsDebugEnabled)
                _logger.Debug(verboseAction?.Invoke());
        }

        public void Info(string message)
        {
            _logger.Info(message);
        }

        public void Warn(string message, Exception ex = null)
        {
            _logger.Warn(message, ex);
        }

        public void Error(string message, Exception ex = null)
        {
            _logger.Error(message, ex);
        }

        public void Fatal(string message, Exception ex = null)
        {
            _logger.FatalExt(message, ex);
        }
    }
}