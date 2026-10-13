using Microsoft.AspNetCore.HttpOverrides;

namespace AppTemplate.Api;

// Behind the reverse proxy (ADR 0029): the client's address and scheme come from X-Forwarded-For/-Proto, but only from
// the proxies named in Proxy:KnownNetworks (CIDR, comma-separated) — anyone else could put any address there and pass
// every per-IP rate limit. Without the setting only loopback is trusted, as ASP.NET Core does by default.
internal static class ProxySetup
{
    public const string KnownNetworksKey = "Proxy:KnownNetworks";

    public static IServiceCollection AddApiProxy(this IServiceCollection services, IConfiguration configuration) =>
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            foreach (string network in (configuration[KnownNetworksKey] ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });
}
