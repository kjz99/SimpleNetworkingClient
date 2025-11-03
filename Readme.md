# Introduction
The JSS SimpleNetworkingClient is designed for simple point to point tcp connections. For example, it can be used to control a payment terminal from a cash register application, running on a POS device.<br/>
It excels in its ease of use and straight forward functionality and alleviates you from the many problems that raw tcp communication presents.

# For what scenarios is the SimpleNetworkingClient not designed?
This client is not designed for multithreaded/high performance scenario's, where multiple clients connect to a single SimpleNetworkingClient.<br/>
For that scenario, more in depth design and programming is required anyway, defeating the purpose of the SimpleNetworkingClient.

# Usage
The following use case scenario's are most commonly used. Also see the integration tests for some real world working examples on how to use the library.

## Receiving and sending data with stx and etx characters
The following example shows how to receive and send data using the SimpleNetworkingClient that starts with a stx character and ends with an etx character.
```csharp
using System.Text;
using JSS.SimpleNetworkingClient;

byte stxCharacter = 0x02;
byte etxCharacter = 0x03;

var settings = new TcpClientSettings()
{
    Host = "127.0.0.1",
    Port = 514,
    SendReadTimeout = TimeSpan.FromSeconds(30),
    IpStackBufferSize = 1024,
    StxCharacters = [ stxCharacter ],
    EtxCharacters = [ etxCharacter ]
};

using var reader = new TcpReadConnection(settings);
reader.OnDataReceived = (receivedDataFromSender) =>
{
    Console.WriteLine($"Received data from sender: {receivedDataFromSender}");
    _ = reader.SendData("ACK", Encoding.UTF8);
};

reader.StartListening();

using var sendConnection = new TcpSendConnection(settings);
await sendConnection.SendData("This is a message", Encoding.UTF8);
var receivedDataFromReader = sendConnection.ReceiveDataAsString();
Console.WriteLine($"Received data from reader: {receivedDataFromReader}"); 
```

## Receiving and sending data with a message length header
The following example shows how to receive and send data using the SimpleNetworkingClient that starts with 1 to 4 bytes that indicate the total length of the message.</br>
When the total number of bytes is received, it will return with the data as a string.
```csharp
using System.Text;
using JSS.SimpleNetworkingClient;

var settings = new TcpClientSettings()
{
    Host = "127.0.0.1",
    Port = 514,
    SendReadTimeout = TimeSpan.FromSeconds(30),
    IpStackBufferSize = 1024,
    StxCharacters = [ ],
    EtxCharacters = [ ],
    LeadingMessageLengthBytes = 4,
    LittleEndian = false
};

using var reader = new TcpReadConnection(settings);
reader.OnDataReceived = (receivedDataFromSender) =>
{
    Console.WriteLine($"Received data from sender: {receivedDataFromSender}");
    _ = reader.SendData("ACK", Encoding.UTF8);
};

reader.StartListening();

using var sendConnection = new TcpSendConnection(settings);
await sendConnection.SendData("This is a message", Encoding.UTF8);
var receivedDataFromReader = sendConnection.ReceiveDataAsString();
Console.WriteLine($"Received data from reader: {receivedDataFromReader}"); 
```

## Using the log4net logger
To use the log4net logger you can instantiate the logger setting the logging in the TcpClientSettings instance that you can pass to the constructor.<br/>
As the Log4netLogger wrapper implements the ISimpleNetworkingClientLogger interface, it can be passed the a TcpReadConnection or TcpSendConnection as the logging instance.

### Log4net example
```csharp
// Log4Net logger
var hierarchy = (Hierarchy)LogManager.GetRepository();
hierarchy.Root.RemoveAllAppenders();
var consoleAppender = new ConsoleAppender
{
    Layout = new PatternLayout("%date{yyyy-MM-dd HH:mm:ss} [%thread] %-5level %logger - %message%newline"),
};
consoleAppender.ActivateOptions();
var debugAppender = new DebugAppender
{
    Layout = new PatternLayout("%date{yyyy-MM-dd HH:mm:ss} [%thread] %-5level %logger - %message%newline"),
};
debugAppender.ActivateOptions();
hierarchy.Root.AddAppender(consoleAppender);
hierarchy.Root.AddAppender(debugAppender);
hierarchy.Root.Level = Level.Debug;
hierarchy.Configured = true;
var log4netLogger = LogManager.GetLogger(typeof(Program));

// Settings initialized with Log4Net
var settingsLog4net = new TcpClientSettings()
{
    Host = "127.0.0.1",
    Port = 514,
    SendReadTimeout = TimeSpan.FromSeconds(30),
    IpStackBufferSize = 1024,
    StxCharacters = [ stxCharacter ],
    EtxCharacters = [ etxCharacter ],
    Logger = new Log4netLogger("tcpClientLogger")
};

using var reader = new TcpReadConnection(settingsLog4net);
reader.OnDataReceived = (receivedDataFromSender) =>
{
    Console.WriteLine($"Received data from sender: {receivedDataFromSender}");
    _ = reader.SendData("ACK", Encoding.UTF8);
};

reader.StartListening();

using var sendConnection = new TcpSendConnection(settingsLog4net);
await sendConnection.SendData("This is a message", Encoding.UTF8);
var receivedDataFromReader = sendConnection.ReceiveDataAsString();
Console.WriteLine($"Received data from reader: {receivedDataFromReader}");

Console.WriteLine("Press any key to exit...");
Console.ReadKey();
```
## Using the Serilog logger
To use the Serilog logger you can instantiate the logger setting the logging in the TcpClientSettings instance that you can pass to the constructor.<br/>
As the SerilogLogger wrapper implements the ISimpleNetworkingClientLogger interface, it can be passed the a TcpReadConnection or TcpSendConnection as the logging instance.

### Serilog example
```csharp
// Serilog logger
var serilogLogger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Debug()
    .CreateLogger();

// Settings initialized with Serilog
var settingsSerilog = new TcpClientSettings()
{
    Host = "127.0.0.1",
    Port = 514,
    SendReadTimeout = TimeSpan.FromSeconds(30),
    IpStackBufferSize = 1024,
    StxCharacters = [ stxCharacter ],
    EtxCharacters = [ etxCharacter ],
    Logger = new SerilogLogger(serilogLogger)
};

using var reader = new TcpReadConnection(settingsSerilog);
reader.OnDataReceived = (receivedDataFromSender) =>
{
    Console.WriteLine($"Received data from sender: {receivedDataFromSender}");
    _ = reader.SendData("ACK", Encoding.UTF8);
};

reader.StartListening();

using var sendConnection = new TcpSendConnection(settingsSerilog);
await sendConnection.SendData("This is a message", Encoding.UTF8);
var receivedDataFromReader = sendConnection.ReceiveDataAsString();
Console.WriteLine($"Received data from reader: {receivedDataFromReader}");

Console.WriteLine("Press any key to exit...");
Console.ReadKey();
```

# Unit/Integration Tests
The JSS.SimpleNetworkingClient.UnitTests contains the unit and integration tests.<br/>
In the Unit subfolder all unit tests are located and in the integration subfolder, all the integration and load tests are located.<br/>
All the unit/integration tests are self contained and only need read/write access to tcp sockets on port 514.

# Pitfalls
## The .Net TcpClient is not reliable on Windows
The TcpClient connection is not fully reliable on Windows. The Winsock tcp stack that is used by the .Net TcpClient, has known design deficiencies.<br/>
It does not always detect that a remote party has closed the connection, leading to zombie connections.<br/>
This can cause the TcpClient to think that the connection is still open. Reading from the stream will not return an exception.<br/>
Also, sending data will just be discarded by Winsock without any indication that a problem has occurred.<br/>
Therefore, I highly recommend using a verification mechanism(eg, ping message) to detect if the connection has failed.

## Tcp/ip is a streaming protocol
Tcp is a streaming protocol, meaning it will stream data from the source to the destination without any indication to the end of the stream.</br>
If multiple messages are sent slowly enough, the remote party will see these as separate messages.</br>
But when multiple messages are sent fast enough, they will appear as a single concatenated message on the remote party's side.</br>
This means that messages need to be split up using start/end of transmission character, or a message length header.</br>

## Numbers and UTF characters can be seen as the STX/ETX characters
If you also send numbers as an actual number instead of an ascii string(0x30 up to 0x3A), the number 2 will be the same as an ascii STX character(0x02).</br>
This can also happen when sending UTF characters, which consist of multiple bytes. Currently, the SimpleNetworkingClient does not support interpreting UTF characters as a 2-byte pair.</br>
In these cases, using a message length header will be a better option than using a start/end of transmission character.

# Licenses
## JSS.SimpleNetworkingClient
This application is public domain and is available as described by the Creative Commons CC0 1.0 Universal public license.<br/>
See License.md for the exact terms and conditions.