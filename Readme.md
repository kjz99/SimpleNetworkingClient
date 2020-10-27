# Introduction
The JSS SimpleNetworkingClient is designed for simple point to point tcp connections. For example it can be used to control a payment terminal from a cash register application, running on a POS device.
It excels in it's ease of use and straight forward functions.

# For what is the SimpleNetworkingClient not designed?
This client is not designed for multithreaded scenario's, where multiple clients connect to a single SimpleNetworkingClient.
For that scenario, more in depth design and programming is required anyway. Defeating the purpose of the SimpleNetworkingClient.

# Todo
The JSS SimpleNetworkingClient is currently not fully implemented and only supports sending data.
It partially supports reading data as implemented in the unit tests.

# License
This application public domain and is available as described by the Creative Commons CC0 1.0 Universal public license.<br/>
See License.md for the exact terms and conditions
