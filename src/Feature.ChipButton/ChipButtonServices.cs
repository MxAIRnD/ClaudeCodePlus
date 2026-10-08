using System;
using Microsoft.Extensions.DependencyInjection;

namespace Feature.ChipButton;

/// <summary>Registration of the module in a service collection.</summary>
public static class ChipButtonServices
{
    /// <summary>
    /// Registers <see cref="ChipButtonForm"/> as transient (one window per resolve; it needs
    /// nothing else). Throws <see cref="ArgumentNullException"/> for a null argument. Returns
    /// <paramref name="services"/> itself, for chaining.
    /// </summary>
    public static IServiceCollection AddChipButton(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<ChipButtonForm>();
        return services;
    }
}
