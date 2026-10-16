using System.ComponentModel.DataAnnotations;

namespace AppTemplate.Infrastructure.Web;

// Where the web app lives, for the links in mail (ADR 0034); bound in the Api's Program.cs with ValidateOnStart.
public sealed class WebOptions
{
    public const string SectionName = "Web";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;
}
