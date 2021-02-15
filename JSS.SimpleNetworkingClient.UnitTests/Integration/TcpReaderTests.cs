using FluentAssertions;
using JSS.SimpleNetworkingClient.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace JSS.SimpleNetworkingClient.UnitTests.Integration
{
    public class TcpReaderTests
    {
        private const string LocalHost = "127.0.0.1";
        private const int Port = 514;
        private TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);
        private Mutex _mutex = new Mutex(false, nameof(TcpReaderTests));

        [Theory]
        [InlineData("qwertyuiop")]
        [InlineData("qwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiop")]
        public void TestReadConnectionAsync(string testData)
        {
            _mutex.WaitOne(30000);
            var are = new AutoResetEvent(false);

            // Start the receiving side
            var receiveTask = Task.Run(async () =>
            {
                var dataReceived = new AutoResetEvent(false);
                using (var reader = new TcpReadConnection(null, Port, _defaultTimeout, 10, new List<byte>() { 0x02 }, new List<byte>() { 0x03 }))
                {
                    reader.OnDataReceived = (returnedData) =>
                    {
                        returnedData.Should().Be(testData);
                        reader.SendData("ACK", Encoding.UTF8).Wait(_defaultTimeout);
                        dataReceived.Set();
                    };

                    reader.StartListening();
                    are.Set();
                    dataReceived.WaitOne(_defaultTimeout);
                }
            });

            // Wait for the receiver to start listening
            are.WaitOne(5000);

            // Start sending data
            var sendTask = Task.Run(async () =>
            {
                using (var sendConnection = new TcpSendConnection(null, LocalHost, Port, TimeSpan.FromSeconds(30), 10, new List<byte>() { 0x02 }, new List<byte>() { 0x03 }))
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

        [Theory]
        [InlineData("abc")]
        [InlineData("qazwsxedcrfv")]
        [InlineData("qwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiop")]
        public void TestReadConnection(string testData)
        {
            _mutex.WaitOne(30000);
            var are = new AutoResetEvent(false);

            // Start the receiving side
            var receiveTask = Task.Run(async () =>
            {
                using (var reader = new TcpReadConnection(null, Port, _defaultTimeout, 10, new List<byte>() { 0x02 }, new List<byte>() { 0x03 }))
                {
                    reader.StartListening();
                    are.Set();
                    var result = await reader.WaitForData(_defaultTimeout);
                    await reader.SendData("ACK", Encoding.UTF8);
                    result.Should().Be(testData);
                }
            });

            // Wait for the receiver to start listening
            are.WaitOne(5000);

            // Start sending data
            var sendTask = Task.Run(async () =>
            {
                using (var sendConnection = new TcpSendConnection(null, LocalHost, Port, TimeSpan.FromSeconds(30), 10, new List<byte>() { 0x02 }, new List<byte>() { 0x03 }))
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
