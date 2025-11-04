using System;

namespace JSS.SimpleNetworkingClient.UnitTests.Integration;

public static class TcpClientSettingFactory
{
    public static TcpClientSettings DefaultSettings => new ()
    {
        Host = "127.0.0.1",
        Port = 514,
        SendReadTimeout = TimeSpan.FromSeconds(30),
        IpStackBufferSize = 1024,
        StxCharacters = [ 0x02 ],
        EtxCharacters = [ 0x03 ]
    };
}