using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
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
    private readonly int _sendReadTimeoutMicroseconds;
    private readonly byte[] _singleMessageBuffer;
    private readonly byte[] _loopBuffer;
    private int _totalBytesRead;

    protected readonly TcpClientSettings Settings;
    protected TcpClient TcpClient;
    
    /// <summary>
    /// Ctor; Sets defaults for the connection base class
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="messageQueueSize">
    /// Contains the maximum number of messages that will be cached in the internal message queue.
    /// Every call to ReadTcpData or is derivatives will return one message from the buffer.
    /// If the buffer overflows, an exception is thrown and the connection is closed.
    /// Remaining messages in the buffer will then be discarded.
    /// </param>
    protected TcpConnectionBase(TcpClientSettings settings, int messageQueueSize = 128)
    {
        Settings = settings;
        Settings.VerifySettings();

        _messageBuffer = new Queue<byte[]>(messageQueueSize);
        _sendReadTimeoutMicroseconds = (int)Settings.SendReadTimeout.TotalMilliseconds * 1000;

        if (Settings.SendReadTimeout == default || Settings.SendReadTimeout <= TimeSpan.Zero)
            throw new ArgumentException($"{nameof(Settings.SendReadTimeout)} must be set to a value higher than zero seconds");

        if (Settings.IpStackBufferSize <= 0)
            throw new ArgumentException($"{nameof(Settings.IpStackBufferSize)} must be larger than zero");

        Settings.Logger?.Verbose($"{nameof(TcpConnectionBase)} ctor has been initialized. {nameof(Settings.SendReadTimeout)}={Settings.SendReadTimeout}, {nameof(Settings.IpStackBufferSize)}={Settings.IpStackBufferSize}, {nameof(messageQueueSize)}={messageQueueSize}");
        _singleMessageBuffer = new byte[Settings.IpStackBufferSize * 2];
        _loopBuffer = new byte[Settings.IpStackBufferSize];
    }

    /// <summary>
    /// Reads all the tcp data from the tcp client and assumes the remote part sends the total length of the data to transmit as an int32
    /// The length will be the first 4 bytes of the tcp stream data payload
    /// </summary>
    /// <param name="stxCharacters">Begin of transmission characters, Eg 0x02 for ASCII char STX. Set to default to disable to disable adding/removing stx characters</param>
    /// <param name="etxCharacters">End of transmission characters, Eg 0x03 for ASCII char ETX. Set to default to disable end of transmission checking</param>
    /// <returns>byte array with all the data, excluding the length</returns>
    /// <exception cref="NetworkingException">Will throw an exception if the length of the total data received does not match the reported data that should be send according to the remote party</exception>
    protected async Task<byte[]> ReadTcpDataWithLengthHeader(byte[] stxCharacters, byte[] etxCharacters)
    {
        var stream = TcpClient.GetStream();
        _timeoutTimer = DateTime.Now;
        var bytesToRead = 0;
        var bytesRemaining = 0;
        var actualBytesRead = 0;
        var totalBytesRead = 0;

        // Check how many bytes will be send by the remote party
        var lengthBuffer = new byte[Settings.LeadingMessageLengthBytes + stxCharacters.Length];
        _ = stream.Read(lengthBuffer, 0, Settings.LeadingMessageLengthBytes + stxCharacters.Length);

        // Check for the stx characters
        if (!lengthBuffer.AsSpan().StartsWith(stxCharacters.AsSpan()))
            throw new NetworkingException($"Parameter {nameof(stxCharacters)} has been set with '{StringUtils.ByteEnumerableToHexString(stxCharacters)}' but these bytes have not been found at the start of transmission", NetworkingException.NetworkingExceptionTypeEnum.WrongStxEtxCharactersReceived);
        
        // Check if the remote party is actually going to return any data
        var dataStreamTotalLength = TcpLengthUtils.GetMessageLength(lengthBuffer, (short)Settings.LeadingMessageLengthBytes!, Settings.LittleEndian);
        if (dataStreamTotalLength == 0)
            return [];

        // Determine if more bytes are available than the buffer size
        byte[] totalBuffer = new byte[dataStreamTotalLength];
        bytesRemaining = dataStreamTotalLength;
        bytesToRead = bytesRemaining > Settings.IpStackBufferSize
            ? Settings.IpStackBufferSize
            : bytesRemaining
        ;

        // Read all the data in IpStackBufferSize chunks until all the data has been read
        while (bytesRemaining > 0)
        {
            // Detect if the connection has been closed, reset or terminated
            if (TcpClient.Connected == false)
                throw new NetworkingException($"Networking socket has been closed by the remote party", NetworkingException.NetworkingExceptionTypeEnum.ConnectionAbortedPrematurely);

            // Check if the read has timed out. The TcpClient has a mechanism for this but it is not relyable
            if (DateTime.Now > _timeoutTimer + Settings.SendReadTimeout)
                throw new NetworkingException($"Reading of tcp data timed out. Timeout set to {Settings.SendReadTimeout.TotalMilliseconds} ms", NetworkingException.NetworkingExceptionTypeEnum.ReadTimeout);

            actualBytesRead = await stream.ReadAsync(totalBuffer, totalBytesRead, bytesToRead);
            totalBytesRead += actualBytesRead;
            
            // Check if we have actually read any bytes. If we read faster that the transmitting party, we could overtake it 
            if (actualBytesRead == 0)
            {
                // We have overtaken the transmitting party and haven't read any bytes. Wait for the transmitting party to catch up
                await Task.Delay(1);
                continue;
            }
            
            Settings.Logger?.Verbose($"{totalBytesRead} bytes have been read in total. Last chunk contains {actualBytesRead} bytes");
            bytesRemaining -= actualBytesRead;

            bytesToRead = bytesRemaining > Settings.IpStackBufferSize
                ? Settings.IpStackBufferSize
                : bytesRemaining
            ;
        }

        // Validate length reported with the actual length received
        if (totalBytesRead != dataStreamTotalLength)
            throw new NetworkingException($"The actual number of bytes received({totalBytesRead}) doesn't match the number of bytes({dataStreamTotalLength}) that should have been send by the remote party", NetworkingException.NetworkingExceptionTypeEnum.MoreOrLessDataReceived);

        // Validate if the etx characters have been received
        if (!totalBuffer.AsSpan().EndsWith(etxCharacters.AsSpan()))
            throw new NetworkingException($"Parameter {nameof(etxCharacters)} has been set with '{StringUtils.ByteEnumerableToHexString(etxCharacters)}' but these bytes have not been found at the end of transmission", NetworkingException.NetworkingExceptionTypeEnum.WrongStxEtxCharactersReceived);
            
        Settings.Logger?.Verbose($"Total nr of {totalBytesRead} bytes have been read");

        // Remove the stx and etx characters from the total buffer and then return it
        return totalBuffer.AsSpan(stxCharacters.Length, totalBytesRead - stxCharacters.Length - etxCharacters.Length).ToArray();
    }

    /// <summary>
    /// Reads TCP data with length header until the remote part closes the connection or the end of stream character is received
    /// </summary>
    /// <param name="stxCharacters">Begin of transmission characters, Eg 0x02 for ASCII char STX. Set to default to disable to disable adding/removing stx characters</param>
    /// <param name="etxCharacters">End of transmission characters, Eg 0x03 for ASCII char ETX. Set to default to disable end of transmission checking</param>
    /// <returns>UTF8 formatted string with the data</returns>
    protected async Task<string> ReadTcpDataWithLengthHeaderAsString(byte[] stxCharacters, byte[] etxCharacters) 
        => Encoding.UTF8.GetString(await ReadTcpDataWithLengthHeader(stxCharacters, etxCharacters));
    
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
        
        var stream = TcpClient.GetStream();
        _timeoutTimer = DateTime.Now;

        // Read all the data until the tcp connection has been closed
        while (PollTcpClient())
        {
            // Detect if there is an error on the socket
            if (TcpClient.Client.Poll(1, SelectMode.SelectError))
                throw new NetworkingException($"Networking socket is in an error state", NetworkingException.NetworkingExceptionTypeEnum.SocketError);

            // Detect if the connection has been closed, reset or terminated
            if (TcpClient.Client.Connected == false || TcpClient.Available == 0)
                throw new NetworkingException($"Connection has been closed, reset or terminated", NetworkingException.NetworkingExceptionTypeEnum.SocketError);

            // Check if the read has timed out. The TcpClient.Connected mechanism is not reliable
            if (DateTime.Now > _timeoutTimer + Settings.SendReadTimeout)
                throw new NetworkingException($"Reading of tcp data timed out. Timeout set to {Settings.SendReadTimeout.TotalMilliseconds} ms", NetworkingException.NetworkingExceptionTypeEnum.ReadTimeout);

            // Read available data and get the nr of bytes received
            var bytesRead = stream.Read(_loopBuffer, 0, Settings.IpStackBufferSize);
            
            // Check that the singleMessageBuffer doesn't overflow
            if (_totalBytesRead + bytesRead > Settings.IpStackBufferSize)
                throw new NetworkingException($"internal loopBuffer overflowed. Bytes read last={bytesRead}, Bytes read before last={_totalBytesRead}", NetworkingException.NetworkingExceptionTypeEnum.BufferOverflow);
            
            // Add the bytes read to the end of the single message buffer
            Array.Copy(_loopBuffer, 0, _singleMessageBuffer, _totalBytesRead, bytesRead);
            
            // Advance the total bytes read counter so that the next read will be copied to the proper position in the singleMessageBuffer
            _totalBytesRead += bytesRead;
            
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
                    Settings.Logger?.Verbose($"End of stream character(s) '{StringUtils.ByteEnumerableToHexString(etxCharacters)}' have been detected. Pushing message to queue. Message: {BitConverter.ToString([.. singleMessageBufferSpan[.. etxCharactersIndex]], 0, etxCharactersIndex)}");
                    _messageBuffer.Enqueue(singleMessageBufferSpan[stxCharacters.Length .. etxCharactersIndex].ToArray());

                    // Move remaining bytes to the start of the buffer.
                    Array.Copy(_singleMessageBuffer, etxCharactersIndex + 1, _singleMessageBuffer, 0, _singleMessageBuffer.Length - etxCharactersIndex - 1);
                    
                    // Set the first available index of the singleMessageBuffer to after any remaining data that can be still in the buffer.
                    _totalBytesRead -= etxCharactersIndex + 1;
                    if (_totalBytesRead < 0)
                        throw new ArgumentException($"totalBytesRead cannot be negative. {nameof(_singleMessageBuffer)} contains: {BitConverter.ToString([.. _singleMessageBuffer], 0, _singleMessageBuffer.Length)}");
                }
                else
                {
                    // No complete messages remain in the buffer
                    break;
                }
            }
            
            if (_messageBuffer.Any())
                return _messageBuffer.Dequeue();
        }
        
        return [];
    }
    
    /// <summary>
    /// Reads TCP data until the remote part closes the connection or the end of stream character is received
    /// </summary>
    /// <param name="stxCharacters">Begin of transmission characters, Eg 0x02 for ASCII char STX. Set to default to disable to disable adding/removing stx characters</param>
    /// <param name="etxCharacters">End of transmission characters, Eg 0x03 for ASCII char ETX. Set to default to disable end of transmission checking</param>
    /// <returns>UTF8 formatted string with the data</returns>
    protected string ReadTcpDataAsString(byte[] stxCharacters, byte[] etxCharacters) 
        => Encoding.UTF8.GetString([.. ReadTcpData(stxCharacters, etxCharacters)]);
    
    protected string ReadTcpDataSocket(Socket socket)
    {
        _timeoutTimer = DateTime.Now;
        var totalBytesRead = 0;
        var chunckBuffer = new byte[Settings.IpStackBufferSize];
        List<byte> totalBuffer = [];

        // Read all the data until the tcp connection has been closed
        while (socket.Poll(_sendReadTimeoutMicroseconds, SelectMode.SelectRead))
        {
            // Detect if there is an error on the socket
            if (socket.Poll(1, SelectMode.SelectError))
                throw new NetworkingException($"Networking socket is in an error state", NetworkingException.NetworkingExceptionTypeEnum.SocketError);

            // Detect if the connection has been closed, reset or terminated
            if (socket.Connected == false || TcpClient.Available == 0)
                break;

            // Check if the read has timed out. The TcpClient client.Connected mechanism is not reliable
            if (DateTime.Now > _timeoutTimer + Settings.SendReadTimeout)
                throw new NetworkingException($"Reading of tcp data timed out. Timeout set to {Settings.SendReadTimeout.TotalMilliseconds} ms", NetworkingException.NetworkingExceptionTypeEnum.ReadTimeout);

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
        //var bytesToSend = GetByteListNotNull(_stxCharacters).Concat(encoding.GetBytes(dataToSend)).Concat(GetByteListNotNull(_etxCharacters)).ToArray();
        await SendData([..Settings.StxCharacters, ..encoding.GetBytes(dataToSend), ..Settings.EtxCharacters], sendDelayMs);
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

        Settings.Logger?.Verbose($"Sending data: {BitConverter.ToString(dataToSend, 0, dataToSend.Length)}");

        if (TcpClient == null) {
            Settings.Logger?.Verbose($"SendData: Client not initialized");
            return;
        }
        
        while (nrOfBytesSend < dataToSend.Length)
        {
            // Calculate initial send buffer size
            var totalBytesStillToSend = dataToSend.Length - nrOfBytesSend;
            var nrOfBytesToSend = totalBytesStillToSend > Settings.IpStackBufferSize ? Settings.IpStackBufferSize : totalBytesStillToSend;

            // Check for a timeout
            if (DateTime.Now > startTime + Settings.SendReadTimeout)
                throw new NetworkingException($"Timeout in sending data. Timeout is {Settings.SendReadTimeout.TotalMilliseconds} ms", NetworkingException.NetworkingExceptionTypeEnum.WriteTimeout);

            // Delay sending of the data.
            if (sendDelayMs > 0)
                await Task.Delay(sendDelayMs);

            // Wait until the socket becomes ready to write any data
            if (TcpClient.Client.Poll(_pollWriteTimeout, SelectMode.SelectWrite) == false)
                throw new NetworkingException($"Timeout waiting for the socket to become ready for sending data. {nrOfBytesToSend} bytes have to be send in total. {nrOfBytesSend} bytes have actually been send.", NetworkingException.NetworkingExceptionTypeEnum.WriteTimeout);

            // Select the chunck of data to be send without copying the array and send the data
            var sendOperation = TcpClient.Client.BeginSend(dataToSend, nrOfBytesSend, nrOfBytesToSend, SocketFlags.None, _ => { }, TcpClient.Client);
            nrOfBytesSend += await Task.Factory.FromAsync(sendOperation, result => TcpClient.Client.EndSend(result));
            Settings.Logger?.Verbose($"{nrOfBytesSend} bytes have been send in total");
        }

        Settings.Logger?.Verbose($"All data has been transmitted");
    }

    /// <summary>
    /// Polls the underlying winsock connection to detect if data can be read
    /// </summary>
    /// <returns>True to indicate that data is available or the connection has been closed. False to indicate the connection is not readable</returns>
    /// <remarks>
    /// IMPORTANT: The Poll method only blocks when the connection is established and data has yet to be send.
    /// That a .Net Socket is reported as being open does not mean that the full connection has been established yet
    /// </remarks>
    private bool PollTcpClient()
        => TcpClient.Client.Poll(_sendReadTimeoutMicroseconds, SelectMode.SelectRead);

    /// <summary>
    /// Disposes the currently active tcp client(if any)
    /// </summary>
    protected void DisposeCurrentTcpClient()
    {
        if (TcpClient != default)
        {
            TcpClient.Dispose();
            TcpClient = default;
        }

        Settings.Logger?.Verbose($"{nameof(DisposeCurrentTcpClient)}() has been executed");
    }

    public void Dispose()
        => DisposeCurrentTcpClient();
}
