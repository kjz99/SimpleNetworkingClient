using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace JSS.SimpleNetworkingClient
{
    public class TcpReadConnection : TcpConnectionBase
    {
        private readonly int _defaultBufferSize = 1024;
        private readonly int _port;
        private Task _listenerTask;
        private CancellationTokenSource _cancellationTokenSource;
        private TcpListener _tcpListener;

        // TODO: implement universal logging

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
            if (OnDataReceived == null)
                throw new ArgumentException("Set OnDataReceived before calling StartListening()");

            _cancellationTokenSource = new CancellationTokenSource();
            _listenerTask = Task.Run(ConnectionListenerImpl, _cancellationTokenSource.Token);
        }

        private void ConnectionListenerImpl()
        {
            while (true)
            {
                // Start listening for the remote party
                try
                {
                    // Open the listening socket
                    _tcpListener = new TcpListener(IPAddress.Any, _port);
                    _tcpListener.Start();

                    while (true)
                    {
                        // Wait for a new incoming connection or a cancellation
                        var asyncAcceptResult = _tcpListener.BeginAcceptTcpClient(ar => { }, _tcpListener);
                        Task.Factory.FromAsync(asyncAcceptResult, result =>
                        {
                            DisposeCurrentTcpClient();
                            _tcpClient = _tcpListener.EndAcceptTcpClient(result);
                            OnDataReceived(ReadTcpData(_stxCharacters, _etxCharacters));
                        }).Wait(_cancellationTokenSource.Token);

                        _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    }
                }
                catch (TaskCanceledException tcEx)
                {
                    return;
                }
                catch (Exception ex)
                {
                    throw new NetworkingException($"Failed to listen on local port {_port}. Make sure the port is not blocked or in use by another application", NetworkingException.NetworkingExceptionTypeEnum.ListeningError, ex);
                }
            }
        }

        /// <summary>
        /// Action that is executed when new data has been received
        /// </summary>
        public Action<string> OnDataReceived { get; set; }


    }
}
