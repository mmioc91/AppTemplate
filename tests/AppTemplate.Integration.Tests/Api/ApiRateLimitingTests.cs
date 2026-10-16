using System.Net;
using AppTemplate.Api;
using Shouldly;

namespace AppTemplate.Integration.Tests.Api;

// Who a client is, for its rate-limit window (ADR 0031): no server needed.
public class ApiRateLimitingTests
{
    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:1:2::10", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:3::10", "2001:db8:1:3::/64")]
    public void ClientKey_Address_IsTheClientsWindow(string address, string expected)
    {
        // Act
        string key = ApiRateLimiting.ClientKey(IPAddress.Parse(address));

        // Assert: an IPv4 client as it is, also when reported IPv6-mapped; an IPv6 client by its /64.
        key.ShouldBe(expected);
    }

    [Fact]
    public void ClientKey_NoAddress_IsOneSharedWindow()
    {
        // Act
        string key = ApiRateLimiting.ClientKey(null);

        // Assert
        key.ShouldBe("unknown");
    }
}
