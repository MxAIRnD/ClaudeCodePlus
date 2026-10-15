using System;
using Microsoft.Extensions.DependencyInjection;

namespace Feature.ContextBar;

/// <summary>Registration of the module in a service collection.</summary>
public static class ContextBarServices
{
    /// <summary>
    /// Registers <paramref name="options"/> as a singleton, one <see cref="FocusedSessionFinder"/>
    /// over its session store as a singleton, and <see cref="ContextBarForm"/> as transient (one
    /// window per resolve). Throws <see cref="ArgumentNullException"/> for a null argument.
    /// Returns <paramref name="services"/> itself, for chaining.
    /// </summary>
    public static IServiceCollection AddContextBar(this IServiceCollection services, ContextBarOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton(options);
        services.AddSingleton(new FocusedSessionFinder(options.SessionsRoot));
        services.AddTransient<ContextBarForm>();
        return services;
    }
}
