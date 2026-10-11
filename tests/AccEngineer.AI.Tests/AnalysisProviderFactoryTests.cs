using AccEngineer.AI;

namespace AccEngineer.AI.Tests;

public class AnalysisProviderFactoryTests
{
    [Fact]
    public void Create_NoOptions_ReturnsNull()
    {
        var p = AnalysisProviderFactory.Create(null);
        Assert.IsType<NullAnalysisProvider>(p);
        Assert.False(p.IsConfigured);
    }

    [Fact]
    public void Create_OpenRouter_InfersEndpointAndKey()
    {
        var p = AnalysisProviderFactory.Create(new AiProviderOptions
        {
            Provider = "openrouter",
            Model = "test/model",
            ApiKey = "sk-or-v1-abc"
        });
        Assert.IsType<OpenAiCompatibleProvider>(p);
        Assert.True(p.IsConfigured);
    }

    [Fact]
    public void ResolveEndpoint_LocalVariants_Work()
    {
        Assert.Contains("localhost:1234", AnalysisProviderFactory.ResolveEndpoint(new AiProviderOptions { Provider = "lmstudio" }) ?? string.Empty);
        Assert.Contains("localhost:11434", AnalysisProviderFactory.ResolveEndpoint(new AiProviderOptions { Provider = "ollama" }) ?? string.Empty);
        Assert.Contains("openrouter.ai", AnalysisProviderFactory.ResolveEndpoint(new AiProviderOptions { Provider = "openrouter" }) ?? string.Empty);
    }

    [Fact]
    public void ResolveApiKey_UsesEnvironment_Fallback()
    {
        var before1 = Environment.GetEnvironmentVariable(AnalysisProviderFactory.ApiKeyEnvironmentVariable);
        var before2 = Environment.GetEnvironmentVariable(AnalysisProviderFactory.ApiKeyFallbackEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(AnalysisProviderFactory.ApiKeyEnvironmentVariable, null);
            Environment.SetEnvironmentVariable(AnalysisProviderFactory.ApiKeyFallbackEnvironmentVariable, "fallback-key");
            Assert.Equal("fallback-key", AnalysisProviderFactory.ResolveApiKey(new AiProviderOptions { Provider = "openrouter", Model = "m" }));
        }
        finally
        {
            Environment.SetEnvironmentVariable(AnalysisProviderFactory.ApiKeyEnvironmentVariable, before1);
            Environment.SetEnvironmentVariable(AnalysisProviderFactory.ApiKeyFallbackEnvironmentVariable, before2);
        }
    }
}
