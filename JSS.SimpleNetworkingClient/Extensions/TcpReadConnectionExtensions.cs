using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JSS.SimpleNetworkingClient.Extensions
{
    public static class TcpReadConnectionExtensions
    {
        /// <summary>
        /// Returns a task that synchronously waits for the data.
        /// </summary>
        /// <param name="readConnection">Tcp Read connection</param>
        /// <param name="timeout">Timeout to wait for data to be received</param>
        /// <returns></returns>
        public static async Task<string> WaitForData(this TcpReadConnection readConnection, TimeSpan timeout)
        {
            return await Task.Run(() =>
            {
                string result = null;
                var dataReceivedAutoResetEvent = new AutoResetEvent(false);

                readConnection.OnDataReceived = data =>
                {
                    result = data;
                    dataReceivedAutoResetEvent.Set();
                };

                if (dataReceivedAutoResetEvent.WaitOne(readConnection.SendReadTimeout))
                    throw new NetworkingException("", NetworkingException.NetworkingExceptionTypeEnum.ReadTimeout);

                return result;
            });
        }
    }
}
