using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace JSS.SimpleNetworkingClient.UnitTests.Integration;

public class FaultTests
{
    /// <summary>
    /// Test that checks if the connection to the TcpReceiveConnection closes when the other side closes the connection
    /// </summary>
    /// <param name="testData">Data that will be transmitted from the sender to the receiver</param>
    [Fact]
    public void SimpleAsyncByteArrayReadWithLengthHeaderShouldSucceed()
    {
        byte[] testData = [ 0x30, 0x31, 0x32, 0x33 ];
        var are = new AutoResetEvent(false);
        var dataReceived = new AutoResetEvent(false);

        // Start the receiving side
        var receiveTask = Task.Run(() =>
        {
            var settings = (TcpClientSettings)TcpClientSettingFactory.DefaultSettings.Clone();
            settings.LeadingMessageLengthBytes = 1;
            using var reader = new TcpReadConnection(settings);
            var connectionClosed = false;
            reader.OnConnectionClosed = () =>
            {
                connectionClosed = true;
            };

            reader.StartListening();
            are.Set();
            dataReceived.WaitOne(TcpClientSettingFactory.DefaultSettings.SendReadTimeout);
            if (!connectionClosed)
                throw new Exception("Connection was not closed");
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
            await Task.Delay(1000);
            sendConnection.Dispose();
            await Task.Delay(1000);
            dataReceived.Set();
        });

        if (Task.WaitAll(new[] { receiveTask, sendTask }, 30000) == false)
        {
            if (receiveTask.Exception != null)
                throw receiveTask.Exception;
            if (sendTask.Exception != null)
                throw sendTask.Exception;
            
            throw new TimeoutException("Send or receive task has not completed within the allotted time");
        }
    }
}