using System;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Feature.ChipButton.Tests;

/// <summary>
/// Black-box behavior of <see cref="ChipButtonServices.AddChipButton"/>: inspects the
/// registration and resolves the window from a container. The window is constructed (hidden,
/// not watching) and disposed; it is never shown and <c>StartWatching</c> is never called.
/// </summary>
public sealed class ChipButtonServicesTests
{
    private static ChipButtonOptions Options() => new() { DiagnosticsFile = null };

    [Fact]
    public void AddChipButton_null_arguments_throw_ArgumentNullException()
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => ChipButtonServices.AddChipButton(null!, Options()));

        Assert.Equal("services", ex.ParamName);
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddChipButton(null!));
        Assert.Throws<ArgumentNullException>(() => new ChipButtonForm(null!));
    }

    [Fact]
    public void AddChipButton_returns_the_same_collection()
    {
        IServiceCollection services = new ServiceCollection();

        Assert.Same(services, services.AddChipButton(Options()));
    }

    [Fact]
    public void AddChipButton_registers_ChipButtonForm_as_transient_by_its_own_type()
    {
        IServiceCollection services = new ServiceCollection().AddChipButton(Options());

        ServiceDescriptor descriptor = Assert.Single(services, d => d.ServiceType == typeof(ChipButtonForm));
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        Assert.Equal(typeof(ChipButtonForm), descriptor.ImplementationType);
        Assert.Null(descriptor.ImplementationInstance);
        Assert.Null(descriptor.ImplementationFactory);
    }

    [Fact]
    public void AddChipButton_keeps_existing_registrations()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddSingleton<IFormatProvider>(CultureInfo.InvariantCulture);
        ServiceDescriptor existing = services.Single();

        services.AddChipButton(Options());

        Assert.Contains(existing, services);
    }

    [Fact]
    public void AddChipButton_adds_the_options_and_the_window_registrations_only()
    {
        ChipButtonOptions options = Options();
        IServiceCollection services = new ServiceCollection().AddChipButton(options);

        Assert.Equal(2, services.Count);
        ServiceDescriptor registered = Assert.Single(services, d => d.ServiceType == typeof(ChipButtonOptions));
        Assert.Equal(ServiceLifetime.Singleton, registered.Lifetime);
        Assert.Same(options, registered.ImplementationInstance);
    }

    [Fact]
    public void AddChipButton_window_resolves_twice_as_distinct_hidden_instances()
    {
        using ServiceProvider provider = new ServiceCollection().AddChipButton(Options()).BuildServiceProvider();

        ChipButtonForm first = provider.GetRequiredService<ChipButtonForm>();
        ChipButtonForm second = provider.GetRequiredService<ChipButtonForm>();

        Assert.NotSame(first, second);
        Assert.False(first.Visible);
        Assert.False(second.Visible);
    }
}
