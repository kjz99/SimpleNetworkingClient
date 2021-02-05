using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace JSS.SimpleNetworkingClient
{
    /// <summary>
    /// Base class that implements core functionality
    /// </summary>
    public abstract class TcpConnectionBase : IDisposable
    {
        protected int _bufferSize;
        private readonly int _pollWriteTimeout = (int)TimeSpan.FromSeconds(5).TotalMilliseconds * 1000;
        protected TimeSpan _sendReadTimeout;
        protected int _sendReadTimeoutMicroseconds;
        protected TcpClient _tcpClient;
        private DateTime _timeoutTimer;
        protected IList<byte> _stxCharacters;
        protected IList<byte> _etxCharacters;

        /// <summary>
        /// Ctor; Sets defaults for the connection base class
        /// </summary>
        /// <param name="sendReadTimeout">Send/Read timeout</param>
        /// <param name="bufferSize">Size of the tcp buffer that determines the amount of bytes that is received/send per chunk</param>
        protected TcpConnectionBase(TimeSpan sendReadTimeout, int bufferSize)
        {
            _sendReadTimeout = sendReadTimeout;
            _bufferSize = bufferSize;
            _sendReadTimeoutMicroseconds = (int)_sendReadTimeout.TotalMilliseconds * 1000;

            if (_sendReadTimeout == null || _sendReadTimeout <= TimeSpan.Zero)
                throw new ArgumentException($"{nameof(_sendReadTimeout)} must be set to a value higher than zero seconds");
            
            if (_bufferSize <= 0)
                throw new ArgumentException($"{nameof(_bufferSize)} must be larger than zero");
        }

        /// <summary>
        /// Reads all the tcp data from the tcp client and assumes the remote part sends the total length of the data to transmit as an int32.
        /// This length will be the first 4 bytes of the tcp stream data payload
        /// </summary>
        /// <returns>String with all the data, excluding the length</returns>
        /// <exception cref="NetworkingException">Will throw an exception if the length of the total data received does not match the reported data that should be send according to the remote party</exception>
        protected async Task<string> ReadTcpDataWithLengthHeader()
        {
            NetworkStream stream = _tcpClient.GetStream();
            _timeoutTimer = DateTime.Now;
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

        /// <summary>
        /// Reads TCP data until the remote part closes the connection or the end of stream character is received
        /// </summary>
        /// <param name="stxCharacters">Begin of transmission characters, Eg 0x02 for ASCII char STX. Set to null to disable to disable adding/removing stx characters.</param>
        /// <param name="etxCharacters">End of transmission characters, Eg 0x03 for ASCII char ETX. Set to null to disable end of transmission checking.</param>
        /// <returns></returns>
        protected string ReadTcpData(IList<byte> stxCharacters, IList<byte> etxCharacters)
        {
            NetworkStream stream = _tcpClient.GetStream();
            _timeoutTimer = DateTime.Now;
            var totalBytesRead = 0;
            var chunckBuffer = new byte[_bufferSize];
            List<byte> totalBuffer = new List<byte>();

            // Read all the data until the tcp connection has been closed
            while (PollTcpClient())
            {
                // Detect if there is an error on the socket
                if (_tcpClient.Client.Poll(1, SelectMode.SelectError))
                    throw new NetworkingException($"Networking socket is in an error state", NetworkingException.NetworkingExceptionTypeEnum.SocketError);

                // Detect if the connection has been closed, reset or terminated
                if (_tcpClient.Client.Connected == false || _tcpClient.Available == 0)
                    break;

                // Check if the read has timed out. The TcpClient client.Connected mechanism is not reliable
                if (DateTime.Now > _timeoutTimer + _sendReadTimeout)
                    throw new NetworkingException($"Reading of tcp data timed out. Timeout set to {_sendReadTimeout.TotalMilliseconds} ms", NetworkingException.NetworkingExceptionTypeEnum.ReadTimeout);

                // Read available data, but do not exceed the buffer size in one read
                var bytesRead = stream.Read(chunckBuffer, 0, _bufferSize);
                totalBytesRead += bytesRead;
                var actualBytesRead = chunckBuffer.Take(bytesRead).ToList();
                totalBuffer.AddRange(actualBytesRead);

                // Check if the end of the actual bytes read matches the supplied end of stream character(s)
                if (etxCharacters != null 
                    && actualBytesRead.Count >= etxCharacters.Count
                    && actualBytesRead.Skip(actualBytesRead.Count - etxCharacters.Count).Take(etxCharacters.Count).Except(etxCharacters).Any() == false)
                    break;
            }

            // Check if the start of transmission matches
            if (stxCharacters != null && (totalBuffer.Count < stxCharacters.Count || totalBuffer.Take(stxCharacters.Count).Except(stxCharacters).Any()))
                throw new InvalidOperationException($"Parameter {nameof(stxCharacters)} has been set with '{string.Join(", ", stxCharacters)}' but these bytes have not been found at the start of transmission");

            // Return string excluding stx/etx characters
            var startIndex = stxCharacters?.Count ?? 0;
            var endCount = totalBytesRead - startIndex - etxCharacters?.Count ?? 0;
            return Encoding.UTF8.GetString(totalBuffer.ToArray(), startIndex, endCount);
        }

        protected string ReadTcpDataSocket(Socket socket)
        {
            _timeoutTimer = DateTime.Now;
            var totalBytesRead = 0;
            var chunckBuffer = new byte[_bufferSize];
            List<byte> totalBuffer = new List<byte>();

            // Read all the data until the tcp connection has been closed
            while (socket.Poll(_sendReadTimeoutMicroseconds, SelectMode.SelectRead))
            {
                // Detect if there is an error on the socket
                if (socket.Poll(1, SelectMode.SelectError))
                    throw new NetworkingException($"Networking socket is in an error state", NetworkingException.NetworkingExceptionTypeEnum.SocketError);

                // Detect if the connection has been closed, reset or terminated
                if (socket.Connected == false || _tcpClient.Available == 0)
                    break;

                // Check if the read has timed out. The TcpClient client.Connected mechanism is not reliable
                if (DateTime.Now > _timeoutTimer + _sendReadTimeout)
                    throw new NetworkingException($"Reading of tcp data timed out. Timeout set to {_sendReadTimeout.TotalMilliseconds} ms", NetworkingException.NetworkingExceptionTypeEnum.ReadTimeout);

                // Read available data, but do not exceed the buffer size in one read
                var bytesRead = socket.Receive(chunckBuffer);
                totalBytesRead += bytesRead;
                totalBuffer.AddRange(chunckBuffer.Take(bytesRead).AsEnumerable());
            }

            return Encoding.UTF8.GetString(totalBuffer.ToArray(), 0, totalBytesRead);
        }

        /// <summary>
        /// Send data to the remote party.
        /// If the stx/etx characters have been set with the constructor, they will be added to the dataToSend
        /// </summary>
        /// <param name="dataToSend">Data in UTF-8 encoding to send to the remote party</param>
        /// <param name="encoding">Encoding to use</param>
        /// <param name="sendDelayMs">Delay per data chunk for sending that data in milliseconds. Do not use in production. Only useful in integration testing scenario's. Defaults to 0, meaning no delay</param>
        public async Task SendData(string dataToSend, Encoding encoding, int sendDelayMs = 0)
        {
            var bytesToSend = GetByteListNotNull(_stxCharacters).Concat(encoding.GetBytes(dataToSend)).Concat(GetByteListNotNull(_etxCharacters)).ToArray();
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
                var sendOperation = _tcpClient.Client.BeginSend(dataToSend, nrOfBytesSend, nrOfBytesToSend, SocketFlags.None, _ => { }, _tcpClient.Client);
                nrOfBytesSend += await Task.Factory.FromAsync(sendOperation, result => _tcpClient.Client.EndSend(result));
            }
        }

        /// <summary>
        /// Polls the underlying winsock connection to detect if data can be read
        /// </summary>
        /// <returns>True to indicate that data is available or the connection has been closed</returns>
        /// <remarks>
        /// IMPORTANT: The Poll method only blocks when the connection is established and data has yet to be send.
        /// That a .Net Socket is reported as being open does not mean that the full connection has been established yet
        /// </remarks>
        private bool PollTcpClient()
        {
            return _tcpClient.Client.Poll(_sendReadTimeoutMicroseconds, SelectMode.SelectRead);
        }

        /// <summary>
        /// Gets a byte list. If the input is null, a new empty list will be returned
        /// </summary>
        /// <param name="input">Byte list or null</param>
        /// <returns>byte list</returns>
        private List<byte> GetByteListNotNull(IList<byte> input)
        {
            return input as List<byte> ?? new List<byte>();
        }

        /// <summary>
        /// Disposes the currently active tcp client(if any)
        /// </summary>
        protected void DisposeCurrentTcpClient()
        {
            _tcpClient.Client.Close();
            _tcpClient.Client.Dispose();
            _tcpClient.Client = null;
            _tcpClient.Close();
            _tcpClient.Dispose();
        }

        public void Dispose()
        {
            DisposeCurrentTcpClient();
        }
    }
}
