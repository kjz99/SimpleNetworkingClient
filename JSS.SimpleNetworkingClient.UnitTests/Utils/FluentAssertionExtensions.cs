using FluentAssertions;
using FluentAssertions.Primitives;

namespace JSS.SimpleNetworkingClient.UnitTests.Utils;

public static class FluentAssertionExtensions
{
    public static AndConstraint<TAssertions> BeCaptureExWithXUnit<TAssertions>(this StringAssertions assertion, string expected, string because = "", params object[] becauseArgs)
    {


        return null;
    }
}