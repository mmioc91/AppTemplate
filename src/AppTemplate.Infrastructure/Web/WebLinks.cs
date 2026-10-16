using AppTemplate.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace AppTemplate.Infrastructure.Web;

// The web app's routes the mail links to (web/src/app/app.routes.ts), in the market's language, which the app serves
// at / (ADR 0032); a token goes in the query, where the page reads it.
internal sealed class WebLinks(IOptions<WebOptions> options) : IWebLinks
{
    public Uri SignIn() => Page("login");

    // BaseUrl + the page's path, the token escaped: how every page's method builds its link.
    internal Uri Page(string path, string? token = null)
    {
        string query = token is null ? string.Empty : $"?token={Uri.EscapeDataString(token)}";
        return new Uri($"{options.Value.BaseUrl.TrimEnd('/')}/{path}{query}");
    }
}
