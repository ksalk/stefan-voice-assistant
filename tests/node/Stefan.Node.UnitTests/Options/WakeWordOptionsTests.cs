using Stefan.Node.Options;

namespace Stefan.Node.UnitTests.Options;

public class WakeWordOptionsTests
{
    [Theory]
    [InlineData("nanowakeword", "nanowakeword")]
    [InlineData("NanoWakeWord", "nanowakeword")]
    [InlineData("sherpa", "sherpa")]
    [InlineData("SHERPA", "sherpa")]
    [InlineData("  sherpa  ", "sherpa")]
    public void Normalize_ReturnsCanonicalName(string input, string expected)
    {
        Assert.Equal(expected, WakeWordOptions.Normalize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("vosk")]
    [InlineData(null)]
    public void Normalize_RejectsUnsupportedValue(string? input)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => WakeWordOptions.Normalize(input));

        Assert.Contains("Supported values: nanowakeword, sherpa", exception.Message);
    }
}
