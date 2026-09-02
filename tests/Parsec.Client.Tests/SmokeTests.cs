namespace Parsec.Client.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void InterfaceContractIsExposed() =>
        Assert.NotNull(typeof(IParsecClient).GetProperty(nameof(IParsecClient.ProviderName)));
}
