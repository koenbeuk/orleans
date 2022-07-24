using Microsoft.CodeAnalysis;
using Xunit;

namespace Orleans.CodeGenerator.Tests;

public abstract class GeneratorTestBase<TSelf> : IClassFixture<GeneratorTestFixture<TSelf>>
    where TSelf : GeneratorTestBase<TSelf>
{
    private readonly GeneratorTestFixture<TSelf> _fixture;

    public GeneratorTestBase(GeneratorTestFixture<TSelf> fixture)
    {
        _fixture = fixture;
    }

    protected abstract string SourceText { get; }

    protected Compilation Compilation => _fixture.Compilation;

    protected GeneratorDriverRunResult DriverResult => _fixture.Driver;

    [Fact]
    public void GeneratorProducedNoDiagnostics()
    {
        Assert.Empty(_fixture.Driver.Diagnostics);
    }

    [Fact]
    public void GeneratedSourceCompiles()
    {
        var compilation = _fixture.CreateCompilation(SourceText, DriverResult.GeneratedTrees);

        Assert.Empty(compilation.GetDiagnostics());
    }
}
