using IdentityService.Infrastructure.Email;

namespace IdentityService.UnitTests.Infrastructure.Email;

public sealed class FileEmailTemplateRendererTests
{
    [Fact]
    public async Task RenderAsync_should_replace_placeholders_Async()
    {
        var templatePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        await File.WriteAllTextAsync(
            templatePath,
            "<p>{{UserName}}</p><p>{{MerchantId}}</p><a href=\"{{AuthenticationLink}}\">Entrar</a>",
            TestContext.Current.CancellationToken);

        try
        {
            var renderer = new FileEmailTemplateRenderer();

            var result = await renderer.RenderAsync(
                templatePath,
                new Dictionary<string, string>
                {
                    ["UserName"] = "Ana Identity",
                    ["MerchantId"] = "merchant-123",
                    ["AuthenticationLink"] = "https://auth.localhost/login?client=identity"
                },
                TestContext.Current.CancellationToken);

            Assert.Contains("Ana Identity", result, StringComparison.Ordinal);
            Assert.Contains("merchant-123", result, StringComparison.Ordinal);
            Assert.Contains("https://auth.localhost/login?client=identity", result, StringComparison.Ordinal);
            Assert.DoesNotContain("{{UserName}}", result, StringComparison.Ordinal);
            Assert.DoesNotContain("{{MerchantId}}", result, StringComparison.Ordinal);
            Assert.DoesNotContain("{{AuthenticationLink}}", result, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(templatePath);
        }
    }
}
