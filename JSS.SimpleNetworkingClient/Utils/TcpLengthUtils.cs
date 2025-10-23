using System;
using System.Linq;

namespace JSS.SimpleNetworkingClient.Utils;

public static class TcpLengthUtils
{
    public static int GetMessageLength(byte[] message, short nrOfLeadingBytes, bool littleEndian = false)
    {
        if (message.Length < nrOfLeadingBytes)
            throw new ArgumentException($"Message does not contain enough bytes to read the length. MessageLength: {message.Length}, nrOfLeadingBytes: {nrOfLeadingBytes}");

        var messageLengthBytes = message.AsSpan().Slice(0, nrOfLeadingBytes);
        
        // Reverse the bytes if the arch is big endian
        if (!littleEndian)
            messageLengthBytes.Reverse();

        return BitConverter.ToInt32(messageLengthBytes);
    }

    public static byte[] CreateMessageLengthHeader(byte[] message, short nrOfLeadingBytes, bool littleEndian = false)
    {
        var lengthBytes = BitConverter.GetBytes(message.Length).AsSpan(0, nrOfLeadingBytes);

        // Reverse the bytes if the arch is big endian
        if (!littleEndian)
            lengthBytes.Reverse();

        return lengthBytes.ToArray();
    }
}