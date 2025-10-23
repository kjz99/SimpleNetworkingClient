using FluentAssertions;
using JSS.SimpleNetworkingClient.Extensions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using JSS.SimpleNetworkingClient.Utils;

namespace JSS.SimpleNetworkingClient.UnitTests.Integration
{
    public class TcpReaderTests
    {
        private const string LocalHost = "127.0.0.1";
        private const int Port = 514;
        private TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);
        private Mutex _mutex = new Mutex(false, nameof(TcpReaderTests));

        /// <summary>
        /// Test that the TcpReadConnection can receive and respond asynchronously
        /// </summary>
        /// <param name="testData">Data that will be transmitted from the sender to the receiver</param>
        [Theory]
        [InlineData("qwertyuiop")]
        public void SimpleAsyncReadShouldSucceed(string testData)
        {
            _mutex.WaitOne(30000);
            var are = new AutoResetEvent(false);

            // Start the receiving side
            var receiveTask = Task.Run(async () =>
            {
                var dataReceived = new AutoResetEvent(false);
                using var reader = new TcpReadConnection(DefaultSettings);
                reader.OnDataReceived = (returnedData) =>
                {
                    returnedData.Should().Be(testData);
                    reader.SendData("ACK", Encoding.UTF8).Wait(_defaultTimeout);
                    dataReceived.Set();
                };

                reader.StartListening();
                are.Set();
                dataReceived.WaitOne(_defaultTimeout);
            });

            // Wait for the receiver to start listening
            are.WaitOne(5000);

            // Start sending data
            var sendTask = Task.Run(async () =>
            {
                using var sendConnection = new TcpSendConnection(DefaultSettings);
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
        /// Test using the TcpReadConnectionDeadlockSimulator that deadlocks do not lock up the TcpReadConnection logic and it can still be disposed
        /// </summary>
        [Fact]
        public void DeadlockedTcpReaderShouldDisposeNormally()
        {
            var testData = "qwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiop";
            _mutex.WaitOne(30000);
            var are = new AutoResetEvent(false);

            // Start the receiving side
            var receiveTask = Task.Run(async () =>
            {
                using var reader = new TcpReadConnectionDeadlockSimulator(DefaultSettings);
                reader.StartListening();
                are.Set();
                await Task.Delay(5000);
            });

            // Wait for the receiver to start listening
            are.WaitOne(5000);

            // Start sending data
            var sendTask = Task.Run(async () =>
            {
                using var sendConnection = new TcpSendConnection(DefaultSettings);
                await sendConnection.SendData(testData, Encoding.UTF8);
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
            _mutex.WaitOne(30000);
            var receiverReady = new AutoResetEvent(false);
            var cancelReceiverTokenSource = new CancellationTokenSource();

            // Start the receiving side
            var receiveTask = Task.Run(() =>
            {
                while (true)
                {
                    if (cancelReceiverTokenSource.IsCancellationRequested)
                        return;

                    var dataReceived = new AutoResetEvent(false);
                    using var reader = new TcpReadConnection(DefaultSettings);
                    reader.OnDataReceived = (returnedData) =>
                    {
                        returnedData.Should().Be(testData);
                        reader.SendData("ACK", Encoding.UTF8).Wait(_defaultTimeout);
                        receiveCounter++;
                        
                        if (receiveCounter == 100)
                            dataReceived.Set();
                    };

                    reader.StartListening();
                    receiverReady.Set();
                    dataReceived.WaitOne(5000);
                }
            });

            // Start sending data
            var sendTask = Task.Run(async () =>
            {
                for (int i = 0; i < 100; i++)
                {
                    // Wait for the receiver to start listening
                    receiverReady.WaitOne(5000);

                    using var sendConnection = new TcpSendConnection(DefaultSettings);
                    await sendConnection.SendData(testData, Encoding.UTF8);
                    sendConnection.ReceiveDataAsString().Should().Be("ACK");
                }
            });

            if (Task.WaitAll(new[] { receiveTask, sendTask }, TimeSpan.FromMinutes(30)) == false)
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
            var testData = "qwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiopqwertyuiop";
            _mutex.WaitOne(30000);
            var receiverReady = new AutoResetEvent(false);
            var cancelReceiverTokenSource = new CancellationTokenSource();

            // Start the receiving side
            var receiveTask = Task.Run(() =>
            {
                using var reader = new TcpReadConnection(DefaultSettings);
                reader.OnDataReceived = (returnedData) =>
                {
                    returnedData.Should().Be(testData);
                    reader.SendData("ACK", Encoding.UTF8).Wait(_defaultTimeout);
                };

                reader.StartListening();
                receiverReady.Set();

                while (true)
                {
                    if (cancelReceiverTokenSource.IsCancellationRequested) 
                        return;
                }
            });

            // Start sending data
            var sendTask = Task.Run(async () =>
            {
                for (int i = 0; i < 100; i++)
                {
                    // Wait for the receiver to start listening
                    receiverReady.WaitOne(5000);

                    using (var sendConnection = new TcpSendConnection(DefaultSettings))
                    {
                        await sendConnection.SendData(testData, Encoding.UTF8);
                        sendConnection.ReceiveDataAsString().Should().Be("ACK");
                    }

                    using (var sendConnection = new TcpSendConnection(DefaultSettings))
                    {
                        await sendConnection.SendData(testData, Encoding.UTF8);
                        sendConnection.ReceiveDataAsString().Should().Be("ACK");
                    }

                    await Task.Delay(30000);
                }
            });

            if (Task.WaitAll(new[] { receiveTask, sendTask }, TimeSpan.FromMinutes(30)) == false)
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
            _mutex.WaitOne(30000);
            var are = new AutoResetEvent(false);

            // Start the receiving side
            var receiveTask = Task.Run(async () =>
            {
                using var reader = new TcpReadConnection(DefaultSettings);
                reader.StartListening();
                are.Set();
                var result = await reader.WaitForData(_defaultTimeout);
                await reader.SendData("ACK", Encoding.UTF8);
                result.Should().Be(testData);
            });

            // Wait for the receiver to start listening
            are.WaitOne(5000);

            // Start sending data
            var sendTask = Task.Run(async () =>
            {
                using var sendConnection = new TcpSendConnection(DefaultSettings);
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
            _mutex.WaitOne(30000);
            var are = new AutoResetEvent(false);
            var resultCounter = 0;
            string[] testData = [ "qwertyuiop", "asdfghjkl", "zxcvbnm,./", "qwertyuiop", "asdfghjkl", "zxcvbnm,./" ];

            // Start the receiving side
            var receiveTask = Task.Run(async () =>
            {
                var dataReceived = new AutoResetEventEx(false);
                using var reader = new TcpReadConnection(DefaultSettings);
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
                dataReceived.WaitOne(_defaultTimeout);
            });

            // Wait for the receiver to start listening
            are.WaitOne(5000);

            // Start sending data
            var sendTask = Task.Run(async () =>
            {
                using (var sendConnection = new TcpSendConnection(DefaultSettings))
                {
                    foreach (var dataToSend in testData)
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
        
        [Fact]
        public void TcpReaderShouldHandleMultipleMessagesWithMessageLength()
        {
            _mutex.WaitOne(30000);
            var are = new AutoResetEvent(false);
            var resultCounter = 0;
            string[] testData = [ "qwertyuiop", "asdfghjkl", "zxcvbnm,./", "qwertyuiop", "asdfghjkl", "zxcvbnm,./" ];

            var settings = (TcpClientSettings)DefaultSettings.Clone();
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
                dataReceived.WaitOne(_defaultTimeout);
            });

            // Wait for the receiver to start listening
            are.WaitOne(5000);

            // Start sending data
            var sendTask = Task.Run(async () =>
            {
                using (var sendConnection = new TcpSendConnection(settings))
                {
                    foreach (var dataToSend in testData)
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

        private TcpClientSettings DefaultSettings => new ()
            {
                Host = LocalHost,
                Port = Port,
                SendReadTimeout = _defaultTimeout,
                IpStackBufferSize = 1024,
                StxCharacters = [ 0x02 ],
                EtxCharacters = [ 0x03 ]
            };
    }
}
