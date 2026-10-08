using System;
using Feature.Catalog;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Feature.Catalog.Tests;

/// <summary>CatalogServices.AddCatalog: the registration contract.</summary>
public sealed class AddCatalogTests
{
    [Fact]
    public void Null_services_throws_argument_null()
    {
        Assert.Throws<ArgumentNullException>(() => CatalogServices.AddCatalog(null!, new CatalogOptions()));
    }

    [Fact]
    public void Null_options_throws_argument_null()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddCatalog(null!));
    }

    [Fact]
    public void The_catalog_is_a_singleton()
    {
        using ServiceProvider provider = new ServiceCollection().AddCatalog(new CatalogOptions()).BuildServiceProvider();

        IProjectCatalog first = provider.GetRequiredService<IProjectCatalog>();
        IProjectCatalog second = provider.GetRequiredService<IProjectCatalog>();

        Assert.Same(first, second);
    }

    [Fact]
    public void The_options_instance_is_registered_as_given()
    {
        var options = new CatalogOptions { IncludeWorktrees = true };
        using ServiceProvider provider = new ServiceCollection().AddCatalog(options).BuildServiceProvider();

        Assert.Same(options, provider.GetRequiredService<CatalogOptions>());
    }
}
