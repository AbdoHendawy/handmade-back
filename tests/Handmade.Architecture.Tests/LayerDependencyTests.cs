using Handmade.Application.Abstractions.Persistence;
using Handmade.Domain.Orders;
using Handmade.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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
    public void Catalog_Should_Not_Depend_On_Cart()
    {
        TestResult application = Types.InAssembly(typeof(Handmade.Application.DependencyInjection).Assembly)
            .That()
            .ResideInNamespaceStartingWith("Handmade.Application.Catalog")
            .ShouldNot()
            .HaveDependencyOn("Handmade.Application.Cart")
            .GetResult();
        TestResult domain = Types.InAssembly(typeof(Handmade.Domain.Common.Entity).Assembly)
            .That()
            .ResideInNamespaceStartingWith("Handmade.Domain.Catalog")
            .ShouldNot()
            .HaveDependencyOn("Handmade.Domain.Cart")
            .GetResult();

        Assert.True(application.IsSuccessful, FormatFailures(application));
        Assert.True(domain.IsSuccessful, FormatFailures(domain));
    }

    [Fact]
    public void OrdersDomain_Should_Not_Depend_On_Cart()
    {
        TestResult result = Types.InAssembly(typeof(Handmade.Domain.Common.Entity).Assembly)
            .That()
            .ResideInNamespaceStartingWith("Handmade.Domain.Orders")
            .ShouldNot()
            .HaveDependencyOn("Handmade.Domain.Cart")
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

    [Fact]
    public void Order_OwnsLifecycleMethods_OrderGroupDoesNot()
    {
        string[] names = ["Confirm", "Prepare", "Ship", "Deliver", "Cancel"];
        foreach (string name in names)
        {
            Assert.NotNull(typeof(Order).GetMethod(name));
            Assert.Null(typeof(OrderGroup).GetMethod(name));
        }
    }

    [Fact]
    public void OrdersDomain_Should_Not_Depend_On_Notifications()
    {
        TestResult result = Types.InAssembly(typeof(Handmade.Domain.Common.Entity).Assembly)
            .That()
            .ResideInNamespaceStartingWith("Handmade.Domain.Orders")
            .ShouldNot()
            .HaveDependencyOn("Handmade.Domain.Notifications")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Application_Should_Not_Reference_MediatR_Or_MessageBus()
    {
        TestResult result = Types.InAssembly(typeof(Handmade.Application.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("MediatR", "Rebus", "MassTransit", "NServiceBus")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void PaymentMethod_BelongsToOrderGroup_NotOrder()
    {
        Assert.NotNull(typeof(OrderGroup).GetProperty(nameof(OrderGroup.PaymentMethod)));
        Assert.Null(typeof(Order).GetProperty("PaymentMethod"));
    }

    [Fact]
    public void Api_Should_Not_Expose_Payment_Controllers()
    {
        IEnumerable<Type> controllers = Types
            .InAssembly(typeof(Handmade.Api.Controllers.CheckoutController).Assembly)
            .That()
            .HaveNameEndingWith("Controller")
            .And()
            .HaveNameMatching(".*Payment.*")
            .GetTypes();

        Assert.Empty(controllers);
    }

    [Fact]
    public void Persistence_Should_Not_Have_A_Payment_DbSet()
    {
        Assert.DoesNotContain(
            typeof(IApplicationDbContext).GetProperties(),
            property => property.Name.Contains("Payment", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OrderGroup_PaymentMethod_IsMappedAsRequiredString()
    {
        using HandmadeDbContext db = CreateModelContext();
        IProperty property = db.Model
            .FindEntityType(typeof(OrderGroup))!
            .FindProperty(nameof(OrderGroup.PaymentMethod))!;

        Assert.False(property.IsNullable);
        Assert.Equal(typeof(PaymentMethod), property.ClrType);
        Assert.Equal(32, property.GetMaxLength());
        Assert.Equal("character varying(32)", property.GetColumnType());
        Assert.Null(db.Model.FindEntityType(typeof(Order))!.FindProperty("PaymentMethod"));
    }

    [Fact]
    public void Order_Status_IsMappedAsRequiredString_WithinExistingColumn()
    {
        using HandmadeDbContext db = CreateModelContext();
        IProperty property = db.Model
            .FindEntityType(typeof(Order))!
            .FindProperty(nameof(Order.Status))!;

        Assert.False(property.IsNullable);
        Assert.Equal(typeof(OrderStatus), property.ClrType);
        Assert.Equal(32, property.GetMaxLength());
        Assert.Equal("character varying(32)", property.GetColumnType());
        Assert.All(
            Enum.GetNames<OrderStatus>(),
            name => Assert.True(name.Length <= 32, name));
    }

    [Fact]
    public void OrderGroup_Status_RemainsPlacedOnly_MappedAsRequiredString()
    {
        using HandmadeDbContext db = CreateModelContext();
        IProperty property = db.Model
            .FindEntityType(typeof(OrderGroup))!
            .FindProperty(nameof(OrderGroup.Status))!;

        Assert.False(property.IsNullable);
        Assert.Equal(typeof(OrderGroupStatus), property.ClrType);
        Assert.Equal(32, property.GetMaxLength());
        Assert.Equal("character varying(32)", property.GetColumnType());
        Assert.Equal(new[] { OrderGroupStatus.Placed }, Enum.GetValues<OrderGroupStatus>());
    }

    [Fact]
    public void Order_And_OrderGroup_HaveXminConcurrencyToken()
    {
        using HandmadeDbContext db = CreateModelContext();

        IProperty orderXmin = db.Model.FindEntityType(typeof(Order))!.FindProperty("xmin")!;
        IProperty groupXmin = db.Model.FindEntityType(typeof(OrderGroup))!.FindProperty("xmin")!;

        Assert.True(orderXmin.IsConcurrencyToken);
        Assert.True(groupXmin.IsConcurrencyToken);
        Assert.Equal("xid", orderXmin.GetColumnType());
        Assert.Equal("xid", groupXmin.GetColumnType());
    }

    [Fact]
    public void Application_Should_Not_Reference_Minio()
    {
        TestResult result = Types.InAssembly(typeof(Handmade.Application.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Minio")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    [Fact]
    public void Domain_Should_Not_Reference_Minio_Or_FileStorage()
    {
        TestResult minio = Types.InAssembly(typeof(Handmade.Domain.Common.Entity).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Minio")
            .GetResult();
        TestResult storage = Types.InAssembly(typeof(Handmade.Domain.Common.Entity).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Handmade.Application.Abstractions.Storage")
            .GetResult();

        Assert.True(minio.IsSuccessful, FormatFailures(minio));
        Assert.True(storage.IsSuccessful, FormatFailures(storage));
    }

    [Fact]
    public void Api_Should_Not_Reference_Minio()
    {
        TestResult result = Types.InAssembly(typeof(Handmade.Api.Controllers.CheckoutController).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Minio")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailures(result));
    }

    private static HandmadeDbContext CreateModelContext()
    {
        DbContextOptions<HandmadeDbContext> options = new DbContextOptionsBuilder<HandmadeDbContext>()
            .UseNpgsql("Host=localhost;Database=handmade;Username=handmade;Password=handmade")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new HandmadeDbContext(options);
    }
}
