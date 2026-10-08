using System;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Feature.DeepLink.Tests;

public sealed class DeepLinkServicesTests
{
    [Fact]
    public void AddDeepLink_throws_ArgumentNullException_for_a_null_collection()
    {
        Assert.Throws<ArgumentNullException>(() => DeepLinkServices.AddDeepLink(null!));
    }

    [Fact]
    public void AddDeepLink_returns_the_same_collection_for_chaining()
    {
        var services = new ServiceCollection();
        Assert.Same(services, services.AddDeepLink());
    }

    [Fact]
    public void AddDeepLink_registers_ISessionOpener_as_a_singleton()
    {
        IServiceCollection services = new ServiceCollection().AddDeepLink();
        ServiceDescriptor descriptor = Assert.Single(services, d => d.ServiceType == typeof(ISessionOpener));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Same(provider.GetRequiredService<ISessionOpener>(), provider.GetRequiredService<ISessionOpener>());
    }

    [Fact]
    public void The_registered_opener_is_usable()
    {
        ISessionOpener opener = OpenerFactory.Create();
        Assert.Equal("claude://code/new?folder=C%3A%5CRnDs%5CAI", opener.BuildNewSessionLink(@"C:\RnDs\AI"));
    }
}
