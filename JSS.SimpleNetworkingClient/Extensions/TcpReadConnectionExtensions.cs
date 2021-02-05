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
        /// Returns a task that waits for the data.
        /// </summary>
        /// <param name="readConnection"></param>
        /// <returns></returns>
        public static Task<string> WaitForData(this TcpReadConnection readConnection)
        {
            return Task.Run(() =>
            {
                string result = null;
                var dataReceivedAutoResetEvent = new AutoResetEvent(false);

                readConnection.OnDataReceived = data =>
                {
                    result = data;
                    dataReceivedAutoResetEvent.Set();
                };

                dataReceivedAutoResetEvent.WaitOne();
                return result;
            });
        }
    }
}
