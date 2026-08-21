using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Handmade.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        Assembly assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
