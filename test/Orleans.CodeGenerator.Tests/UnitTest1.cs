using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Orleans.CodeGenerator.Tests;

public class SerializerGeneratorTests : GeneratorTestBase<SerializerGeneratorTests>
{
    public SerializerGeneratorTests(GeneratorTestFixture<SerializerGeneratorTests> fixture) : base(fixture)
    {
    }

    protected override string SourceText { get; } = @"
        [Orleans.GenerateSerializer]
        public class Test {
            [Orleans.Id(0)]
            public int A;
        }";

    [Fact]
    public void GeneratedTreeIsNamedAfterType()
    {
        var generatedTree = Assert.Single(DriverResult.GeneratedTrees);

        Assert.EndsWith("Test.g.cs", generatedTree.FilePath);
    }

    [Fact]
    public void HasGeneratedCodec()
    {
        var generatedTree = Assert.Single(DriverResult.GeneratedTrees);

        var generatedSerializer = generatedTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(x => x.Identifier.ValueText == "Codec_Test");

        Assert.NotNull(generatedSerializer);
    }

    [Fact]
    public void HasGeneratedCopier()
    {
        var generatedTree = Assert.Single(DriverResult.GeneratedTrees);

        var generatedSerializer = generatedTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(x => x.Identifier.ValueText == "Copier_Test");

        Assert.NotNull(generatedSerializer);
    }

    [Fact]
    public void HasGeneratedActivator()
    {
        var generatedTree = Assert.Single(DriverResult.GeneratedTrees);

        var generatedSerializer = generatedTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(x => x.Identifier.ValueText == "Activator_Test");

        Assert.NotNull(generatedSerializer);
    }

    [Fact]
    public void HasGeneratedMetadata()
    {
        var generatedTree = Assert.Single(DriverResult.GeneratedTrees);

        var generatedSerializer = generatedTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(x => x.Identifier.ValueText == "Metadata_Test");

        Assert.NotNull(generatedSerializer);
    }

    [Fact]
    public void HasGeneratedTypeManifestProviderAttribute()
    {
        var generatedTree = Assert.Single(DriverResult.GeneratedTrees);

        var generatedSerializer = generatedTree.GetRoot()
            .DescendantNodes()
            .OfType<AttributeSyntax>()
            .FirstOrDefault(x => x.Name.ToString() == "global::Orleans.Serialization.Configuration.TypeManifestProviderAttribute");

        Assert.NotNull(generatedSerializer);
    }
}