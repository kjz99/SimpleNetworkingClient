using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JSS.SimpleNetworkingClient
{
    /// <summary>
    /// Defines a TCP send connection that can be used to send data to a remote party.
    /// Warning; please make sure that you always dispose of this class so that unmanaged resources will be released.
    /// This implementation is not guaranteed to be thread safe. If you do any other IO related operations on the same thread, TcpClient unmanaged memory leaks may occur.
    /// </summary>
    /// <see cref="https://github.com/kjz99/SimpleNetworkingClient" />
    public class TcpSendConnection : TcpConnectionBase
    {
        private readonly int _pollWriteTimeout = (int)TimeSpan.FromSeconds(5).TotalMilliseconds * 1000;
        private readonly int _defaultBufferSize = 1024;
        private readonly string _host;
        private readonly int _port;

        // TODO: implement universal logging

        /// <summary>
        /// Ctor; Establishes a new connection to the remote party for sending and or receiving data
        /// </summary>
        /// <param name="host">Hostname or ip address to connect to</param>
        /// <param name="port">Port to use</param>
        /// <param name="sendReadTimeout">Send/Read timeout</param>
        /// <param name="bufferSize">Size of the tcp buffer that determines the amount of bytes that is received/send per chunk</param>
        /// <param name="stxCharacters">Begin of transmission characters, Eg 0x02 for ASCII char STX. Set to null to disable to disable adding/removing stx characters.</param>
        /// <param name="etxCharacters">End of transmission characters, Eg 0x03 for ASCII char ETX. Set to null to disable end of transmission checking.</param>
        public TcpSendConnection(string host, int port, TimeSpan sendReadTimeout, int bufferSize, IList<byte> stxCharacters = null, IList<byte> etxCharacters = null) : base(sendReadTimeout, bufferSize)
        {
            _host = host;
            _port = port;
            _stxCharacters = stxCharacters;
            _etxCharacters = etxCharacters;

            StartConnection();
        }

        /// <summary>
        /// Starts the connection to the remote party
        /// </summary>
        private void StartConnection()
        {
            //TODO: implement ip address validation

            try
            {
                _tcpClient = new TcpClient();
                
                // Let the connection remain open for x seconds after calling Close() if data still needs to be transmitted
                _tcpClient.Client.LingerState.Enabled = true;
                _tcpClient.Client.LingerState.LingerTime = 2; // 2 seconds

                // Connect and set the send/receive timeout
                if (_tcpClient?.Client != null)
                    _tcpClient.Client.SendTimeout = _tcpClient.Client.ReceiveTimeout = (int)_sendReadTimeout.TotalMilliseconds;

                if (_tcpClient.ConnectAsync(_host, _port).Wait(_sendReadTimeout) == false)
                    throw new TimeoutException();
            }
            catch (Exception ex)
            {
                throw new NetworkingException($"Failed to connect to the remote party at '{_host}:{_port}'. Please check that the remote party is listening and the connection is not blocked by a virus scanner or firewall", NetworkingException.NetworkingExceptionTypeEnum.ConnectionSetupFailed, ex);
            }
        }

        /// <summary>
        /// Attempt to receive data on the send connection.
        /// This method blocks until the remote party disconnected, the receive timeout expired or the endOfStreamCharacters have been found.
        /// </summary>
        /// <returns>
        /// Data received from the remote party. If the stx/etx character has been set using the constructor, they will be removed from the begin/end of the received data string
        /// </returns>
        public string ReceiveData()
        {
            return ReadTcpData(_stxCharacters, _etxCharacters);
        }
    }
}
