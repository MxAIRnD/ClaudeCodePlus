using System;
using Microsoft.Extensions.DependencyInjection;

namespace Feature.Catalog;

/// <summary>Registration of the module in a service collection.</summary>
public static class CatalogServices
{
    /// <summary>
    /// Registers <see cref="IProjectCatalog"/> as a singleton that scans per
    /// <paramref name="options"/>; the options instance is registered as well. Throws
    /// <see cref="ArgumentNullException"/> for a null argument.
    /// </summary>
    public static IServiceCollection AddCatalog(this IServiceCollection services, CatalogOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton(options);
        services.AddSingleton<IProjectCatalog, ProjectCatalog>();
        return services;
    }
}
