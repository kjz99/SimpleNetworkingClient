using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace JSS.SimpleNetworkingClient.Utils
{
    public static class StringUtils
    {
        /// <summary>
        /// Formats a list of bytes into a hexadecimal string notation string
        /// </summary>
        /// <param name="byteList">List opf bytes to parse</param>
        /// <returns>Bytes in hexadecimal notation. Eg, 0x02 0x63 0x03</returns>
        public static string ByteEnumerableToHexString(IEnumerable<byte> byteList) => byteList != null ? string.Join(" ", byteList.Select(s => $"0x{s:X}")) : "";
    }
}
