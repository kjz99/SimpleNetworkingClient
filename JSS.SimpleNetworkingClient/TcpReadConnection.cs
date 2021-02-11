﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

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
        /// <param name="port">Port on which to listen for incoming connections</param>
        /// <param name="sendReadTimeout">Send/Read timeout</param>
        /// <param name="bufferSize">Size of the tcp buffer that determines the amount of bytes that is received/send per chunk</param>
        /// <param name="stxCharacters">Begin of transmission characters, Eg 0x02 for ASCII char STX. Set to null to disable to disable adding/removing stx characters.</param>
        /// <param name="etxCharacters">End of transmission characters, Eg 0x03 for ASCII char ETX. Set to null to disable end of transmission checking.</param>
        public TcpReadConnection(int port, TimeSpan sendReadTimeout, int bufferSize, IList<byte> stxCharacters = null, IList<byte> etxCharacters = null) : base(sendReadTimeout, bufferSize)
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
                    // Open the listening socket and start listening for the remote party
                    _logger?.Debug($"Attempting to start {nameof(TcpReadConnection)} Tcp Listener task");
                    _tcpListener = new TcpListener(IPAddress.Any, _port);
                    _tcpListener.Start();
                    _logger?.Debug($"{nameof(TcpReadConnection)} Tcp Listener task has been started successfully");

                    while (true)
                    {
                        // Wait for a new incoming connection or a cancellation
                        var asyncAcceptResult = _tcpListener.BeginAcceptTcpClient(ar => { }, _tcpListener);
                        Task.Factory.FromAsync(asyncAcceptResult, result =>
                        {
                            _tcpClient = _tcpListener.EndAcceptTcpClient(result);
                            OnDataReceived.Invoke(ReadTcpData(_stxCharacters, _etxCharacters));
                            if (OnDataReceived == null)
                                _logger?.Error($"Property {nameof(OnDataReceived)} not set. Ignoring data that has been received");
                        }).Wait(_cancellationTokenSource.Token);

                        _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger?.Debug($"{nameof(TcpReadConnection)} Tcp Listener task has been successfully cancelled");
                    return;
                }
                catch (Exception ex)
                {
                    _logger?.Error("TcpReadConnection.ConnectionListenerImpl() failed", new NetworkingException($"Failed to listen on local port {_port}. Make sure the port is not blocked or in use by another application", NetworkingException.NetworkingExceptionTypeEnum.ListeningError, ex));
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
            }
        }

        /// <summary>
        /// Action that is executed when new data has been received
        /// </summary>
        public Action<string> OnDataReceived { get; set; }

        public new void Dispose()
        {
            _tcpListener?.Stop();
            _cancellationTokenSource.Cancel();
            _listenerTask.Wait(_sendReadTimeout);
            base.Dispose();
        }
    }
}
