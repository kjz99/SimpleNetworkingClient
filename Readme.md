# Introduction
The JSS SimpleNetworkingClient is designed for simple point to point tcp connections. For example it can be used to control a payment terminal from a cash register application, running on a POS device.
It excels in it's ease of use and straight forward functions and alleviates you from the many problems that raw tcp communication presents.

# For what is the SimpleNetworkingClient not designed?
This client is not designed for multithreaded/high performance scenario's, where multiple clients connect to a single SimpleNetworkingClient.
For that scenario, more in depth design and programming is required anyway. Defeating the purpose of the SimpleNetworkingClient.

# Usage
The following use case scenario's are most commonly used. Also see the unit tests for some real world working examples on how to use the library.
## Receiving data
TODO...
## Sending data
TODO...


## Using the log4net logger
To use the log4net logger you can instantiate the logger using one of the constructors.
As the logger implements the ISimpleNetworkingClientLogger interface it can be passed the a TcpReadConnection or TcpSendConnection as the logging instance.
### sample log4net instance using the default repository and config
var defaultLoggingRepo = LogManager.CreateRepository("defaultrepository");
XmlConfigurator.Configure(defaultLoggingRepo, File.ReadAllText("C:\path\to\log4netconfig.xml"));
var loggerToUse = new Log4netLogger("defaultrepository", "networkingclient");
using var reader = new TcpReadConnection(loggerToUse, 8081, TimeSpan.FromSeconds(10), 1024, new List<byte>() { 0x02 }, new List<byte>() { 0x03 });

## Using the Sewrilog logger
To use the serilog logger pass the ILogger instance to the SerilogLogger constructor.
As the logger implements the ISimpleNetworkingClientLogger interface it can be passed the a TcpReadConnection or TcpSendConnection as the logging instance.
# Sample Serilog logger instance using appsettings.json
TODO...

# Unit/Integration Tests
The JSS.SimpleNetworkingClient.UnitTests contains the unit tests.
In the Unit subfolder all unit tests are located and in the Integration subfolder all the integration and load tests are located.
All the unit/integration tests are self contained and only need read/write access to tcp sockets on port 514.

# License
This application public domain and is available as described by the Creative Commons CC0 1.0 Universal public license.<br/>
See License.md for the exact terms and conditions
