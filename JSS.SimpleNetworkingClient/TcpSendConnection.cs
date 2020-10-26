using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace JSS.SimpleNetworkingClient
{
    /// <summary>
    /// Defines a TCP send connection that can be used to send data to a remote party
    /// </summary>
    public class TcpSendConnection : TcpConnectionBase, IDisposable
    {
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);
        private readonly int _defaultBufferSize = 1024;
        private readonly string _host;
        private readonly int _port;
        private TcpClient _tcpClient;

        // TODO: implement universal logging

        /// <summary>
        /// Ctor; Establishes a new connection to the remote party for sending and or receiving data
        /// </summary>
        /// <param name="host">Hostname or ip address to connect to</param>
        /// <param name="port">Port to use</param>
        public TcpSendConnection(string host, int port)
        {
            _sendReadTimeout = _defaultTimeout;
            _bufferSize = _defaultBufferSize;
            _host = host;
            _port = port;
            StartConnection();
        }

        /// <summary>
        /// Ctor; Establishes a new connection to the remote party for sending and or receiving data
        /// </summary>
        /// <param name="host">Hostname or ip address to connect to</param>
        /// <param name="port">Port to use</param>
        /// <param name="sendReadTimeout">Send/Read timeout</param>
        /// <param name="bufferSize">Size of the tcp buffer that determines the amount of bytes that is received/send per chunk</param>
        public TcpSendConnection(string host, int port, TimeSpan sendReadTimeout, int bufferSize) : base(sendReadTimeout, bufferSize)
        {
            _host = host;
            _port = port;
            StartConnection();
        }

        /// <summary>
        /// Starts the connection to the remote party
        /// </summary>
        private void StartConnection()
        {
            //TODO: implement ip address validation

            try
            {
                _tcpClient = new TcpClient();
                _tcpClient.ConnectAsync(_host, _port).Wait(_defaultTimeout);
                SetTimeout();
            }
            catch (Exception ex)
            {
                throw new NetworkingException($"Failed to connect to the remote party at '{_host}:{_port}'. Please check that the remote party is listening and the connection is not blocked by a virus scanner or firewall", NetworkingException.NetworkingExceptionTypeEnum.ConnectionSetupFailed);
            }
        }

        public async Task SendData(string dataToSend)
        {
            // TODO: Implement encoding selection instead of fixing it to UTF8
            var bytesToSend = Encoding.UTF8.GetBytes(dataToSend);
            var nrOfBytesSend = 0;
            int nrOfBytesToSend;

            while (nrOfBytesSend < bytesToSend.Length)
            {
                // Set initial send buffer size
                if (bytesToSend.Length > _bufferSize)
                    nrOfBytesToSend = _bufferSize;
                else
                    nrOfBytesToSend = bytesToSend.Length;

                nrOfBytesSend += await _tcpClient.Client.SendAsync(new ArraySegment<byte>(bytesToSend, nrOfBytesSend, nrOfBytesToSend), SocketFlags.None);
                //nrOfBytesSend += _tcpClient.Client.Send(bytesToSend.Skip(nrOfBytesSend).Take(nrOfBytesToSend).ToArray());
            }
            
        }

        /// <summary>
        /// Disposes any active connections managed by this class
        /// </summary>
        public void Dispose()
        {
            _tcpClient?.Dispose();
        }
    }
}
