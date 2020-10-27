using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Xunit;

namespace JSS.SimpleNetworkingClient.UnitTests.Unit
{
    public class StructTests
    {
        [Fact]
        public void TcpStructByteInput()
        {
            var lengthStruct = new TcpLengthStruct(new byte[] {253, 254, 0, 0});
            lengthStruct.Value.Should().Be(65277);
        }

        [Fact]
        public void TcpStructIntInput()
        {
            var lengthStruct = new TcpLengthStruct(65277);
            lengthStruct.Byte0.Should().Be(253);
            lengthStruct.Byte1.Should().Be(254);
        }
    }
}
