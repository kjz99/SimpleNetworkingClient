using JSS.SimpleNetworkingClient.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace JSS.SimpleNetworkingClient;

/// <summary>
/// Manages a new tcp listening connection on the given port.
/// Warning; please make sure that you always dispose of this class so that unmanaged resources will be released.
/// This implementation is guaranteed to be thread safe because the tcp listener and tcp IO operations run on a separate thread.
/// </summary>
/// <see cref="https://github.com/kjz99/SimpleNetworkingClient" />
public class TcpReadConnection : TcpConnectionBase, IDisposable
{
    private bool _pendingRequestActive = false;
    private Task _listenerTask;
    private CancellationTokenSource _cancellationTokenSource;
    private TcpListener _tcpListener;
    private AutoResetEvent _startListeningCompletedEvent;

    /// <summary>
    /// Ctor; Starts a new tcp listener 
    /// </summary>
    /// <param name="settings">Settings for the tcp client</param>
    public TcpReadConnection(TcpClientSettings settings) : base(settings)
    {
        
    }

    /// <summary>
    /// Start listening for new incoming connections
    /// </summary>
    public void StartListening()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _startListeningCompletedEvent = new AutoResetEvent(false);
        _listenerTask = Task.Run(async () => await ConnectionListenerImpl(), _cancellationTokenSource.Token);
        _startListeningCompletedEvent.WaitOne(Settings.SendReadTimeout);
    }

    private async Task ConnectionListenerImpl()
    {
        while (true)
        {
            try
            {
                if (_tcpListener == default)
                    StartTcpListener();

                while (true)
                {
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    if (_pendingRequestActive && TcpClient == default)
                        // tcp client has been disposed, indicating the last request has ended and the connection has been closed
                        _pendingRequestActive = false;

                    if (!_tcpListener.Pending() || _pendingRequestActive)
                    {
                        // No pending requests are available or a pending request is being handled
                        await Task.Delay(100);
                        continue;
                    }

                    if (_pendingRequestActive)
                    {
                        Settings.Logger?.Warn($"A second pending request has been detected on port {Settings.Port}, which is not supported. The request will be ignored until the other request has ended");
                        continue;
                    }

                    // First pending request is available
                    _pendingRequestActive = true;
                    Settings.Logger?.Debug($"First pending request has been detected on port {Settings.Port}");
                    
                    _tcpListener.BeginAcceptTcpClient(ar =>
                    {
                        try
                        {
                            TcpClient = ((TcpListener) ar.AsyncState).EndAcceptTcpClient(ar);
                            while (true)
                            {
                                // Poll returns true if data is available or the connection is closed
                                var pollResult = TcpClient.Client.Poll(-1, SelectMode.SelectRead);
                                if (_cancellationTokenSource.IsCancellationRequested)
                                    return;

                                if (MessageBuffer.Any())
                                {
                                    // Buffer still contains messages, process them
                                    var receivedData = ReadTcpDataAsString(Settings.StxCharacters, Settings.EtxCharacters);
                                    TryExecuteOnDataReceived(receivedData);
                                }
                                else if (pollResult && TcpClient.Client.Available == 0)
                                {
                                    // Connection has been closed by the remote party
                                    DisposeCurrentTcpClient();
                                    break;
                                }
                                else if (pollResult && (MessageBuffer.Any() || TcpClient.Client.Available > 0))
                                {
                                    // Data is available
                                    string receivedData = "";
                                    if (Settings.LeadingMessageLengthBytes > 0)
                                    {
                                        // TcpReadConnection has been configured to expect a length header before the actual data
                                        var readTcpDataResultString = ReadTcpDataWithLengthHeaderAsString(Settings.StxCharacters, Settings.EtxCharacters);
                                        if (readTcpDataResultString.Wait(Settings.SendReadTimeout))
                                            receivedData = readTcpDataResultString.Result;
                                        else
                                            throw new NetworkingException($"{nameof(ReadTcpDataWithLengthHeaderAsString)} timed out trying to attempt to read data", NetworkingException.NetworkingExceptionTypeEnum.ReadTimeout);
                                    }
                                    else
                                        receivedData = ReadTcpDataAsString(Settings.StxCharacters, Settings.EtxCharacters);

                                    TryExecuteOnDataReceived(receivedData);
                                }
                                else
                                {
                                    // pollResult is false, indicating the connection is not readable. Treat it as dead and reestablish the connection.
                                    var errorState = TcpClient.Client.Poll(1, SelectMode.SelectError);
                                    var writeState = TcpClient.Client.Poll(1, SelectMode.SelectWrite);
                                    Settings.Logger?.Verbose($"Connection is not readable so treat is as dead. Poll states: SelectError={errorState}, SelectRead={pollResult}, SelectWrite={writeState}");
                                    DisposeCurrentTcpClient();
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // This exception case usually should not happen, even during tcp errors and frequently indicates a premature disposal of the tcp socket
                            // The premature disposal can also be triggered by the OS if it force closes the connection due to an unhandled error
                            Settings.Logger?.Warn($"Failed to process BeginAcceptTcpClient async result. Connection has been closed/disposed abnormally by the app, OS, remote party, virus scanner, IDS ed.", ex);
                            DisposeCurrentTcpClient();
                            if (Settings.ThrowInsteadOfReconnect)
                                throw;
                        }
                    }, _tcpListener);
                }
            }
            catch (OperationCanceledException)
            {
                Settings.Logger?.Verbose($"{nameof(TcpReadConnection)} Tcp Listener task has been successfully cancelled");
                StopTcpListener();
                return;
            }
            catch (Exception ex)
            {
                if (ex.InnerException != default && ex.InnerException.GetType() == typeof(NetworkingException))
                {
                    Settings.Logger?.Error("Networking Exception has been received", ex.InnerException);
                    if (Settings.ThrowInsteadOfReconnect)
                        throw;
                }
                else
                {
                    Settings.Logger?.Error("TcpReadConnection.ConnectionListenerImpl() failed", new NetworkingException($"Failed to listen on local port {Settings.Port}. Make sure the port is not blocked or in use by another application", NetworkingException.NetworkingExceptionTypeEnum.ListeningError, ex));
                    StopTcpListener();
                    if (Settings.ThrowInsteadOfReconnect)
                        throw;
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
            }
        }

        void TryExecuteOnDataReceived(string receivedData)
        {
            Settings.Logger?.Verbose($"Tcp Listener on port '{Settings.Port}' received the following data: {receivedData}");
            OnDataReceived?.Invoke(receivedData);
            if (OnDataReceived == default)
                Settings.Logger?.Warn($"Property {nameof(OnDataReceived)} not set. Ignoring data that has been received thus far");
        }
    }

    /// <summary>
    /// Action that is executed when new data has been received
    /// </summary>
    public Action<string> OnDataReceived { get; set; }

    /// <summary>
    /// Open the listening socket and start listening for the remote party
    /// </summary>
    private void StartTcpListener()
    {
        Settings.Logger?.Verbose($"Attempting to start {nameof(TcpReadConnection)} Tcp Listener task");
        _tcpListener?.Stop();
        _tcpListener = new TcpListener(IPAddress.Any, Settings.Port);
        _tcpListener.Start();
        Settings.Logger?.Debug($"{nameof(TcpReadConnection)} Tcp Listener task on port {Settings.Port} has been started successfully");
        _startListeningCompletedEvent.Set();
    }

    /// <summary>
    /// Stops the listening socket and stop listening for the remote party
    /// </summary>
    private void StopTcpListener()
    {
        try
        {
            Settings.Logger?.Verbose($"Attempting to stop {nameof(TcpListener)}");
            DisposeCurrentTcpClient();
            _tcpListener?.Stop();
            _tcpListener = default;
            Settings.Logger?.Verbose($"{nameof(TcpListener)} has stopped listening for new connections");
        }
        catch (Exception ex)
        {
            Settings.Logger?.Error($"Failed to stop {nameof(TcpListener)}", ex);
        }
    }

    public new void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _tcpListener?.Stop();

        try
        {
            _listenerTask.Wait(Settings.SendReadTimeout);
        }
        catch (TaskCanceledException)
        {
            // Task has been cancelled successfully
        }

        base.Dispose();
    }
}