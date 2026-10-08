using System;
using System.Linq;
using Feature.Catalog;
using Feature.DeepLink;
using Feature.Picker;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Tests.Flows;

/// <summary>
/// Flow 1, compose step: one service collection built like the composition root
/// (AddCatalog + AddDeepLink + AddPicker) resolves every surface the flow needs.
/// Never constructs <see cref="PickerForm"/> (no window) and never calls
/// <see cref="ISessionOpener.OpenNewSession"/> (it would start a real session).
/// </summary>
public sealed class CompositionFlowTests
{
    [Fact]
    public void Compose_registers_catalog_opener_options_and_picker_with_their_lifetimes()
    {
        var options = new CatalogOptions();
        IServiceCollection services = FlowFixture.Compose(options);

        Assert.Contains(services, d => d.ServiceType == typeof(IProjectCatalog) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d => d.ServiceType == typeof(ISessionOpener) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d => d.ServiceType == typeof(CatalogOptions) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d => d.ServiceType == typeof(PickerForm) && d.Lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public void Compose_validates_on_build_so_every_dependency_of_the_picker_is_satisfiable()
    {
        // ValidateOnBuild checks each registration's constructor call site without creating it:
        // PickerForm(IProjectCatalog, ISessionOpener) must be resolvable from this collection alone.
        IServiceCollection services = FlowFixture.Compose(new CatalogOptions());

        Exception? error = Record.Exception(() =>
        {
            using ServiceProvider provider = services.BuildServiceProvider(
                new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        });

        Assert.Null(error);
    }

    [Fact]
    public void Picker_alone_does_not_validate_its_dependencies_come_from_the_other_two_modules()
    {
        IServiceCollection services = new ServiceCollection().AddPicker();

        Assert.ThrowsAny<Exception>(() =>
            services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true }));
    }

    [Fact]
    public void Provider_resolves_catalog_and_opener_as_singletons_and_the_options_instance_given()
    {
        var options = new CatalogOptions { DesktopSessionsRoot = null };
        using ServiceProvider provider = FlowFixture.Compose(options).BuildServiceProvider();

        IProjectCatalog catalog = provider.GetRequiredService<IProjectCatalog>();
        ISessionOpener opener = provider.GetRequiredService<ISessionOpener>();

        Assert.Same(catalog, provider.GetRequiredService<IProjectCatalog>());
        Assert.Same(opener, provider.GetRequiredService<ISessionOpener>());
        Assert.Same(options, provider.GetRequiredService<CatalogOptions>());
    }

    [Fact]
    public void Picker_registration_is_one_transient_descriptor_for_the_form_type_itself()
    {
        IServiceCollection services = FlowFixture.Compose(new CatalogOptions());

        ServiceDescriptor picker = Assert.Single(services, d => d.ServiceType == typeof(PickerForm));
        Assert.Equal(typeof(PickerForm), picker.ImplementationType);
        Assert.Equal(ServiceLifetime.Transient, picker.Lifetime);
        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(IProjectCatalog)));
        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(ISessionOpener)));
    }
}
