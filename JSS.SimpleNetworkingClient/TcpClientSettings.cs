using System;
using System.Linq;
using JSS.SimpleNetworkingClient.Interfaces;

namespace JSS.SimpleNetworkingClient;

public class TcpClientSettings : ICloneable
{
    public void VerifySettings()
    {
        if (Port is <= 0 or > 65536)
            throw new ArgumentOutOfRangeException($"{nameof(Port)} is out of range");
    }
    
    /// <summary>
    /// Logger instance that implements ISimpleNetworkingClientLogger for diagnostic logging
    /// </summary>
    public ISimpleNetworkingClientLogger Logger { get;  set; }

    /// <summary>
    /// Host to which to connect to.
    /// Only used by the TcpSendConnection.
    /// </summary>
    public string Host { get; set; }
    
    /// <summary>
    /// Port on which to listen for incoming connections / connect to the remote party
    /// </summary>
    public int Port { get; set; }
    
    /// <summary>
    /// Send/Read timeout when the connection is stale
    /// </summary>
    public TimeSpan SendReadTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Size of the tcp buffer that determines the amount of bytes that is received/send per chunk
    /// </summary>
    public int IpStackBufferSize { get; set; } = 1024;
    
    /// <summary>
    /// Begin of transmission characters, Eg 0x02 for ASCII char STX. Set to default to disable to disable adding/removing stx characters
    /// </summary>
    public byte[] StxCharacters  { get; set; } = [];
    
    /// <summary>
    /// End of transmission characters, Eg 0x03 for ASCII char ETX. Set to default to disable end of transmission checking
    /// </summary>
    public byte[] EtxCharacters  { get; set; } = [];
    
    /// <summary>
    /// Throws exception on a tcp error instead of trying to reinitialize the tcp listener
    /// </summary>
    public bool ThrowInsteadOfReconnect { get; set; }

    /// <summary>
    /// Nr of leading bytes that indicate the message length that will follow it.
    /// Set to null to disable
    /// </summary>
    public short LeadingMessageLengthBytes { get; set; }

    /// <summary>
    /// True if the meading message length bytes are little endian, false if big endian.
    /// </summary>
    public bool LittleEndian { get; set; }

    /// <summary>Creates a new object that is a copy of the current instance.</summary>
    /// <returns>A new object that is a copy of this instance.</returns>
    public object Clone()
    {
        return new TcpClientSettings
        {
            Logger = Logger,
            Host = Host,
            Port = Port,
            SendReadTimeout = SendReadTimeout,
            IpStackBufferSize = IpStackBufferSize,
            StxCharacters = StxCharacters?.ToArray(),
            EtxCharacters = EtxCharacters?.ToArray(),
            ThrowInsteadOfReconnect = ThrowInsteadOfReconnect,
            LeadingMessageLengthBytes = LeadingMessageLengthBytes,
            LittleEndian = LittleEndian
        };
    }
}