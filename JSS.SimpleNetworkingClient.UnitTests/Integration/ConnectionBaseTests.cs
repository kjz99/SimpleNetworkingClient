using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using JSS.SimpleNetworkingClient.UnitTests.Mocks;
using Xunit;

namespace JSS.SimpleNetworkingClient.UnitTests.Integration
{
    public class ConnectionBaseTests
    {
        private const string LocalHost = "127.0.0.1";
        private const int Port = 514;
        private Mutex _mutex = new Mutex(false, nameof(ConnectionBaseTests));

        // Test reading data and receiving the correct amount of data
        // Test reading data and receiving 1 byte too little
        // Test reading data and receiving 1 byte too much

        [Theory]
        [InlineData("qwertyuiop")]
        [InlineData("qwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiop")]
        public void TestReadingBaseWithLength(string testData)
        {
            _mutex.WaitOne(5000);
            var are = new AutoResetEvent(false);

            // Start the receiving side
            var receiveTask = Task.Run(async () =>
            {
                TcpListener receiverClient = new TcpListener(IPAddress.Any, Port);
                receiverClient.Start();
                are.Set();

                for (int i = 0; i < 500; i++)
                {
                    if (receiverClient.Pending())
                    {
                        var mock = new TcpReaderMock(await receiverClient.AcceptTcpClientAsync());
                        var returnedData = await mock.ReadTcpDataWithLength();
                        returnedData.Should().Be(testData);
                        receiverClient.Stop();
                        return;
                    }

                    await Task.Delay(10);
                }

                receiverClient.Stop();
                throw new Exception("Experienced timeout on receiving data");
            });

            are.WaitOne(5000);

            // Start sending data
            var sendTask = Task.Run(() =>
            {
                TcpClient senderClient = new TcpClient(LocalHost, Port);
                byte[] data = Encoding.UTF8.GetBytes(testData);
                byte[] buffer = ((byte[]) new TcpLengthStruct(testData.Length)).Concat(data).ToArray();
                var sendStream = senderClient.GetStream();
                sendStream.Write(buffer, 0, buffer.Length);
                sendStream.Flush();
            });

            if (Task.WaitAll(new[] { receiveTask, sendTask }, 5000) == false)
                throw new TimeoutException();

            // Make sure that exceptions on other thread tasks fail the unit test
            if (receiveTask.Exception != null)
                throw receiveTask.Exception;
            if (sendTask.Exception != null)
                throw sendTask.Exception;
        }
    }
}
