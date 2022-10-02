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

    protected virtual string SymbolName => "Test";

    [Fact]
    public void GeneratedTreeIsNamedAfterType()
    {
        var generatedTree = Assert.Single(DriverResult.GeneratedTrees);

        var expectedFileName = TypeArguments is 0 ? $"{SymbolName}.g.cs" : $"{SymbolName}+{TypeArguments}.g.cs";
        Assert.EndsWith(expectedFileName, generatedTree.FilePath);
    }

    [Fact]
    public void HasGeneratedTypeManifestProviderAttribute()
    {
        AssertGeneratedTypeManifestProviderAttribute($"Metadata_{SymbolName}_{TypeArguments}");
    }

    [Fact]
    public void HasGeneratedCodec()
    {
        AssertGeneratedArtifact($"Codec_{SymbolName}", SymbolName, TypeArguments);
    }

    [Fact]
    public void HasGeneratedCopier()
    {
        AssertGeneratedCopier($"Copier_{SymbolName}", SymbolName, TypeArguments);
    }

    [Fact]
    public void HasGeneratedActivator()
    {
        AssertGeneratedArtifact($"Activator_{SymbolName}", SymbolName, TypeArguments);
    }
}
