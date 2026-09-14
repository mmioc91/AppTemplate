using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace AppTemplate.Architecture.Tests;

public class LayerDependencyTests
{
    private static readonly Assembly SharedKernelAssembly = Assembly.Load("AppTemplate.SharedKernel");
    private static readonly Assembly DomainAssembly = Assembly.Load("AppTemplate.Domain");
    private static readonly Assembly ApplicationAssembly = Assembly.Load("AppTemplate.Application");
    private static readonly Assembly InfrastructureAssembly = Assembly.Load("AppTemplate.Infrastructure");

    [Fact]
    public void SharedKernel_should_not_depend_on_any_other_layer()
    {
        TestResult result = Types.InAssembly(SharedKernelAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "AppTemplate.Domain",
                "AppTemplate.Application",
                "AppTemplate.Infrastructure",
                "AppTemplate.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailureMessage(result));
    }

    [Fact]
    public void SharedKernel_should_not_depend_on_infrastructure_packages()
    {
        // SharedKernel is the innermost project - BCL only, same purity rule as Domain.
        TestResult result = Types.InAssembly(SharedKernelAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "Dapper",
                "Microsoft.AspNetCore",
                "Microsoft.AspNetCore.Identity")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailureMessage(result));
    }

    [Fact]
    public void Domain_should_not_depend_on_Application_Infrastructure_or_Api()
    {
        TestResult result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "AppTemplate.Application",
                "AppTemplate.Infrastructure",
                "AppTemplate.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailureMessage(result));
    }

    [Fact]
    public void Application_should_not_depend_on_Infrastructure_or_Api()
    {
        TestResult result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "AppTemplate.Infrastructure",
                "AppTemplate.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailureMessage(result));
    }

    [Fact]
    public void Infrastructure_should_not_depend_on_Api()
    {
        TestResult result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("AppTemplate.Api")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailureMessage(result));
    }

    [Fact]
    public void Domain_should_not_depend_on_infrastructure_packages()
    {
        // Domain must stay "clean" - no ORM, database or web framework.
        TestResult result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "Dapper",
                "Microsoft.AspNetCore",
                "Microsoft.AspNetCore.Identity")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailureMessage(result));
    }

    [Fact]
    public void Application_should_not_depend_on_concrete_infrastructure_packages()
    {
        // Application only knows abstractions (ports) - not the concrete ORM/web implementation.
        TestResult result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "Dapper",
                "Microsoft.AspNetCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailureMessage(result));
    }

    private static string FailureMessage(TestResult result)
    {
        IEnumerable<string> failing = result.FailingTypeNames ?? [];
        return $"Dependency rule violated by: {string.Join(", ", failing)}";
    }
}
