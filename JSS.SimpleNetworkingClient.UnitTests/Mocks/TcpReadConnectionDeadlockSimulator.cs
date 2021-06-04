using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using JSS.SimpleNetworkingClient.Interfaces;

namespace JSS.SimpleNetworkingClient
{
    /// <summary>
    /// Manages a new tcp listening connection on the given port.
    /// Warning; please make sure that you always dispose of this class so that unmanaged resources will be released.
    /// This implementation is guaranteed to be thread safe because the tcp listener and tcp IO operations run on a separate thread.
    /// </summary>
    /// <see cref="https://github.com/kjz99/SimpleNetworkingClient" />
    public class TcpReadConnectionDeadlockSimulator : TcpConnectionBase, IDisposable
    {
        private readonly int _defaultBufferSize = 1024;
        private readonly int _port;
        private bool _pendingRequestActive = false;
        private Task _listenerTask;
        private CancellationTokenSource _cancellationTokenSource;
        private TcpListener _tcpListener;

        /// <summary>
        /// Ctor; Starts a new tcp listener 
        /// </summary>
        /// <param name="logger">Logger instance that implements ISimpleNetworkingClientLogger for diagnostic logging</param>
        /// <param name="port">Port on which to listen for incoming connections</param>
        /// <param name="sendReadTimeout">Send/Read timeout</param>
        /// <param name="bufferSize">Size of the tcp buffer that determines the amount of bytes that is received/send per chunk</param>
        /// <param name="stxCharacters">Begin of transmission characters, Eg 0x02 for ASCII char STX. Set to null to disable to disable adding/removing stx characters.</param>
        /// <param name="etxCharacters">End of transmission characters, Eg 0x03 for ASCII char ETX. Set to null to disable end of transmission checking.</param>
        public TcpReadConnectionDeadlockSimulator(ISimpleNetworkingClientLogger logger, int port, TimeSpan sendReadTimeout, int bufferSize, IList<byte> stxCharacters = null, IList<byte> etxCharacters = null) : base(logger, sendReadTimeout, bufferSize)
        {
            _port = port;
            _stxCharacters = stxCharacters;
            _etxCharacters = etxCharacters;
        }

        /// <summary>
        /// Start listening for new incoming connections
        /// </summary>
        public void StartListening()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _listenerTask = Task.Run(async () => await ConnectionListenerImpl(), _cancellationTokenSource.Token);
        }

        private async Task ConnectionListenerImpl()
        {
            while (true)
            {
                try
                {
                    if (_tcpListener == null)
                        StartTcpListener();

                    while (true)
                    {
                        _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                        if (_pendingRequestActive && _tcpClient == null)
                            // tcp client has been disposed, indicating the last request has ended and the connection has been closed
                            _pendingRequestActive = false;

                        if (!_tcpListener.Pending() || _pendingRequestActive)
                        {
                            // No pending requests are available or a pending request is being handled
                            await Task.Delay(100);
                            continue;
                        }

                        if (!_pendingRequestActive)
                        {
                            // A new pending request has been detected, log it
                            _pendingRequestActive = true;
                            _logger?.Warn($"A second pending request has been detected on port {_port}, which is not supported. The request will be ignored until the other request has ended");
                            continue;
                        }

                        _logger?.Verbose($"New pending connection has been received on port {_port}");
                        _tcpListener.BeginAcceptTcpClient(ar =>
                        {
                            try
                            {
                                // Deadlock the listener
                                Task.Delay(TimeSpan.MaxValue).Wait();
                                //_tcpClient = ((TcpListener)ar.AsyncState).EndAcceptTcpClient(ar);
                            }
                            catch (Exception ex)
                            {
                                // This exception case usually should not happen, even during tcp errors and frequently indicates a premature disposal of the tcp socket
                                // The premature disposal can also be triggered by the OS if it force closes the connection due to an unhandled error
                                _logger?.Warn($"Failed to process BeginAcceptTcpClient async result. Connection has been closed/disposed abnormally by the app, OS, remote party, virus scanner, IDS ed.", ex);
                                DisposeCurrentTcpClient();
                            }
                        }, _tcpListener);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger?.Verbose($"{nameof(TcpReadConnection)} Tcp Listener task has been successfully cancelled");
                    StopTcpListener();
                    return;
                }
                catch (Exception ex)
                {
                    if (ex.InnerException != null && ex.InnerException.GetType() == typeof(NetworkingException))
                    {
                        _logger?.Error("Networking Exception has been received", ex.InnerException);
                    }
                    else
                    {
                        _logger?.Error("TcpReadConnection.ConnectionListenerImpl() failed", new NetworkingException($"Failed to listen on local port {_port}. Make sure the port is not blocked or in use by another application", NetworkingException.NetworkingExceptionTypeEnum.ListeningError, ex));
                        StopTcpListener();
                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }
                }
            }
        }

        /// <summary>
        /// Action that is executed when new data has been received
        /// </summary>
        public Action<string> OnDataReceived { get; set; }

        /// <summary>
        /// Open the listening socket and start listening for the remote party
        /// </summary>
        private void StartTcpListener()
        {
            _logger?.Verbose($"Attempting to start {nameof(TcpReadConnection)} Tcp Listener task");
            _tcpListener?.Stop();
            _tcpListener = new TcpListener(IPAddress.Any, _port);
            _tcpListener.Start();
            _logger?.Debug($"{nameof(TcpReadConnection)} Tcp Listener task on port {_port} has been started successfully");
        }

        /// <summary>
        /// Stops the listening socket and stop listening for the remote party
        /// </summary>
        private void StopTcpListener()
        {
            try
            {
                _logger?.Verbose($"Attempting to stop {nameof(TcpListener)}");
                DisposeCurrentTcpClient();
                _tcpListener?.Stop();
                _tcpListener = null;
                _logger?.Verbose($"{nameof(TcpListener)} has stopped listening for new connections");
            }
            catch (Exception ex)
            {
                _logger?.Error($"Failed to stop {nameof(TcpListener)}", ex);
            }
        }

        public new void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _tcpListener?.Stop();

            try
            {
                _listenerTask.Wait(_sendReadTimeout);
            }
            catch (TaskCanceledException)
            {
                // Task has been cancelled successfully
            }

            base.Dispose();
        }
    }
}
