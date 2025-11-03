using FluentAssertions;
using JSS.SimpleNetworkingClient.Utils;
using Xunit;

namespace JSS.SimpleNetworkingClient.UnitTests.Unit
{
    public class UtilTests
    {
        [Fact]
        public void GetMessageLengthShouldReturnLengthWithEndianess()
        {
            byte[] lengthStruct = [ 253, 254, 0, 0 ];
            TcpLengthUtils.GetMessageLength(lengthStruct, 4, false).Should().Be(65277);

            lengthStruct = [ 0, 0, 254, 253, 1, 2 ];
            TcpLengthUtils.GetMessageLength(lengthStruct, 4, true).Should().Be(65277);

            lengthStruct = [ 253 ];
            TcpLengthUtils.GetMessageLength(lengthStruct, 1, false).Should().Be(253);

            lengthStruct = [ 253 ];
            TcpLengthUtils.GetMessageLength(lengthStruct, 1, true).Should().Be(253);
            
            lengthStruct = [ 253, 0 ];
            TcpLengthUtils.GetMessageLength(lengthStruct, 2, false).Should().Be(253);

            lengthStruct = [ 0, 253 ];
            TcpLengthUtils.GetMessageLength(lengthStruct, 2, true).Should().Be(253);
        }
    }
}