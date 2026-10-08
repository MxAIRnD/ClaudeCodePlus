using System;
using Microsoft.Extensions.DependencyInjection;

namespace Feature.Picker;

/// <summary>Registration of the module in a service collection.</summary>
public static class PickerServices
{
    /// <summary>
    /// Registers <see cref="PickerForm"/> as transient (one window per resolve); the catalog
    /// and the opener it needs must be registered too. Throws <see cref="ArgumentNullException"/>
    /// for a null argument. Returns <paramref name="services"/> itself, for chaining.
    /// </summary>
    public static IServiceCollection AddPicker(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddTransient<PickerForm>();
        return services;
    }
}
