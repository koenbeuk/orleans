using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Orleans.CodeGenerator.Tests.Serializables;

public abstract class SerialiableGeneratorTestBase<TSelf> : GeneratorTestBase<TSelf>
    where TSelf : SerialiableGeneratorTestBase<TSelf>
{
    protected SerialiableGeneratorTestBase(GeneratorTestFixture<TSelf> fixture) : base(fixture)
    {
    }

    protected abstract string SymbolName { get; }

    [Fact]
    public void GeneratedTreeIsNamedAfterType()
    {
        var generatedTree = Assert.Single(DriverResult.GeneratedTrees);

        Assert.EndsWith($"{SymbolName}.g.cs", generatedTree.FilePath);
    }

    [Fact]
    public void HasGeneratedCodec()
    {
        AssertGeneratedArtifact($"Codec_{SymbolName}", SymbolName);
    }

    [Fact]
    public void HasGeneratedCopier()
    {
        AssertGeneratedCopier($"Copier_{SymbolName}", SymbolName);
    }

    [Fact]
    public void HasGeneratedActivator()
    {
        AssertGeneratedArtifact($"Activator_{SymbolName}", SymbolName);
    }
}
