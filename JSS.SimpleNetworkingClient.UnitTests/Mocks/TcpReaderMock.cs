using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace JSS.SimpleNetworkingClient.UnitTests.Mocks
{
    public class TcpReaderMock : TcpConnectionBase
    {
        public TcpReaderMock() : base(TimeSpan.FromSeconds(5), 16)
        {

        }

        public async Task<string> ReadTcpData(TcpClient client)
        {
            _tcpClient = client;
            return await base.ReadTcpData();
        }

        public async Task<string> ReadTcpData(Socket socket)
        {
            return await base.ReadTcpData(socket);
        }

        public async Task<string> ReadTcpDataWithLength(TcpClient client)
        {
            _tcpClient = client;
            return await base.ReadTcpDataWithLengthHeader();
        }
    }
}
