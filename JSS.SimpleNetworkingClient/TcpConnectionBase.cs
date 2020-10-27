using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JSS.SimpleNetworkingClient
{
    /// <summary>
    /// Base class that implements core functionality
    /// </summary>
    public abstract class TcpConnectionBase
    {
        protected int _bufferSize;
        protected TimeSpan _sendReadTimeout;
        protected TcpClient _tcpClient;
        private DateTime _timeoutTimer;

        /// <summary>
        /// Ctor; Sets defaults for the connection base class
        /// </summary>
        /// <param name="sendReadTimeout">Send/Read timeout</param>
        protected TcpConnectionBase(TimeSpan sendReadTimeout, int bufferSize)
        {
            // TODO: set linger state
            _sendReadTimeout = sendReadTimeout;
            _bufferSize = bufferSize;
        }

        /// <summary>
        /// Overloaded Ctor; allows implementing class to set the timeout and buffer sizes
        /// </summary>
        protected TcpConnectionBase()
        {
            
        }

        /// <summary>
        /// Reads all the tcp data from the tcp client and assumes the remote part sends the total length of the data to transmit as an int32.
        /// This length will be the first 4 bytes of the tcp stream data payload
        /// </summary>
        /// <returns>String with all the data, excluding the length</returns>
        /// <exception cref="NetworkingException">Will throw an exception if the length of the total data received does not match the reported data that should be send according to the remote party</exception>
        protected async Task<string> ReadTcpDataWithLengthHeader()
        {
            using (NetworkStream stream = _tcpClient.GetStream())
            {
                SetTimeout();
                var bytesToRead = 0;
                var bytesRemaining = 0;
                var actualBytesRead = 0;
                var totalBytesRead = 0;
                var chunckBuffer = new byte[_bufferSize];
                List<byte> totalBuffer;

                // Check how many bytes will be send by the remote party
                var lengthBuffer = new byte[4];
                if (stream.Read(lengthBuffer, 0, 4) != 4)
                    throw new NetworkingException("Failed to read the first 4 bytes of the tcp data stream", NetworkingException.NetworkingExceptionTypeEnum.InvalidDataStreamLength);
                var dataStreamTotalLength = new TcpLengthStruct(lengthBuffer);

                // Check if the remote party is actually going to return any data
                if (dataStreamTotalLength == 0)
                    return "";

                // Determine if more bytes are available than the buffer size
                totalBuffer = new List<byte>(dataStreamTotalLength);
                bytesRemaining = dataStreamTotalLength;
                if (bytesRemaining > _bufferSize)
                    bytesToRead = _bufferSize;
                else
                    bytesToRead = bytesRemaining;

                // Read all the data in _bufferSize chunks until all the data has been read
                while (bytesRemaining > 0)
                {
                    // Detect if the connection has been closed, reset or terminated
                    if (_tcpClient.Connected == false)
                        throw new NetworkingException($"Networking socket has been closed by the remote party", NetworkingException.NetworkingExceptionTypeEnum.ConnectionAbortedPrematurely);

                    // Check if the read has timed out. The TcpClient has a mechanism for this but it is not relyable
                    if (DateTime.Now > _timeoutTimer + _sendReadTimeout)
                        throw new NetworkingException($"Reading of tcp data timed out. Timeout set to {_sendReadTimeout.TotalMilliseconds} ms", NetworkingException.NetworkingExceptionTypeEnum.ReadTimeout);

                    actualBytesRead = await stream.ReadAsync(chunckBuffer, 0, bytesToRead);

                    // Check if we have actually read any bytes. If we read faster that the transmitting party, we could overtake it.
                    if (actualBytesRead == 0)
                    {
                        // We have overtaken the transmitting party and haven't read any bytes. Wait for the transmitting party to catch up
                        await Task.Delay(1);
                        continue;
                    }

                    totalBytesRead += actualBytesRead;
                    totalBuffer.AddRange(chunckBuffer.Take(actualBytesRead));
                    bytesRemaining -= actualBytesRead;

                    if (bytesRemaining > _bufferSize)
                        bytesToRead = _bufferSize;
                    else
                        bytesToRead = bytesRemaining;
                }

                // Validate length reported with the actual length received
                if (totalBytesRead != dataStreamTotalLength)
                    throw new NetworkingException($"The actual number of bytes received({totalBytesRead}) doesn't match the number of bytes({dataStreamTotalLength.Value}) that should have been send by the remote party", NetworkingException.NetworkingExceptionTypeEnum.MoreOrLessDataReceived);

                return Encoding.UTF8.GetString(totalBuffer.ToArray(), 0, totalBytesRead);
            }
        }

        protected async Task<string> ReadTcpData()
        {
            using (NetworkStream stream = _tcpClient.GetStream())
            {
                SetTimeout();
                var totalBytesRead = 0;
                var chunckBuffer = new byte[_bufferSize];
                List<byte> totalBuffer = new List<byte>();

                // Read all the data until the tcp connection has been closed
                while (PollTcpClient())
                {
                    // Detect the tcp state and log this if the state changes
                    // TODO: Use Client.GetSocketState SocketOptionName.PacketInformation
                    //var state = _tcpClient.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.UpdateConnectContext);
                    //byte[] tcpOptions = new byte[1024];
                    //var sOpts = _tcpClient.Client.GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.PacketInformation);
                    _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.UpdateAcceptContext, null);

                    // Detect if there is an error on the socket
                    if (_tcpClient.Client.Poll(1, SelectMode.SelectError))
                        throw new NetworkingException($"Networking socket is in an error state", NetworkingException.NetworkingExceptionTypeEnum.SocketError);

                    // Detect if the connection has been closed, reset or terminated
                    if (_tcpClient.Connected == false && _tcpClient.Available == 0)
                        break;

                    // Check if the read has timed out. The TcpClient has a mechanism for this but it is not relyable
                    if (DateTime.Now > _timeoutTimer + _sendReadTimeout)
                        throw new NetworkingException($"Reading of tcp data timed out. Timeout set to {_sendReadTimeout.TotalMilliseconds} ms", NetworkingException.NetworkingExceptionTypeEnum.ReadTimeout);
                    
                    // Read available data, but do not exceed the buffer size in one read
                    var bytesRead = stream.Read(chunckBuffer, 0, _bufferSize);
                    totalBytesRead += bytesRead;
                    totalBuffer.AddRange(chunckBuffer.Take(bytesRead).AsEnumerable());
                }

                return Encoding.UTF8.GetString(totalBuffer.ToArray(), 0, totalBytesRead);
            }
        }

        private bool PollTcpClient()
        {
            return _tcpClient.Client.Poll(1000, SelectMode.SelectRead);
        }

        /// <summary>
        /// Sets the tcp/udp client timeout
        /// </summary>
        protected void SetTimeout()
        {
            if (_tcpClient?.Client != null)
                _tcpClient.Client.SendTimeout = _tcpClient.Client.ReceiveTimeout = (int)_sendReadTimeout.TotalMilliseconds;

            _timeoutTimer = DateTime.Now;
        }
    }
}
