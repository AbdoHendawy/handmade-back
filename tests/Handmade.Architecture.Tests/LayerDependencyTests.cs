using NetArchTest.Rules;

namespace Handmade.Architecture.Tests;

public sealed class LayerDependencyTests
{
    private const string DomainNamespace = "Handmade.Domain";
    private const string ApplicationNamespace = "Handmade.Application";
    private const string InfrastructureNamespace = "Handmade.Infrastructure";
    private const string ApiNamespace = "Handmade.Api";

    [Fact]
    public void Domain_Should_Not_Depend_On_Application_Infrastructure_Or_Api()
    {
        TestResult result = Types.InAssembly(typeof(Handmade.Domain.Common.Entity).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ApplicationNamespace, InfrastructureNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Or_Api()
    {
        TestResult result = Types.InAssembly(typeof(Handmade.Application.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(InfrastructureNamespace, ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Application_Should_Not_Reference_Hangfire_Or_SignalR()
    {
        TestResult result = Types.InAssembly(typeof(Handmade.Application.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Hangfire", "Microsoft.AspNetCore.SignalR")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_Api()
    {
        TestResult result = Types.InAssembly(typeof(Handmade.Infrastructure.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOn(ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Domain_Should_Not_Reference_EfCore_Or_AspNetCore()
    {
        TestResult result = Types.InAssembly(typeof(Handmade.Domain.Common.Entity).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Npgsql",
                "Hangfire",
                "Microsoft.AspNetCore.SignalR")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    private static string FormatFailures(TestResult result)
    {
        if (result.FailingTypeNames is null || result.FailingTypeNames.Count == 0)
        {
            return "Architecture rule failed.";
        }

        return "Architecture rule failed for: " + string.Join(", ", result.FailingTypeNames);
    }
}
