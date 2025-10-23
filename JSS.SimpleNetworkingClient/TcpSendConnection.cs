using JSS.SimpleNetworkingClient.Interfaces;
using System;
using System.Collections.Generic;

namespace JSS.SimpleNetworkingClient;

/// <summary>
/// Defines a TCP send connection that can be used to send data to a remote party.
/// Warning; please make sure that you always dispose of this class so that unmanaged resources will be released.
/// This implementation is not guaranteed to be thread safe. If you do any other IO related operations on the same thread, TcpClient unmanaged memory leaks may occur.
/// </summary>
/// <see cref="https://github.com/kjz99/SimpleNetworkingClient" />
public class TcpSendConnection : TcpConnectionBase, IDisposable
{
    private readonly int _pollWriteTimeout = (int)TimeSpan.FromSeconds(5).TotalMilliseconds * 1000;

    /// <summary>
    /// Ctor; Establishes a new connection to the remote party for sending and or receiving data
    /// </summary>
    /// <param name="settings">Settings for the tcp client</param>)
    public TcpSendConnection(TcpClientSettings settings) : base(settings)
    {
        StartConnection();
    }

    /// <summary>
    /// Starts the connection to the remote party
    /// </summary>
    private void StartConnection()
    {
        try
        {
            TcpClient = new();

            // Let the connection remain open for x seconds after calling Close() if data still needs to be transmitted
            TcpClient.Client.LingerState.Enabled = true;
            TcpClient.Client.LingerState.LingerTime = 2; // 2 seconds

            // Connect and set the send/receive timeout
            if (TcpClient?.Client != default)
                TcpClient.Client.SendTimeout = TcpClient.Client.ReceiveTimeout = (int) Settings.SendReadTimeout.TotalMilliseconds;

            if (TcpClient.ConnectAsync(Settings.Host, Settings.Port).Wait(Settings.SendReadTimeout) == false)
                throw new TimeoutException();
            else
                Settings.Logger?.Info($"{nameof(TcpSendConnection)} on port {Settings.Port} has been started successfully");
        }
        catch (Exception ex)
        {
            Dispose();
            throw new NetworkingException($"Failed to connect to the remote party at '{Settings.Host}:{Settings.Port}'. Please check that the remote party is listening and the connection is not blocked by a virus scanner or firewall", NetworkingException.NetworkingExceptionTypeEnum.ConnectionSetupFailed, ex);
        }
    }

    /// <summary>
    /// Attempt to receive data on the send connection.
    /// This method blocks until the remote party disconnected, the receive timeout expired or the endOfStreamCharacters have been found.
    /// </summary>
    /// <returns>
    /// Data received from the remote party. If the stx/etx character has been set using the constructor, they will be removed from the begin/end of the received data string
    /// </returns>
    public string ReceiveDataAsString()
        => ReadTcpDataAsString(Settings.StxCharacters, Settings.EtxCharacters);
    
    /// <summary>
    /// Attempt to receive data on the send connection. 
    /// This method blocks until the remote party disconnected, the receive timeout expired or the endOfStreamCharacters have been found. 
    /// </summary>
    /// <returns>
    /// Data received from the remote party. If the stx/etx character has been set using the constructor, they will be removed from the begin/end of the received data string.
    /// </returns>
    public byte[] ReceiveDataAsByteArray()
        => ReadTcpData(Settings.StxCharacters, Settings.EtxCharacters);
}
