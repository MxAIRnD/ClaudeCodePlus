using System;
using Microsoft.Extensions.DependencyInjection;

namespace Feature.DeepLink;

/// <summary>Registration of the module in a service collection.</summary>
public static class DeepLinkServices
{
    /// <summary>
    /// Registers <see cref="ISessionOpener"/> as a singleton. Throws
    /// <see cref="ArgumentNullException"/> for a null argument. Returns
    /// <paramref name="services"/> itself, for chaining.
    /// </summary>
    public static IServiceCollection AddDeepLink(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ISessionOpener, SessionOpener>();
        return services;
    }
}
