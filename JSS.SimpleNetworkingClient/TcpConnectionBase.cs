using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using JSS.SimpleNetworkingClient.Interfaces;
using JSS.SimpleNetworkingClient.Utils;

namespace JSS.SimpleNetworkingClient;

/// <summary>
/// Base class that implements core functionality
/// </summary>
public abstract class TcpConnectionBase : IDisposable
{
    private readonly int _pollWriteTimeout = (int)TimeSpan.FromSeconds(5).TotalMilliseconds * 1000;
    private DateTime _timeoutTimer;
    private readonly Queue<byte[]> _messageBuffer;
    
    protected readonly ISimpleNetworkingClientLogger Logger;
    protected int IpStackBufferSize;
    protected TimeSpan _sendReadTimeout;
    protected int _sendReadTimeoutMicroseconds;
    protected TcpClient _tcpClient;
    protected byte[] _stxCharacters;
    protected byte[] _etxCharacters;
    private byte[] _singleMessageBuffer;
    private byte[] _loopBuffer;
    private int _totalBytesRead = 0;

    /// <summary>
    /// Ctor; Sets defaults for the connection base class
    /// </summary>
    /// <param name="logger">Logger instance that implements ISimpleNetworkingClientLogger for diagnostic logging</param>
    /// <param name="sendReadTimeout">Send/Read timeout when the connection is stale</param>
    /// <param name="ipStackBufferSize">
    /// Size of the tcp buffer that determines the amount of bytes that is received/send per chunk to the operating system networking stack.
    /// This is not equal to the Maximum Transfer Unit, which controls the maximum number of bytes send out from the OS networking stack in one tcp frame.
    /// If the 
    /// </param>
    /// <param name="messageQueueSize">
    /// Contains the maximum number of messages that will be cached in the internal message queue.
    /// Every call to ReadTcpData or is derivatives will return one message from the buffer.
    /// If the buffer overflows, an exception is thrown and the connection is closed.
    /// Remaining messages in the buffer will then be discarded.
    /// </param>
    protected TcpConnectionBase(ISimpleNetworkingClientLogger logger, TimeSpan sendReadTimeout, int ipStackBufferSize, int messageQueueSize = 128)
    {
        Logger = logger;
        _sendReadTimeout = sendReadTimeout;
        IpStackBufferSize = ipStackBufferSize;
        _messageBuffer = new Queue<byte[]>(messageQueueSize);
        _sendReadTimeoutMicroseconds = (int)_sendReadTimeout.TotalMilliseconds * 1000;

        if (_sendReadTimeout == default || _sendReadTimeout <= TimeSpan.Zero)
            throw new ArgumentException($"{nameof(sendReadTimeout)} must be set to a value higher than zero seconds");

        if (IpStackBufferSize <= 0)
            throw new ArgumentException($"{nameof(ipStackBufferSize)} must be larger than zero");

        Logger?.Verbose($"{nameof(TcpConnectionBase)} ctor has been initialized. {nameof(sendReadTimeout)}={sendReadTimeout}, {nameof(ipStackBufferSize)}={ipStackBufferSize}, {nameof(messageQueueSize)}={messageQueueSize}");
        _singleMessageBuffer = new byte[IpStackBufferSize * 2];
        _loopBuffer = new byte[IpStackBufferSize];
    }

    /// <summary>
    /// Reads all the tcp data from the tcp client and assumes the remote part sends the total length of the data to transmit as an int32
    /// The length will be the first 4 bytes of the tcp stream data payload
    /// </summary>
    /// <returns>String with all the data, excluding the length</returns>
    /// <exception cref="NetworkingException">Will throw an exception if the length of the total data received does not match the reported data that should be send according to the remote party</exception>
    protected async Task<string> ReadTcpDataWithLengthHeader()
    {
        var stream = _tcpClient.GetStream();
        _timeoutTimer = DateTime.Now;
        var bytesToRead = 0;
        var bytesRemaining = 0;
        var actualBytesRead = 0;
        var totalBytesRead = 0;
        var chunkBuffer = new byte[IpStackBufferSize];
        List<byte> totalBuffer;

        // Check how many bytes will be send by the remote party
        var lengthBuffer = new byte[4];
        if (stream.Read(lengthBuffer, 0, 4) != 4)
            throw new NetworkingException("Failed to read the first 4 bytes of the tcp data stream", NetworkingException.NetworkingExceptionTypeEnum.InvalidDataStreamLength);

        // Check if the remote party is actually going to return any data
        TcpLengthStruct dataStreamTotalLength = new(lengthBuffer);
        if (dataStreamTotalLength == 0)
            return string.Empty;

        // Determine if more bytes are available than the buffer size
        totalBuffer = new(dataStreamTotalLength);
        bytesRemaining = dataStreamTotalLength;
        bytesToRead = bytesRemaining > IpStackBufferSize
            ? IpStackBufferSize
            : bytesRemaining
        ;

        // Read all the data in IpStackBufferSize chunks until all the data has been read
        while (bytesRemaining > 0)
        {
            // Detect if the connection has been closed, reset or terminated
            if (_tcpClient.Connected == false)
                throw new NetworkingException($"Networking socket has been closed by the remote party", NetworkingException.NetworkingExceptionTypeEnum.ConnectionAbortedPrematurely);

            // Check if the read has timed out. The TcpClient has a mechanism for this but it is not relyable
            if (DateTime.Now > _timeoutTimer + _sendReadTimeout)
                throw new NetworkingException($"Reading of tcp data timed out. Timeout set to {_sendReadTimeout.TotalMilliseconds} ms", NetworkingException.NetworkingExceptionTypeEnum.ReadTimeout);

            actualBytesRead = await stream.ReadAsync(chunkBuffer, 0, bytesToRead);

            // Check if we have actually read any bytes. If we read faster that the transmitting party, we could overtake it 
            if (actualBytesRead == 0)
            {
                // We have overtaken the transmitting party and haven't read any bytes. Wait for the transmitting party to catch up
                await Task.Delay(1);
                continue;
            }

            totalBytesRead += actualBytesRead;
            Logger?.Verbose($"{totalBytesRead} bytes have been read in total. Last chunk contains {actualBytesRead} bytes");
            totalBuffer.AddRange(chunkBuffer.Take(actualBytesRead));
            bytesRemaining -= actualBytesRead;

            bytesToRead = bytesRemaining > IpStackBufferSize
                ? IpStackBufferSize
                : bytesRemaining
            ;
        }

        // Validate length reported with the actual length received
        if (totalBytesRead != dataStreamTotalLength)
            throw new NetworkingException($"The actual number of bytes received({totalBytesRead}) doesn't match the number of bytes({dataStreamTotalLength.Value}) that should have been send by the remote party", NetworkingException.NetworkingExceptionTypeEnum.MoreOrLessDataReceived);

        Logger?.Verbose($"Total nr of {totalBytesRead} bytes have been read");

        return Encoding.UTF8.GetString([.. totalBuffer], 0, totalBytesRead);
    }

    /// <summary>
    /// Reads TCP data until the remote part closes the connection or the end of stream character is received
    /// </summary>
    /// <param name="stxCharacters">Begin of transmission characters, Eg 0x02 for ASCII char STX. Set to default to disable to disable adding/removing stx characters</param>
    /// <param name="etxCharacters">End of transmission characters, Eg 0x03 for ASCII char ETX. Set to default to disable end of transmission checking</param>
    /// <returns>UTF8 formatted string with the data</returns>
    protected string ReadTcpDataAsString(byte[] stxCharacters, byte[] etxCharacters) 
        => Encoding.UTF8.GetString([.. ReadTcpData(stxCharacters, etxCharacters)]);

    /// <summary>
    /// Reads TCP data until the remote part closes the connection or the end of stream character is received
    /// </summary>
    /// <param name="stxCharacters">Begin of transmission characters, Eg 0x02 for ASCII char STX. Set to default to disable to disable adding/removing stx characters</param>
    /// <param name="etxCharacters">End of transmission characters, Eg 0x03 for ASCII char ETX. Set to default to disable end of transmission checking</param>
    /// <returns>byte array with the data</returns>
    /// <remarks>
    /// I dont use a seperate thread for reading the data because this could cause a buffer overflow if the application that calls this method is too slow.
    /// 
    /// </remarks>
    protected byte[] ReadTcpData(byte[] stxCharacters, byte[] etxCharacters)
    {
        // If there are any messages in the queue from the last time this method was called, return the first message in the queue
        if (_messageBuffer.Any())
            return _messageBuffer.Dequeue();
        
        var stream = _tcpClient.GetStream();
        _timeoutTimer = DateTime.Now;

        // Read all the data until the tcp connection has been closed
        while (PollTcpClient())
        {
            // Detect if there is an error on the socket
            if (_tcpClient.Client.Poll(1, SelectMode.SelectError))
                throw new NetworkingException($"Networking socket is in an error state", NetworkingException.NetworkingExceptionTypeEnum.SocketError);

            // Detect if the connection has been closed, reset or terminated
            if (_tcpClient.Client.Connected == false || _tcpClient.Available == 0)
                throw new NetworkingException($"Connection has been closed, reset or terminated", NetworkingException.NetworkingExceptionTypeEnum.SocketError);

            // Check if the read has timed out. The TcpClient.Connected mechanism is not reliable
            if (DateTime.Now > _timeoutTimer + _sendReadTimeout)
                throw new NetworkingException($"Reading of tcp data timed out. Timeout set to {_sendReadTimeout.TotalMilliseconds} ms", NetworkingException.NetworkingExceptionTypeEnum.ReadTimeout);

            // Read available data and get the nr of bytes received
            var bytesRead = stream.Read(_loopBuffer, 0, IpStackBufferSize);
            
            // Check that the singleMessageBuffer doesn't overflow
            if (_totalBytesRead + bytesRead > IpStackBufferSize)
                throw new NetworkingException($"internal chekcBuffer overflowed. Bytes read last={bytesRead}, Bytes read before last={_totalBytesRead}", NetworkingException.NetworkingExceptionTypeEnum.BufferOverflow);
            
            // Add the bytes read to the end of the single message buffer
            Array.Copy(_loopBuffer, 0, _singleMessageBuffer, _totalBytesRead, bytesRead);
            
            // Check that the stream starts with the stx characters
            for (int i = 0; i < stxCharacters.Length; i++)
                if (_singleMessageBuffer[i] != stxCharacters[i])
                    throw new NetworkingException($"Parameter {nameof(stxCharacters)} has been set with '{StringUtils.ByteEnumerableToHexString(stxCharacters)}' but these bytes have not been found at the start of transmission", NetworkingException.NetworkingExceptionTypeEnum.WrongStxEtxCharactersReceived);
            
            while (true)
            {
                // Search for the first etx characters in the stream, indicating one message has been received
                var singleMessageBufferSpan = _singleMessageBuffer.AsSpan();
                var etxCharactersIndex = singleMessageBufferSpan.IndexOf(etxCharacters.AsSpan());
                if (etxCharactersIndex > -1)
                {
                    // Enqueue message for later processing without stx and etx characters
                    Logger?.Verbose($"End of stream character(s) '{StringUtils.ByteEnumerableToHexString(etxCharacters)}' have been detected. Pushing message to queue. Message: {BitConverter.ToString([.. singleMessageBufferSpan[.. etxCharactersIndex]], 0, etxCharactersIndex)}");
                    _messageBuffer.Enqueue(singleMessageBufferSpan[stxCharacters.Length .. etxCharactersIndex].ToArray());

                    // Move remaining bytes to the start of the buffer.
                    Array.Copy(_singleMessageBuffer, etxCharactersIndex, _singleMessageBuffer, 0, _singleMessageBuffer.Length - etxCharactersIndex);
                    
                    // Set the first available index of the singleMessageBuffer to after any remaining data that can be still in the buffer.
                    _totalBytesRead -= etxCharactersIndex;
                    if (_totalBytesRead < 0)
                        throw new ArgumentException($"totalBytesRead cannot be negative. {nameof(_singleMessageBuffer)} contains: {BitConverter.ToString([.. _singleMessageBuffer], 0, _singleMessageBuffer.Length)}");
                }
                else
                {
                    // No complete messages remain in the buffer
                    break;
                }
            }

            // Read available data, but do not exceed the buffer size in one read
            // var bytesRead = stream.Read(singleMessageBuffer, 0, IpStackBufferSize);
            // totalBytesRead += bytesRead;
            // Logger?.Verbose($"{totalBytesRead} bytes have been read in total. Last chunk contains {bytesRead} bytes");
            // var actualBytesRead = singleMessageBuffer.Take(bytesRead).ToList();
            // totalBuffer.AddRange(actualBytesRead);

            // Check if the end of the actual bytes read matches the supplied end of stream character(s)
            // if (etxCharacters != default
            //     && actualBytesRead.Count >= etxCharacters.Count
            //     && actualBytesRead.Skip(actualBytesRead.Count - etxCharacters.Count).Take(etxCharacters.Count).Except(etxCharacters).Any() == false)
            // {
            //     Logger?.Verbose($"End of stream character(s) '{StringUtils.ByteEnumerableToHexString(etxCharacters)}' have been detected. Returning data that has thus far been received excluding the stx and etx characters.");
            //     break;
            // }
        }
        
        return _messageBuffer.Any() ? _messageBuffer.Dequeue() : [];

        // if (_totalBytesRead == 0)
        //     return [];
        //
        // Logger?.Verbose($"Bytes received: {BitConverter.ToString([.. totalBuffer], 0, totalBuffer.Count)}");
        //
        // // Check if the start of transmission matches
        // if (stxCharacters != default && (totalBuffer.Count < stxCharacters.Count || totalBuffer.Take(stxCharacters.Count).Except(stxCharacters).Any()))
        //     throw new NetworkingException($"Parameter {nameof(stxCharacters)} has been set with '{StringUtils.ByteEnumerableToHexString(stxCharacters)}' but these bytes have not been found at the start of transmission", NetworkingException.NetworkingExceptionTypeEnum.WrongStxEtxCharactersReceived);
        //
        // Logger?.Verbose($"Total nr of {_totalBytesRead} bytes have been read");
        //
        // // Return string excluding stx/etx characters
        // var startIndex = stxCharacters?.Count ?? 0;
        // var endCount = _totalBytesRead - startIndex - etxCharacters?.Count ?? 0;
        // return totalBuffer.Skip(startIndex).Take(endCount).ToArray();
    }
    
    protected string ReadTcpDataSocket(Socket socket)
    {
        _timeoutTimer = DateTime.Now;
        var totalBytesRead = 0;
        var chunckBuffer = new byte[IpStackBufferSize];
        List<byte> totalBuffer = [];

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
            totalBuffer.AddRange(chunckBuffer.Take(bytesRead));
        }

        return Encoding.UTF8.GetString([.. totalBuffer], 0, totalBytesRead);
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
    /// Nr of bytes send is the nr of bytes send to the operating system networking stack. The networking stack by design does not guarantee that the data is actually completely transmitted across the network.
    /// We could also pass all the data at once to the BeginSend, but i want to remain in control over each block of data send, so we can see what is going wrong when a transmission failure occurs.
    /// </remarks>
    public async Task SendData(byte[] dataToSend, int sendDelayMs = 0)
    {
        var startTime = DateTime.Now;
        var nrOfBytesSend = 0;

        Logger?.Verbose($"Sending data: {BitConverter.ToString(dataToSend, 0, dataToSend.Length)}");

        if (_tcpClient == null) {
            Logger?.Verbose($"SendData: Client not initialized");
            return;
        }
        
        while (nrOfBytesSend < dataToSend.Length)
        {
            // Calculate initial send buffer size
            var totalBytesStillToSend = dataToSend.Length - nrOfBytesSend;
            var nrOfBytesToSend = totalBytesStillToSend > IpStackBufferSize ? IpStackBufferSize : totalBytesStillToSend;

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
            Logger?.Verbose($"{nrOfBytesSend} bytes have been send in total");
        }

        Logger?.Verbose($"All data has been transmitted");
    }

    /// <summary>
    /// Gets the active send/read timeout
    /// </summary>
    public TimeSpan SendReadTimeout 
        => _sendReadTimeout;

    /// <summary>
    /// Polls the underlying winsock connection to detect if data can be read
    /// </summary>
    /// <returns>True to indicate that data is available or the connection has been closed. False to indicate the connection is not readable</returns>
    /// <remarks>
    /// IMPORTANT: The Poll method only blocks when the connection is established and data has yet to be send.
    /// That a .Net Socket is reported as being open does not mean that the full connection has been established yet
    /// </remarks>
    private bool PollTcpClient()
        => _tcpClient.Client.Poll(_sendReadTimeoutMicroseconds, SelectMode.SelectRead);

    /// <summary>
    /// Gets a byte list. If the input is default, a new empty list will be returned
    /// </summary>
    /// <param name="input">Byte list or default</param>
    /// <returns>byte list</returns>
    private List<byte> GetByteListNotNull(IList<byte> input)
        => input as List<byte> ?? [];

    /// <summary>
    /// Disposes the currently active tcp client(if any)
    /// </summary>
    protected void DisposeCurrentTcpClient()
    {
        if (_tcpClient != default)
        {

            _tcpClient.Dispose();
            _tcpClient = default;
        }

        Logger?.Verbose($"{nameof(DisposeCurrentTcpClient)}() has been executed");
    }

    public void Dispose()
        => DisposeCurrentTcpClient();
}
