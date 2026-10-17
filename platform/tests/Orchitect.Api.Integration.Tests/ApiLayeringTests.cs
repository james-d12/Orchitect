namespace Orchitect.Api.Integration.Tests;

public sealed class ApiLayeringTests
{
    private const string ExecutionAssembly = "Orchitect.Engine.Execution";

    [Fact]
    public void Api_DoesNotReferenceEngineExecution()
    {
        var references = typeof(Program).Assembly.GetReferencedAssemblies().Select(reference => reference.Name);

        Assert.DoesNotContain(ExecutionAssembly, references);
        Assert.False(File.Exists(Path.Combine(AppContext.BaseDirectory, $"{ExecutionAssembly}.dll")),
            $"{ExecutionAssembly} is in the API's dependency graph.");
    }
}
