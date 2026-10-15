using System;
using Microsoft.Extensions.DependencyInjection;

namespace Feature.ChipButton;

/// <summary>Registration of the module in a service collection.</summary>
public static class ChipButtonServices
{
    /// <summary>
    /// Registers <paramref name="options"/> as a singleton and <see cref="ChipButtonForm"/> as
    /// transient (one window per resolve). Throws <see cref="ArgumentNullException"/> for a null
    /// argument. Returns <paramref name="services"/> itself, for chaining.
    /// </summary>
    public static IServiceCollection AddChipButton(this IServiceCollection services, ChipButtonOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton(options);
        services.AddTransient<ChipButtonForm>();
        return services;
    }
}
