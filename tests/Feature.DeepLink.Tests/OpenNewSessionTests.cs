using System;
using Xunit;

namespace Feature.DeepLink.Tests;

// SAFETY: OpenNewSession with a valid, fully qualified path starts a REAL Claude desktop session
// on this machine. The only calls allowed here are ones that must throw ArgumentException before
// any launch (a relative or empty path). Never add a call with a valid, fully qualified path.
public sealed class OpenNewSessionTests
{
    private readonly ISessionOpener _opener = OpenerFactory.Create();

    [Theory]
    [InlineData(@"RnDs\AI")]
    [InlineData(@"C:RnDs\AI")]
    [InlineData("")]
    public void An_invalid_path_throws_ArgumentException_before_any_launch(string path)
    {
        Assert.ThrowsAny<ArgumentException>(() => _opener.OpenNewSession(path));
    }
}
