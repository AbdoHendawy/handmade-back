using System.Reflection;
using FluentValidation;
using Handmade.Application.Abstractions.Identity;
using Handmade.Application.Catalog.Services;
using Handmade.Application.Identity.Services;
using Handmade.Application.Notifications.Services;
using Handmade.Application.Seller.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Handmade.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        Assembly assembly = typeof(DependencyInjection).Assembly;
        services.AddValidatorsFromAssembly(assembly);
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IIdentityRoleService, IdentityRoleService>();
        services.AddScoped<ISellerNotificationService, SellerNotificationService>();
        services.AddScoped<ISellerApplicationService, SellerApplicationService>();
        services.AddScoped<ISellerProfileService, SellerProfileService>();
        services.AddScoped<IAdminSellerService, AdminSellerService>();
        services.AddScoped<INotificationPublisher, NotificationPublisher>();
        services.AddScoped<INotificationInboxService, NotificationInboxService>();
        services.AddScoped<INotificationDeliveryService, NotificationDeliveryService>();
        services.AddScoped<IAdminNotificationService, AdminNotificationService>();
        services.AddScoped<IAdminCategoryService, AdminCategoryService>();
        services.AddScoped<ISellerProductService, SellerProductService>();
        services.AddScoped<IAdminProductService, AdminProductService>();
        services.AddScoped<IPublicCatalogService, PublicCatalogService>();

        return services;
    }
}
