using System.Text;
using JSS.SimpleNetworkingClient;
using JSS.SimpleNetworkingClient.Logging.Log4net;
using JSS.SimpleNetworkingClient.Logging.Serilog;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Serilog;

byte stxCharacter = 0x02;
byte etxCharacter = 0x03;

// Serilog logger
var serilogLogger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Debug()
    .CreateLogger();

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

// Settings initialized with Serilog
var settingsSerilog = new TcpClientSettings()
{
    Host = "127.0.0.1",
    Port = 514,
    SendReadTimeout = TimeSpan.FromSeconds(3000),
    IpStackBufferSize = 1024,
    StxCharacters = [ stxCharacter ],
    EtxCharacters = [ etxCharacter ],
    LeadingMessageLengthBytes = 4,
    LittleEndian = false,
    Logger = new SerilogLogger(serilogLogger)
};

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