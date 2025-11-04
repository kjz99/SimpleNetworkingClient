using System;

namespace JSS.SimpleNetworkingClient.Utils;

public static class TcpLengthUtils
{
    public static int GetMessageLength(byte[] message, short nrOfLeadingBytes, bool littleEndian = false)
    {
        if (nrOfLeadingBytes > 4)
            throw new ArgumentOutOfRangeException($"NrOfLeadingBytes cannot be larger than 4. MessageLength: {message.Length}, nrOfLeadingBytes: {nrOfLeadingBytes}");
        
        if (message.Length < nrOfLeadingBytes)
            throw new ArgumentException($"Message does not contain enough bytes to read the length. MessageLength: {message.Length}, nrOfLeadingBytes: {nrOfLeadingBytes}");

        var messageLengthBytes = message.AsSpan().Slice(0, nrOfLeadingBytes);
        
        // Reverse the bytes if the arch is little endian
        if (littleEndian)
            messageLengthBytes.Reverse();

        // BitConverter.ToInt32 requires exactly 4 bytes. Pad it with 0x00 bytes if the length is less than 4.
        if (messageLengthBytes.Length < 4)
        {
            var paddedBytes = new byte[4];
            messageLengthBytes.CopyTo(paddedBytes);
            messageLengthBytes = paddedBytes;
        }
        
        return BitConverter.ToInt32(messageLengthBytes);
    }

    public static byte[] CreateMessageLengthHeader(byte[] message, short nrOfLeadingBytes, bool littleEndian = false)
    {
        var lengthBytes = BitConverter.GetBytes(message.Length + nrOfLeadingBytes).AsSpan(0, nrOfLeadingBytes);

        // Reverse the bytes if the arch is little endian
        if (littleEndian)
            lengthBytes.Reverse();

        return lengthBytes.ToArray();
    }
}