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
    public class TcpSenderTests
    {
        private const string LocalHost = "127.0.0.1";
        private const int Port = 514;
        private Mutex _mutex = new Mutex(false, nameof(TcpSenderTests));

        [Theory]
        [InlineData("qwertyuiop")]
        [InlineData("qwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiop")]
        public void TestSendWithoutLength(string testData)
        {
            _mutex.WaitOne(30000);
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
                        var returnedData = mock.ReadTcpData();
                        returnedData.Should().Be(testData);

                        // Send a response back to the client and immediately close the connection.
                        mock.SendData("ACK");

                        receiverClient.Stop();
                        return;
                    }

                    await Task.Delay(10);
                }

                receiverClient.Stop();
                throw new Exception("Experienced timeout on receiving data");
            });

            // Wait for the reciever to start listening
            are.WaitOne(5000);

            // Start sending data
            var sendTask = Task.Run(async () =>
            {
                using (var sendConnection = new TcpSendConnection(LocalHost, Port, TimeSpan.FromSeconds(30), 10))
                {
                    await sendConnection.SendData(testData, Encoding.UTF8);
                    sendConnection.ReceiveData().Should().Be("ACK");
                }
            });

            if (Task.WaitAll(new[] { receiveTask, sendTask }, 30000) == false)
                throw new TimeoutException("Send or receive task has not completed within the allotted time");

            // Make sure that exceptions on other thread tasks fail the unit test
            if (receiveTask.Exception != null)
                throw receiveTask.Exception;
            if (sendTask.Exception != null)
                throw sendTask.Exception;
        }
    }
}
