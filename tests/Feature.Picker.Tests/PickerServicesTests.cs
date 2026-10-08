using System;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Feature.Picker.Tests;

/// <summary>
/// Black-box behavior of <see cref="PickerServices.AddPicker"/>: inspects the registration
/// only. No container is built and no window is created.
/// </summary>
public sealed class PickerServicesTests
{
    [Fact]
    public void AddPicker_null_services_throws_ArgumentNullException()
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => PickerServices.AddPicker(null!));

        Assert.Equal("services", ex.ParamName);
    }

    [Fact]
    public void AddPicker_returns_the_same_collection()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Same(services, services.AddPicker());
    }

    [Fact]
    public void AddPicker_registers_PickerForm_as_transient_by_its_own_type()
    {
        IServiceCollection services = new ServiceCollection().AddPicker();

        ServiceDescriptor descriptor = Assert.Single(services, d => d.ServiceType == typeof(PickerForm));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        Assert.Equal(typeof(PickerForm), descriptor.ImplementationType);
        Assert.Null(descriptor.ImplementationInstance);
        Assert.Null(descriptor.ImplementationFactory);
    }

    [Fact]
    public void AddPicker_keeps_existing_registrations()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IFormatProvider>(CultureInfo.InvariantCulture);
        ServiceDescriptor existing = services.Single();

        services.AddPicker();

        Assert.Contains(existing, services);
    }

    [Fact]
    public void AddPicker_adds_only_the_window_registration()
    {
        IServiceCollection services = new ServiceCollection().AddPicker();

        ServiceDescriptor only = Assert.Single(services);
        Assert.Equal(typeof(PickerForm), only.ServiceType);
    }
}
