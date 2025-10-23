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
        public TcpReaderMock(TcpClient client, TcpClientSettings settings) : base(settings)
        {
            TcpClient = client;
        }

        public string ReadTcpData()
        {
            return base.ReadTcpDataAsString([ 0x02 ], [ 0x03 ]);
        }

        public async Task<string> ReadTcpDataWithLength()
        {
            return await base.ReadTcpDataWithLengthHeader([], []);
        }

        public void SendData(string data)
        {
            SendData(data, Encoding.UTF8, 0).Wait(10000);
        }
    }
}
