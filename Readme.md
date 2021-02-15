# Introduction
The JSS SimpleNetworkingClient is designed for simple point to point tcp connections. For example it can be used to control a payment terminal from a cash register application, running on a POS device.
It excels in it's ease of use and straight forward functions.

# For what is the SimpleNetworkingClient not designed?
This client is not designed for multithreaded scenario's, where multiple clients connect to a single SimpleNetworkingClient.
For that scenario, more in depth design and programming is required anyway. Defeating the purpose of the SimpleNetworkingClient.

# Todo
The JSS SimpleNetworkingClient is currently not fully implemented and partially supports sending and reading data.

# Usage
TODO
## Using the log4net logger
To use the log4net logger you can instanciate the logger using  one of the constructors.
As the logger implements the ISimpleNetworkingClientLogger interface it can be passed the a TcpReadConnection or TcpSendConnection as the logging instance.
### log4net instance including full repository
var defaultLoggingRepo = LogManager.CreateRepository("defaultrepository");
XmlConfigurator.Configure(defaultLoggingRepo, File.ReadAllText("C:\path\to\log4netconfig.xml"));
var loggerToUse = new Log4netLogger("defaultrepository", "networkingclient");
using var reader = new TcpReadConnection(loggerToUse, 8081, TimeSpan.FromSeconds(10), 1024, new List<byte>() { 0x02 }, new List<byte>() { 0x03 });

# Unit/Integration Tests
TODO

# License
This application public domain and is available as described by the Creative Commons CC0 1.0 Universal public license.<br/>
See License.md for the exact terms and conditions
