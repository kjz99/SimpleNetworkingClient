using System;
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
        /// <param name="settings">Settings for the tcp client</param>
        public TcpReadConnectionDeadlockSimulator(TcpClientSettings settings) : base(settings)
        {
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

                        if (_pendingRequestActive && TcpClient == null)
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
                            Settings.Logger?.Warn($"A second pending request has been detected on port {_port}, which is not supported. The request will be ignored until the other request has ended");
                            continue;
                        }

                        Settings.Logger?.Verbose($"New pending connection has been received on port {_port}");
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
                                Settings.Logger?.Warn($"Failed to process BeginAcceptTcpClient async result. Connection has been closed/disposed abnormally by the app, OS, remote party, virus scanner, IDS ed.", ex);
                                DisposeCurrentTcpClient();
                            }
                        }, _tcpListener);
                    }
                }
                catch (OperationCanceledException)
                {
                    Settings.Logger?.Verbose($"{nameof(TcpReadConnection)} Tcp Listener task has been successfully cancelled");
                    StopTcpListener();
                    return;
                }
                catch (Exception ex)
                {
                    if (ex.InnerException != null && ex.InnerException.GetType() == typeof(NetworkingException))
                    {
                        Settings.Logger?.Error("Networking Exception has been received", ex.InnerException);
                    }
                    else
                    {
                        Settings.Logger?.Error("TcpReadConnection.ConnectionListenerImpl() failed", new NetworkingException($"Failed to listen on local port {_port}. Make sure the port is not blocked or in use by another application", NetworkingException.NetworkingExceptionTypeEnum.ListeningError, ex));
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
            Settings.Logger?.Verbose($"Attempting to start {nameof(TcpReadConnection)} Tcp Listener task");
            _tcpListener?.Stop();
            _tcpListener = new TcpListener(IPAddress.Any, _port);
            _tcpListener.Start();
            Settings.Logger?.Debug($"{nameof(TcpReadConnection)} Tcp Listener task on port {_port} has been started successfully");
        }

        /// <summary>
        /// Stops the listening socket and stop listening for the remote party
        /// </summary>
        private void StopTcpListener()
        {
            try
            {
                Settings.Logger?.Verbose($"Attempting to stop {nameof(TcpListener)}");
                DisposeCurrentTcpClient();
                _tcpListener?.Stop();
                _tcpListener = null;
                Settings.Logger?.Verbose($"{nameof(TcpListener)} has stopped listening for new connections");
            }
            catch (Exception ex)
            {
                Settings.Logger?.Error($"Failed to stop {nameof(TcpListener)}", ex);
            }
        }

        public new void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _tcpListener?.Stop();

            try
            {
                _listenerTask.Wait(Settings.SendReadTimeout);
            }
            catch (TaskCanceledException)
            {
                // Task has been cancelled successfully
            }

            base.Dispose();
        }
    }
}
