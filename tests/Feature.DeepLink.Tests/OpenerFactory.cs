using Microsoft.Extensions.DependencyInjection;

namespace Feature.DeepLink.Tests;

/// <summary>Obtains the opener the way a consumer does: through the module's registration.</summary>
internal static class OpenerFactory
{
    public static ISessionOpener Create() =>
        new ServiceCollection().AddDeepLink().BuildServiceProvider().GetRequiredService<ISessionOpener>();
}
