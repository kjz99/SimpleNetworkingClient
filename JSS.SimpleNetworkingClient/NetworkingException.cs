using System;
using System.Collections.Generic;
using System.Text;

namespace JSS.SimpleNetworkingClient
{
    /// <summary>
    /// Networking exception thrown by the simple networking client
    /// </summary>
    public class NetworkingException : Exception
    {
        public NetworkingException(string message, NetworkingExceptionTypeEnum exceptionType) : base(message)
        {
            ExceptionType = exceptionType;
        }

        public NetworkingException(string message, NetworkingExceptionTypeEnum exceptionType, Exception innerException) : base(message, innerException)
        {
            ExceptionType = exceptionType;
        }

        /// <summary>
        /// The type of networking exception as described by the <see cref="NetworkingExceptionTypeEnum"/> enum
        /// </summary>
        public NetworkingExceptionTypeEnum ExceptionType { get; set; }

        public enum NetworkingExceptionTypeEnum
        {
            /// <summary>
            /// The first 4 bytes of tcp data are incomplete and the data length cannot be determined
            /// </summary>
            InvalidDataStreamLength,
            /// <summary>
            /// More or less data has been received from the remote party than should have been received according to the tcp length header
            /// </summary>
            MoreOrLessDataReceived,
            /// <summary>
            /// Failed to establish a new connection to the remote party
            /// </summary>
            ConnectionSetupFailed,
            /// <summary>
            /// The socket has an error from which it cannot reliably recover
            /// </summary>
            SocketError,
            /// <summary>
            /// The remote party has prematurely closed and aborted the connection
            /// </summary>
            ConnectionAbortedPrematurely,
            /// <summary>
            /// Reading from the socket has timed out
            /// </summary>
            ReadTimeout,
            /// <summary>
            /// Cannot open a listening socket on the given port or the listener has failed due to an unhandled exception
            /// </summary>
            ListeningError,
            /// <summary>
            /// Timeout waiting for the socket to become ready for writing any data
            /// </summary>
            WriteTimeout
        }
    }
}
