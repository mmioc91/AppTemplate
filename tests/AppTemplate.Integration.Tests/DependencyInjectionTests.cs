using AppTemplate.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AppTemplate.Integration.Tests;

// No database needed: the missing connection string is caught while registering services.
public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_WithoutConnectionString_ThrowsNamingTheMissingSetting()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = new();

        // Act
        Action act = () => services.AddInfrastructure(configuration);

        // Assert: the first error a new developer sees must say which setting to add.
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(act);
        exception.Message.ShouldContain($"'{DependencyInjection.ConnectionStringName}'");
    }
}
