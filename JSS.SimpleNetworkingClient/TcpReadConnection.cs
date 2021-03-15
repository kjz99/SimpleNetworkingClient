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
    public class TcpReadConnection : TcpConnectionBase, IDisposable
    {
        private readonly int _defaultBufferSize = 1024;
        private readonly int _port;
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
        public TcpReadConnection(ISimpleNetworkingClientLogger logger, int port, TimeSpan sendReadTimeout, int bufferSize, IList<byte> stxCharacters = null, IList<byte> etxCharacters = null) : base(logger, sendReadTimeout, bufferSize)
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

                        if (!_tcpListener.Pending())
                        {
                            // No pending requests are available
                            await Task.Delay(100);
                            continue;
                        }
                        else if (_tcpClient != null)
                        {
                            // There is another pending request, but the current implementation does not support multiple connections
                            continue;
                        }

                        _logger?.Verbose($"New pending connection has been received on port {_port}");
                        _tcpListener.BeginAcceptTcpClient(ar =>
                        {
                            _tcpClient = ((TcpListener)ar.AsyncState).EndAcceptTcpClient(ar);
                            while (true)
                            {
                                // Poll returns true if data is available or the connection is closed
                                var pollResult = _tcpClient.Client.Poll(-1, SelectMode.SelectRead);
                                if (pollResult && _tcpClient.Client.Available == 0)
                                {
                                    // Connection has been closed by the remote party
                                    _logger?.Verbose($"Connection has been closed by the remote party");
                                    DisposeCurrentTcpClient();
                                    break;
                                }
                                else if (pollResult && _tcpClient.Client.Available > 0)
                                {
                                    // Data is available
                                    var receivedData = ReadTcpData(_stxCharacters, _etxCharacters);
                                    _logger?.Verbose($"Tcp Listener on port '{_port}' received the following data: {receivedData}");
                                    OnDataReceived?.Invoke(receivedData);
                                    if (OnDataReceived == null)
                                        _logger?.Warn($"Property {nameof(OnDataReceived)} not set. Ignoring data that has been received thus far");
                                }
                                else
                                {
                                    // pollResult is false, indicating the connection is not readable. Treat it as dead and reestablish the connection.
                                    var errorState = _tcpClient.Client.Poll(1, SelectMode.SelectError);
                                    var writeState = _tcpClient.Client.Poll(1, SelectMode.SelectWrite);
                                    _logger?.Verbose($"Connection is not readable and will be treated as dead. Poll states: SelectError={errorState}, SelectRead={pollResult}, SelectWrite={writeState}");
                                    DisposeCurrentTcpClient();
                                    break;
                                }
                            }
                        }, _tcpListener);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger?.Verbose($"{nameof(TcpReadConnection)} Tcp Listener task has been successfully cancelled");
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
            _tcpListener?.Stop();
            _cancellationTokenSource.Cancel();
            _listenerTask.Wait(_sendReadTimeout);
            base.Dispose();
        }
    }
}
