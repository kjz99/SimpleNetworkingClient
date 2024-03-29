﻿using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace JSS.SimpleNetworkingClient.UnitTests.Mocks
{
    /// <summary>
    /// Tcp reader mock that accepts already existing sockets
    /// </summary>
    public class TcpReaderMock : TcpConnectionBase
    {
        public TcpReaderMock(TcpClient client) : base(null, TimeSpan.FromSeconds(5), 16)
        {
            _tcpClient = client;
        }

        public string ReadTcpData()
        {
            return base.ReadTcpData(new List<byte>() { 0x02 }, new List<byte>() { 0x03 });
        }

        public async Task<string> ReadTcpDataWithLength()
        {
            return await base.ReadTcpDataWithLengthHeader();
        }

        public void SendData(string data)
        {
            _stxCharacters = new List<byte>() { 0x02 };
            _etxCharacters = new List<byte>() { 0x03 };
            SendData(data, Encoding.UTF8, 0).Wait(10000);
        }
    }
}
