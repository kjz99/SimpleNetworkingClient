using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace JSS.SimpleNetworkingClient
{
    /// <summary>
    /// Defines the first 4 bytes of a tcp packet, that indicate the number of bytes that will be transmitted
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct TcpLengthStruct
    {
        [FieldOffset(0)]
        public int Value;

        [FieldOffset(0)]
        public byte Byte0;

        [FieldOffset(1)]
        public byte Byte1;

        [FieldOffset(2)]
        public byte Byte2;

        [FieldOffset(3)]
        public byte Byte3;

        public TcpLengthStruct(int value)
        {
            Byte0 = Byte1 = Byte2 = Byte3 = 0;
            Value = value;
        }

        public TcpLengthStruct(byte[] value)
        {
            Byte0 = Byte1 = Byte2 = Byte3 = 0;
            Value = 0;

            if (value.Length != 4)
                throw new ArgumentException("A 4 byte array is expected", nameof(value));

            Byte0 = value[0];
            Byte1 = value[1];
            Byte2 = value[2];
            Byte3 = value[3];
        }

        public static implicit operator Int32(TcpLengthStruct value)
        {
            return value.Value;
        }

        public static implicit operator TcpLengthStruct(int value)
        {
            return new TcpLengthStruct(value);
        }

        public static implicit operator byte[](TcpLengthStruct value)
        {
            return new byte[] { value.Byte0, value.Byte1, value.Byte2, value.Byte3 };
        }

        public static implicit operator TcpLengthStruct(byte[] value)
        {
            return new TcpLengthStruct(value);
        }
    }
}
