# Introduction
The JSS SimpleNetworkingClient is designed for simple point to point tcp connections. For example it can be used to control a payment terminal from a cash register application, running on a POS device.<br/>
It excels in its ease of use and straight forward functionality and alleviates you from the many problems that raw tcp communication presents.

# For what is the SimpleNetworkingClient not designed?
This client is not designed for multithreaded/high performance scenario's, where multiple clients connect to a single SimpleNetworkingClient.<br/>
For that scenario, more in depth design and programming is required anyway, defeating the purpose of the SimpleNetworkingClient.

# Usage
The following use case scenario's are most commonly used. Also see the integration tests for some real world working examples on how to use the library.
## Receiving data

## Sending data
TODO...


## Using the log4net logger
To use the log4net logger you can instantiate the logger using one of the constructors.<br/>
As the logger implements the ISimpleNetworkingClientLogger interface it can be passed the a TcpReadConnection or TcpSendConnection as the logging instance.
### sample log4net instance using the default repository and config
```csharp
var defaultLoggingRepo = LogManager.CreateRepository("defaultrepository");
XmlConfigurator.Configure(defaultLoggingRepo, File.ReadAllText("C:\path\to\log4netconfig.xml"));
var loggerToUse = new Log4netLogger("defaultrepository", "networkingclient");
using var reader = new TcpReadConnection(loggerToUse, 8081, TimeSpan.FromSeconds(10), 1024, new List<byte>() { 0x02 }, new List<byte>() { 0x03 });
reader.OnDataReceived = (returnedData) =>
    {
        Console.WriteLine($"Received data: {returnedData}");
        reader.SendData("ACK", Encoding.UTF8).Wait(_defaultTimeout);
    };
```
## Using the Serilog logger
To use the serilog logger pass the ILogger instance to the SerilogLogger constructor.<br/>
As the logger implements the ISimpleNetworkingClientLogger interface it can be passed the a TcpReadConnection or TcpSendConnection as the logging instance.

# Sample Serilog logger instance using appsettings.json
TODO...

# Unit/Integration Tests
The JSS.SimpleNetworkingClient.UnitTests contains the unit tests.<br/>
In the Unit subfolder all unit tests are located and in the integration subfolder all the integration and load tests are located.<br/>
All the unit/integration tests are self contained and only need read/write access to tcp sockets on port 514.

# Pitfalls
## The TcpClient is not reliable on Windows
The TcpClient connection is not fully reliable on Windows. The Winsock tcp stack that is used by the .Net TcpClient, has known design deficiencies.<br/>
It does not always detect that a remote party has closed the connection, leading to zombie connections.<br/>
This can cause the TcpClient to think that the connection is still open. Reading from the stream will not return an exception.<br/>
Also, sending data will just be discarded by Winsock without any indication that a problem has occurred.<br/>
Therefore, I highly recommend using a verification mechanism(eg, ping message) to detect if the connection has failed.

## Tcp/ip is a streaming protocol
Tcp is a streaming protocol, meaning it will stream data from the source to the destination without any indication of the end of the stream.
If multiple messages are sent slowly enough, the remote party will see these as separate messages.
But when multiple messages are sent fast enough, they will appear as a single concatenated message on the remote party's side.
This means that messages need to be split up using start/end of transmission character, or a message length header.

# Licenses
## JSS.SimpleNetworkingClient
This application is public domain and is available as described by the Creative Commons CC0 1.0 Universal public license.<br/>
See License.md for the exact terms and conditions.