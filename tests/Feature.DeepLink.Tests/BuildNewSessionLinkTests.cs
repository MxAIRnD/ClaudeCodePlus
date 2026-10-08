using System;
using Xunit;

namespace Feature.DeepLink.Tests;

// SAFETY: every test here goes through BuildNewSessionLink, which is pure. Nothing in this file
// calls OpenNewSession: with a valid path that would start a real Claude desktop session.
public sealed class BuildNewSessionLinkTests
{
    private const string AiPath = @"C:\RnDs\AI";
    private const string AiLink = "claude://code/new?folder=C%3A%5CRnDs%5CAI";

    private readonly ISessionOpener _opener = OpenerFactory.Create();

    [Fact]
    public void A_drive_path_becomes_the_exact_new_session_link()
    {
        Assert.Equal(AiLink, _opener.BuildNewSessionLink(AiPath));
    }

    [Fact]
    public void A_path_with_spaces_and_an_ampersand_is_fully_escaped()
    {
        Assert.Equal("claude://code/new?folder=C%3A%5CMy%20Projects%5Ca%26b%3Dc%20%231%20100%25",
            _opener.BuildNewSessionLink(@"C:\My Projects\a&b=c #1 100%"));
    }

    [Theory]
    [InlineData(@"C:\My Projects\a&b=c #1 100%")]
    [InlineData(@"D:\Work\naïve café\日本語")]
    [InlineData(@"C:\x+y;z,(1)[2]{3}'~!$@^")]
    public void Spaces_and_special_characters_are_encoded_as_EscapeDataString_encodes_them(string path)
    {
        Assert.Equal("claude://code/new?folder=" + Uri.EscapeDataString(path), _opener.BuildNewSessionLink(path));
    }

    [Fact]
    public void A_trailing_backslash_is_kept()
    {
        Assert.Equal(AiLink + "%5C", _opener.BuildNewSessionLink(AiPath + @"\"));
    }

    [Theory]
    [InlineData(@"C:\RnDs\..\RnDs\AI")]
    [InlineData(@"C:\RnDs\.\AI")]
    [InlineData(@"C:\RnDs\\AI")]
    [InlineData(@"C:/RnDs/AI")]
    [InlineData(@"c:\rnds\ai")]
    public void The_path_is_encoded_as_given_without_normalization(string path)
    {
        Assert.Equal("claude://code/new?folder=" + Uri.EscapeDataString(path), _opener.BuildNewSessionLink(path));
    }

    [Fact]
    public void A_made_up_path_that_does_not_exist_is_accepted()
    {
        const string path = @"Q:\no\such\folder\7f3c9e2a-made-up";
        Assert.Equal("claude://code/new?folder=" + Uri.EscapeDataString(path), _opener.BuildNewSessionLink(path));
    }

    [Fact]
    public void A_drive_root_is_accepted()
    {
        Assert.Equal("claude://code/new?folder=C%3A%5C", _opener.BuildNewSessionLink(@"C:\"));
    }

    [Fact]
    public void A_UNC_path_is_accepted()
    {
        Assert.Equal("claude://code/new?folder=%5C%5Cserver%5Cshare%5Cproj",
            _opener.BuildNewSessionLink(@"\\server\share\proj"));
    }

    [Theory]
    [InlineData(@"RnDs\AI")]
    [InlineData(@".\AI")]
    [InlineData(@"..\AI")]
    [InlineData("AI")]
    [InlineData(@"C:RnDs\AI")]
    [InlineData(@"\RnDs\AI")]
    public void A_relative_path_throws_ArgumentException(string path)
    {
        Assert.ThrowsAny<ArgumentException>(() => _opener.BuildNewSessionLink(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_null_empty_or_whitespace_path_throws_ArgumentException(string? path)
    {
        Assert.ThrowsAny<ArgumentException>(() => _opener.BuildNewSessionLink(path!));
    }

    [Fact]
    public void A_non_empty_prompt_appends_the_encoded_q_parameter()
    {
        Assert.Equal(AiLink + "&q=fix%20the%20bug%20%26%20add%20a%20test%3F",
            _opener.BuildNewSessionLink(AiPath, "fix the bug & add a test?"));
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("line one\nline two\ttab #hash =eq +plus /slash \\back")]
    [InlineData("überprüfen 日本語")]
    [InlineData(" ")]
    public void A_prompt_is_encoded_as_EscapeDataString_encodes_it(string prompt)
    {
        Assert.Equal(AiLink + "&q=" + Uri.EscapeDataString(prompt), _opener.BuildNewSessionLink(AiPath, prompt));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_null_or_empty_prompt_appends_nothing(string? prompt)
    {
        Assert.Equal(AiLink, _opener.BuildNewSessionLink(AiPath, prompt));
    }

    [Fact]
    public void Omitting_the_prompt_appends_nothing()
    {
        Assert.Equal(AiLink, _opener.BuildNewSessionLink(AiPath));
    }

    [Fact]
    public void A_prompt_of_exactly_MaxPromptLength_characters_is_accepted()
    {
        string prompt = new('a', ISessionOpener.MaxPromptLength);
        Assert.Equal(AiLink + "&q=" + prompt, _opener.BuildNewSessionLink(AiPath, prompt));
    }

    [Fact]
    public void The_prompt_cap_counts_characters_as_given_not_encoded()
    {
        // Each space encodes to three characters; the cap applies to the prompt as given.
        string prompt = new(' ', ISessionOpener.MaxPromptLength);
        Assert.Equal(AiLink + "&q=" + Uri.EscapeDataString(prompt), _opener.BuildNewSessionLink(AiPath, prompt));
    }

    [Fact]
    public void A_prompt_one_character_over_MaxPromptLength_throws_ArgumentException()
    {
        string prompt = new('a', ISessionOpener.MaxPromptLength + 1);
        Assert.ThrowsAny<ArgumentException>(() => _opener.BuildNewSessionLink(AiPath, prompt));
    }

    [Fact]
    public void MaxPromptLength_is_14336()
    {
        Assert.Equal(14336, ISessionOpener.MaxPromptLength);
    }

    [Fact]
    public void Building_is_pure_same_input_same_output()
    {
        const string path = @"C:\My Projects\a&b";
        string first = _opener.BuildNewSessionLink(path, "do it");
        string second = _opener.BuildNewSessionLink(path, "do it");
        string fromAnotherContainer = OpenerFactory.Create().BuildNewSessionLink(path, "do it");
        Assert.Equal(first, second);
        Assert.Equal(first, fromAnotherContainer);
    }
}
