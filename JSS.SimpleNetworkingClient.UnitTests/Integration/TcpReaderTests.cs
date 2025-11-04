using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using JSS.SimpleNetworkingClient.Extensions;
using JSS.SimpleNetworkingClient.Utils;
using Xunit;

namespace JSS.SimpleNetworkingClient.UnitTests.Integration
{
    public class TcpReaderTests
    {
        /// <summary>
        /// Test that the TcpReadConnection can receive and respond asynchronously with a string
        /// </summary>
        /// <param name="testData">Data that will be transmitted from the sender to the receiver</param>
        [Theory]
        [InlineData("qwertyuiop")]
        public void SimpleAsyncStringReadShouldSucceed(string testData)
        {
            var are = new AutoResetEvent(false);

            // Start the receiving side
            var receiveTask = Task.Run(async () =>
            {
                var dataReceived = new AutoResetEvent(false);
                using var reader = new TcpReadConnection(TcpClientSettingFactory.DefaultSettings);
                reader.OnDataReceived = (returnedData) =>
                {
                    returnedData.Should().Be(testData);
                    reader.SendData("ACK", Encoding.UTF8).Wait(TcpClientSettingFactory.DefaultSettings.SendReadTimeout);
                    dataReceived.Set();
                };

                reader.StartListening();
                are.Set();
                dataReceived.WaitOne(TcpClientSettingFactory.DefaultSettings.SendReadTimeout);
            });

            // Wait for the receiver to start listening
            are.WaitOne(5000);

            // Start sending data
            var sendTask = Task.Run(async () =>
            {
                using var sendConnection = new TcpSendConnection(TcpClientSettingFactory.DefaultSettings);
                await sendConnection.SendData(testData, Encoding.UTF8);
                sendConnection.ReceiveDataAsString().Should().Be("ACK");
            });

            if (Task.WaitAll(new[] { receiveTask, sendTask }, 30000) == false)
                throw new TimeoutException("Send or receive task has not completed within the allotted time");

            // Make sure that exceptions on other thread tasks fail the unit test
            if (receiveTask.Exception != null)
                throw receiveTask.Exception;
            if (sendTask.Exception != null)
                throw sendTask.Exception;
        }
        
        /// <summary>
        /// Test that the TcpReadConnection can receive and respond asynchronously with a byte array and a length header
        /// </summary>
        /// <param name="testData">Data that will be transmitted from the sender to the receiver</param>
        [Theory]
        [InlineData(new byte[] { 0x30, 0x31, 0x32, 0x33 })]
        [InlineData(new byte[] { 0x30, 0x31, 0x03, 0x33 })]
        public void SimpleAsyncByteArrayReadWithLengthHeaderShouldSucceed(byte[] testData)
        {
            var are = new AutoResetEvent(false);

            // Start the receiving side
            var receiveTask = Task.Run(async () =>
            {
                var settings = (TcpClientSettings)TcpClientSettingFactory.DefaultSettings.Clone();
                settings.LeadingMessageLengthBytes = 1;
                var dataReceived = new AutoResetEvent(false);
                using var reader = new TcpReadConnection(settings);
                reader.OnDataReceived = (returnedData) =>
                {
                    returnedData.Should().Be(Encoding.Default.GetString(testData));
                    reader.SendData(testData).Wait(TcpClientSettingFactory.DefaultSettings.SendReadTimeout);
                    dataReceived.Set();
                };

                reader.StartListening();
                are.Set();
                dataReceived.WaitOne(TcpClientSettingFactory.DefaultSettings.SendReadTimeout);
            });

            // Wait for the receiver to start listening
            are.WaitOne(5000);

            // Start sending data
            var sendTask = Task.Run(async () =>
            {
                var settings = (TcpClientSettings)TcpClientSettingFactory.DefaultSettings.Clone();
                settings.LeadingMessageLengthBytes = 1;
                using var sendConnection = new TcpSendConnection(settings);
                await sendConnection.SendData(testData);
                sendConnection.ReceiveDataAsByteArray().Should().BeEquivalentTo(testData);
            });

            if (Task.WaitAll(new[] { receiveTask, sendTask }, 30000) == false)
                throw new TimeoutException("Send or receive task has not completed within the allotted time");

            // Make sure that exceptions on other thread tasks fail the unit test
            if (receiveTask.Exception != null)
                throw receiveTask.Exception;
            if (sendTask.Exception != null)
                throw sendTask.Exception;
        }
        
        /// <summary>
        /// Test that the TcpReadConnection can receive and respond asynchronously with a string and a length header
        /// </summary>
        /// <param name="testData">Data that will be transmitted from the sender to the receiver</param>
        [Theory]
        [InlineData(new byte[] { 0x30, 0x31, 0x32, 0x33 })]
        [InlineData(new byte[] { 0x30, 0x31, 0x03, 0x33 })]
        public void SimpleAsyncStringReadWithLengthHeaderShouldSucceed(byte[] testData)
        {
            var are = new AutoResetEvent(false);

            // Start the receiving side
            var receiveTask = Task.Run(async () =>
            {
                var settings = (TcpClientSettings)TcpClientSettingFactory.DefaultSettings.Clone();
                settings.LeadingMessageLengthBytes = 1;
                var dataReceived = new AutoResetEvent(false);
                using var reader = new TcpReadConnection(settings);
                reader.OnDataReceived = (returnedData) =>
                {
                    returnedData.Should().Be(Encoding.Default.GetString(testData));
                    reader.SendData(testData).Wait(TcpClientSettingFactory.DefaultSettings.SendReadTimeout);
                    dataReceived.Set();
                };

                reader.StartListening();
                are.Set();
                dataReceived.WaitOne(TcpClientSettingFactory.DefaultSettings.SendReadTimeout);
            });

            // Wait for the receiver to start listening
            are.WaitOne(5000);

            // Start sending data
            var sendTask = Task.Run(async () =>
            {
                var settings = (TcpClientSettings)TcpClientSettingFactory.DefaultSettings.Clone();
                settings.LeadingMessageLengthBytes = 1;
                using var sendConnection = new TcpSendConnection(settings);
                await sendConnection.SendData(testData);
                sendConnection.ReceiveDataAsString().Should().Be(Encoding.Default.GetString(testData));
            });

            if (Task.WaitAll(new[] { receiveTask, sendTask }, 30000) == false)
                throw new TimeoutException("Send or receive task has not completed within the allotted time");

            // Make sure that exceptions on other thread tasks fail the unit test
            if (receiveTask.Exception != null)
                throw receiveTask.Exception;
            if (sendTask.Exception != null)
                throw sendTask.Exception;
        }

        /// <summary>
        /// Test that the TcpReadConnection can receive and respond asynchronously on multiple requests
        /// </summary>
        [Fact]
        public void ReadConnectionWithALotOfSeparateRequestsShouldSucceed()
        {
            var receiveCounter = 0;
            var testData = "qwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiop";
            var cancelReceiverTokenSource = new CancellationTokenSource();
            var are = new AutoResetEvent(false);

            // Start the receiving side
            var receiveTask = Task.Run(() =>
            {
                if (cancelReceiverTokenSource.IsCancellationRequested)
                    return;

                var dataReceived = new AutoResetEvent(false);
                using var reader = new TcpReadConnection(TcpClientSettingFactory.DefaultSettings);
                reader.OnDataReceived = (returnedData) =>
                {
                    returnedData.Should().Be(testData);
                    reader.SendData("ACK", Encoding.UTF8).Wait(TcpClientSettingFactory.DefaultSettings.SendReadTimeout);
                    receiveCounter++;

                    if (receiveCounter == 100)
                        dataReceived.Set();
                };

                reader.StartListening();
                are.Set();
                dataReceived.WaitOne(1000_000);
            });

            are.WaitOne(5_000);

            // Start sending data
            var sendTask = Task.Run(async () =>
            {
                for (int i = 0; i < 100; i++)
                {
                    using var sendConnection = new TcpSendConnection(TcpClientSettingFactory.DefaultSettings);
                    await sendConnection.SendData(testData, Encoding.UTF8);
                    sendConnection.ReceiveDataAsString().Should().Be("ACK");
                }
            });

            if (Task.WaitAll(new[] { receiveTask, sendTask }, TimeSpan.FromMinutes(1)) == false)
                throw new TimeoutException("Send or receive task has not completed within the allotted time");

            // Make sure that exceptions on other thread tasks fail the unit test
            if (receiveTask.Exception != null)
                throw receiveTask.Exception;
            if (sendTask.Exception != null)
                throw sendTask.Exception;
        }

        /// <summary>
        /// Test that the TcpReadConnection can handle mutiple requests at once
        /// </summary>
        [Fact]
        public void ConsecutiveClientsShouldBeAbleToConnectAndSendData()
        {
            var receiveCounter = 0;
            var testData = "qwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiop";
            var cancelReceiverTokenSource = new CancellationTokenSource();
            var are = new AutoResetEvent(false);

            // Start the receiving side
            var receiveTask = Task.Run(() =>
            {
                if (cancelReceiverTokenSource.IsCancellationRequested)
                    return;

                var dataReceived = new AutoResetEvent(false);
                using var reader = new TcpReadConnection(TcpClientSettingFactory.DefaultSettings);
                reader.OnDataReceived = (returnedData) =>
                {
                    returnedData.Should().Be(testData);
                    reader.SendData("ACK", Encoding.UTF8).Wait(TcpClientSettingFactory.DefaultSettings.SendReadTimeout);
                    receiveCounter++;

                    if (receiveCounter == 100)
                        dataReceived.Set();
                };

                reader.StartListening();
                are.Set();
                dataReceived.WaitOne(1000_000);
            });

            are.WaitOne(5_000);

            // Start sending data
            var sendTask = Task.Run(async () =>
            {
                using var sendConnection = new TcpSendConnection(TcpClientSettingFactory.DefaultSettings);
                
                for (int i = 0; i < 100; i++)
                {
                    await sendConnection.SendData(testData, Encoding.UTF8);
                    sendConnection.ReceiveDataAsString().Should().Be("ACK");
                }
            });

            if (Task.WaitAll(new[] { receiveTask, sendTask }, TimeSpan.FromMinutes(1)) == false)
                throw new TimeoutException("Send or receive task has not completed within the allotted time");

            // Make sure that exceptions on other thread tasks fail the unit test
            if (receiveTask.Exception != null)
                throw receiveTask.Exception;
            if (sendTask.Exception != null)
                throw sendTask.Exception;
        }

        /// <summary>
        /// Test that the TcpReadConnection can receive and respond synchronously using the WaitForData extension method
        /// </summary>
        /// <param name="testData">Data that will be transmitted from the sender to the receiver</param>
        [Theory]
        [InlineData("abc")]
        [InlineData("qazwsxedcrfv")]
        [InlineData("qwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiop")]
        public void TestReadConnection(string testData)
        {
            var are = new AutoResetEvent(false);

            // Start the receiving side
            var receiveTask = Task.Run(async () =>
            {
                using var reader = new TcpReadConnection(TcpClientSettingFactory.DefaultSettings);
                reader.StartListening();
                are.Set();
                var result = await reader.WaitForData(TcpClientSettingFactory.DefaultSettings.SendReadTimeout);
                await reader.SendData("ACK", Encoding.UTF8);
                result.Should().Be(testData);
            });

            // Wait for the receiver to start listening
            are.WaitOne(5000);

            // Start sending data
            var sendTask = Task.Run(async () =>
            {
                using var sendConnection = new TcpSendConnection(TcpClientSettingFactory.DefaultSettings);
                await sendConnection.SendData(testData, Encoding.UTF8);
                sendConnection.ReceiveDataAsString().Should().Be("ACK");
            });

            if (Task.WaitAll(new[] { receiveTask, sendTask }, 30000) == false)
                throw new TimeoutException("Send or receive task has not completed within the allotted time");

            // Make sure that exceptions on other thread tasks fail the unit test
            if (receiveTask.Exception != null)
                throw receiveTask.Exception;
            if (sendTask.Exception != null)
                throw sendTask.Exception;
        }

        /// <summary>
        /// TCP is a streaming protocol, so it is possible to receive multiple messages in one transmission if they are send fast enough after each other.
        /// The client should thus be able to handle multiple messages starting and ending with the stx and etx bytes.
        /// reader.OnDataReceived should then be triggered multiple times.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        [Fact]
        public void TcpReaderShouldHandleMultipleMessagesInOneTransmission()
        {
            var are = new AutoResetEvent(false);
            var resultCounter = 0;
            string[] testData = [ "qwertyuiop", "asdfghjkl", "zxcvbnm,./", "qwertyuiop", "asdfghjkl", "zxcvbnm,./" ];

            // Start the receiving side
            var receiveTask = Task.Run(async () =>
            {
                var dataReceived = new AutoResetEventEx(false);
                using var reader = new TcpReadConnection(TcpClientSettingFactory.DefaultSettings);
                reader.OnDataReceived = (returnedData) =>
                {
                    try
                    {
                        returnedData.Should().Be(testData[resultCounter]);
                        resultCounter++;

                        if (resultCounter == testData.Length - 1)
                            dataReceived.Set();
                    }
                    catch (Exception e)
                    {
                        dataReceived.Set(e);
                    }
                };

                reader.StartListening();
                are.Set();
                dataReceived.WaitOne(TcpClientSettingFactory.DefaultSettings.SendReadTimeout);
            });

            // Wait for the receiver to start listening
            are.WaitOne(5000);

            // Start sending data
            var sendTask = Task.Run(async () =>
            {
                using var sendConnection = new TcpSendConnection(TcpClientSettingFactory.DefaultSettings);
                foreach (var dataToSend in testData)
                    await sendConnection.SendData(dataToSend, Encoding.UTF8);
            });

            if (Task.WaitAll(new[] { receiveTask, sendTask }, 30000) == false)
                throw new TimeoutException("Send or receive task has not completed within the allotted time");

            // Make sure that exceptions on other thread tasks fail the unit test
            if (receiveTask.Exception != null)
                throw receiveTask.Exception;
            if (sendTask.Exception != null)
                throw sendTask.Exception;
        }

        [Fact]
        public void TcpReaderShouldHandleMultipleMessagesWithMessageLength()
        {
            var are = new AutoResetEvent(false);
            var resultCounter = 0;
            string[] testData = [ "qwertyuiop", "asdfghjkl", "zxcvbnm,./", "qwertyuiop", "asdfghjkl", "zxcvbnm,./" ];

            var settings = (TcpClientSettings)TcpClientSettingFactory.DefaultSettings.Clone();
            settings.LeadingMessageLengthBytes = 4;

            // Start the receiving side
            var receiveTask = Task.Run(async () =>
            {
                var dataReceived = new AutoResetEventEx(false);
                using var reader = new TcpReadConnection(settings);
                reader.OnDataReceived = (returnedData) =>
                {
                    try
                    {
                        returnedData.Should().Be(testData[resultCounter]);
                        resultCounter++;

                        if (resultCounter == testData.Length)
                            dataReceived.Set();
                    }
                    catch (Exception e)
                    {
                        dataReceived.Set(e);
                    }
                };

                reader.StartListening();
                are.Set();
                dataReceived.WaitOne(TcpClientSettingFactory.DefaultSettings.SendReadTimeout);
            });

            // Wait for the receiver to start listening
            are.WaitOne(5000);

            // Start sending data
            var sendTask = Task.Run(async () =>
            {
                using var sendConnection = new TcpSendConnection(settings);
                foreach (var dataToSend in testData)
                {
                    await sendConnection.SendData(dataToSend, Encoding.UTF8);
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