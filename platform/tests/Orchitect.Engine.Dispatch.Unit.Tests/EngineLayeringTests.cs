using System.Reflection;
using Orchitect.Engine.Contracts.Runner;
using Orchitect.Engine.Execution;

namespace Orchitect.Engine.Dispatch.Unit.Tests;

public sealed class EngineLayeringTests
{
    private static readonly Assembly Contracts = typeof(RunnerOperation).Assembly;
    private static readonly Assembly Dispatch = typeof(DispatchExtensions).Assembly;
    private static readonly Assembly Execution = typeof(ExecutionExtensions).Assembly;
    private static readonly Assembly Runner = Assembly.Load("Orchitect.Runner");

    [Fact]
    public void Contracts_DoesNotReferenceAnyOrchitectAssembly()
    {
        Assert.DoesNotContain(ReferencedNames(Contracts),
            name => name?.StartsWith("Orchitect.", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Dispatch_DoesNotReferenceExecution()
    {
        Assert.Contains(Contracts.GetName().Name, ReferencedNames(Dispatch));
        Assert.DoesNotContain(Execution.GetName().Name, ReferencedNames(Dispatch));
    }

    [Fact]
    public void Execution_DoesNotReferenceDispatch()
    {
        Assert.Contains(Contracts.GetName().Name, ReferencedNames(Execution));
        Assert.DoesNotContain(Dispatch.GetName().Name, ReferencedNames(Execution));
    }

    [Fact]
    public void Runner_DoesNotReferenceDispatch()
    {
        Assert.Contains(Execution.GetName().Name, ReferencedNames(Runner));
        Assert.DoesNotContain(Dispatch.GetName().Name, ReferencedNames(Runner));
    }

    [Fact]
    public void Runner_DoesNotReferencePersistenceOrDomain()
    {
        Assert.DoesNotContain(ReferencedNames(Runner),
            name => name is "Orchitect.Persistence" or "Orchitect.Domain");
        Assert.DoesNotContain("Orchitect.Domain", ReferencedNames(Execution));
    }

    private static IEnumerable<string?> ReferencedNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(reference => reference.Name);
}
