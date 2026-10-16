namespace AppTemplate.Application.Abstractions;

// Addresses of the web app's pages, for the links a mail carries (ADR 0034): one method per page, so a use case never
// spells a path. A page that takes a token (a confirmation, a password reset, an invitation) gets it in the query,
// where the page reads it and sends it in a request body — never in an API URL, which proxies and logs keep.
public interface IWebLinks
{
    // The sign-in page.
    Uri SignIn();
}
