using System.Reflection;
using FluentValidation;
using Handmade.Application.Identity.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Handmade.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        Assembly assembly = typeof(DependencyInjection).Assembly;
        services.AddValidatorsFromAssembly(assembly);
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        return services;
    }
}
