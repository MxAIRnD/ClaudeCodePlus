using System;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Feature.ContextBar.Tests;

/// <summary>The module's registration: what a consumer gets from <see cref="ContextBarServices.AddContextBar"/>.</summary>
public sealed class ContextBarServicesTests
{
    [Fact]
    public void The_window_resolves_with_the_options_and_one_finder()
    {
        var options = new ContextBarOptions { SessionsRoot = null, FiguresRoot = null };
        using ServiceProvider provider = new ServiceCollection().AddContextBar(options).BuildServiceProvider();

        using ContextBarForm first = provider.GetRequiredService<ContextBarForm>();
        using ContextBarForm second = provider.GetRequiredService<ContextBarForm>();

        Assert.NotSame(first, second);
        Assert.Same(options, provider.GetRequiredService<ContextBarOptions>());
        Assert.Same(provider.GetRequiredService<FocusedSessionFinder>(), provider.GetRequiredService<FocusedSessionFinder>());
        Assert.False(first.Visible);
        Assert.True(first.TopMost);
        Assert.False(first.ShowInTaskbar);
    }

    [Fact]
    public void Null_arguments_throw()
    {
        Assert.Throws<ArgumentNullException>(() => ContextBarServices.AddContextBar(null!, new ContextBarOptions()));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddContextBar(null!));
        Assert.Throws<ArgumentNullException>(() => new ContextBarForm(null!, new FocusedSessionFinder(null)));
        Assert.Throws<ArgumentNullException>(() => new ContextBarForm(new ContextBarOptions(), null!));
    }
}
