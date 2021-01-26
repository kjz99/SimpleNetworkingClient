using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JSS.SimpleNetworkingClient
{
    /// <summary>
    /// Defines a TCP send connection that can be used to send data to a remote party.
    /// Warning, please make sure that you always dipose of this class so that unmanaged resources will be released.
    /// </summary>
    /// <see cref="https://github.com/kjz99/SimpleNetworkingClient" />
    public class TcpSendConnection : TcpConnectionBase, IDisposable
    {
        private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);
        private readonly int _pollWriteTimeout = (int)TimeSpan.FromSeconds(5).TotalMilliseconds * 1000;
        private readonly int _defaultBufferSize = 1024;
        private readonly string _host;
        private readonly int _port;

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
                
                // Let the connection remain open for x seconds after calling Close() if data still needs to be transmitted
                _tcpClient.Client.LingerState.Enabled = true;
                _tcpClient.Client.LingerState.LingerTime = 2; // 2 seconds

                // Connect and set the send/receive timeout
                if (_tcpClient.ConnectAsync(_host, _port).Wait(_defaultTimeout) == false)
                    throw new TimeoutException();

                SetTimeout();
            }
            catch (Exception ex)
            {
                throw new NetworkingException($"Failed to connect to the remote party at '{_host}:{_port}'. Please check that the remote party is listening and the connection is not blocked by a virus scanner or firewall", NetworkingException.NetworkingExceptionTypeEnum.ConnectionSetupFailed, ex);
            }
        }

        /// <summary>
        /// Send data to the remote party
        /// </summary>
        /// <param name="dataToSend">Data in UTF-8 encoding to send to the remote party</param>
        /// <param name="encoding">Encoding to use</param>
        /// <param name="sendDelayMs">Delay per data chunk for sending that data in milliseconds. Do not use in production. Only useful in integration testing scenario's. Defaults to 0, meaning no delay</param>
        public async Task SendData(string dataToSend, Encoding encoding, int sendDelayMs = 0)
        {
            var bytesToSend = encoding.GetBytes(dataToSend);
            await SendData(bytesToSend, sendDelayMs);
        }

        /// <summary>
        /// Send data to the remote party
        /// </summary>
        /// <param name="dataToSend">Byte data to send to the remote party</param>
        /// <param name="sendDelayMs">Delay per data chunk for sending that data in milliseconds. Do not use in production. Only useful in integration testing scenario's. Defaults to 0, meaning no delay</param>
        /// <remarks>
        /// nr of bytes send is the nr of bytes send to the operating system networking stack. The networking stack by design does not guarantee that the data is actually completely transmitted across the network.
        /// We could also pass all the data at once to the BeginSend, but i want to remain in control over each block of data send, so we can see what is going wrong when a transmission failure occurs.
        /// </remarks>
        public async Task SendData(byte[] dataToSend, int sendDelayMs = 0)
        {
            var startTime = DateTime.Now;
            var nrOfBytesSend = 0;

            while (nrOfBytesSend < dataToSend.Length)
            {
                // Calculate initial send buffer size
                var totalBytesStillToSend = dataToSend.Length - nrOfBytesSend;
                var nrOfBytesToSend = totalBytesStillToSend > _bufferSize ? _bufferSize : totalBytesStillToSend;

                // Check for a timeout
                if (DateTime.Now > startTime + _sendReadTimeout)
                    throw new NetworkingException($"Timeout in sending data. Timeout is {_sendReadTimeout.TotalMilliseconds} ms", NetworkingException.NetworkingExceptionTypeEnum.WriteTimeout);

                // Delay sending of the data.
                if (sendDelayMs > 0)
                    await Task.Delay(sendDelayMs);

                // Wait until the socket becomes ready to write any data
                if (_tcpClient.Client.Poll(_pollWriteTimeout, SelectMode.SelectWrite) == false)
                    throw new NetworkingException($"Timeout waiting for the socket to become ready for sending data. {nrOfBytesToSend} bytes have to be send in total. {nrOfBytesSend} bytes have actually been send.", NetworkingException.NetworkingExceptionTypeEnum.WriteTimeout);

                // Select the chunck of data to be send without copying the array and send the data
                var segmentToSend = new ArraySegment<byte>(dataToSend, nrOfBytesSend, nrOfBytesToSend);
                var sendOperation = _tcpClient.Client.BeginSend(segmentToSend.Array, 0, segmentToSend.Count, SocketFlags.None, _ => { }, _tcpClient.Client);
                nrOfBytesSend += await Task.Factory.FromAsync(sendOperation, result => _tcpClient.Client.EndSend(result));
            }
        }

        /// <summary>
        /// Attempt to receive data on the send connection.
        /// This method blocks until the remote party disconnected or the receive timeout expired.
        /// </summary>
        /// <param name="endOfStreamCharacter">End of stream character, Eg 0x03 for ASCII char ETX. Set to null to disable end of stream checking.</param>
        /// <returns>
        /// Data received from the remote party
        /// </returns>
        public string ReceiveData()
        {
            return ReadTcpData();
        }

        /// <summary>
        /// Disposes any active connections managed by this class
        /// </summary>
        public void Dispose()
        {
            _tcpClient?.Close();
            _tcpClient?.Dispose();
        }
    }
}
