using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

    protected virtual int TypeArguments => 0;

    protected Compilation Compilation => _fixture.Compilation;

    protected GeneratorDriverRunResult DriverResult => _fixture.Driver;

    [Fact]
    public void SourceTextProducedNoDiagnostics()
    {
        Assert.Empty(_fixture.Compilation.GetDiagnostics().Where(x => x.Severity >= DiagnosticSeverity.Warning));
    }

    [Fact]
    public void GeneratorProducedNoDiagnostics()
    {
        Assert.Empty(_fixture.Driver.Diagnostics);
    }

    [Fact]
    public void GeneratedSourceCompiles()
    {
        var compilation = _fixture.CreateCompilation(SourceText, DriverResult.GeneratedTrees);

        Assert.Empty(compilation.GetDiagnostics().Where(x => x.Severity >= DiagnosticSeverity.Warning));
    }

    protected void AssertGeneratedTypeManifestProviderAttribute(string className)
    {
        AttributeSyntax generatedAttributeSyntax = null;

        foreach (var generatedSyntaxTree in DriverResult.GeneratedTrees)
        {
            generatedAttributeSyntax = generatedSyntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<AttributeSyntax>()
                .Where(x => x.Name.ToString() == "global::Orleans.Serialization.Configuration.TypeManifestProviderAttribute")
                .FirstOrDefault();

            if (generatedAttributeSyntax is not null)
            {
                break;
            }
        }

        Assert.NotNull(generatedAttributeSyntax);
    }

    protected GeneratedCopierDescriptor AssertGeneratedCopier(string className, string generatedForName, int typeArguments)
    {
        var artifact = AssertGeneratedArtifact(className, generatedForName, typeArguments);
        return new GeneratedCopierDescriptor(artifact);
    }

    protected ClassDeclarationSyntax AssertGeneratedArtifact(string className, string generatedForName, int typeArguments)
    {
        ClassDeclarationSyntax foundGeneratedClass = null;
        var foundGeneratedMetadataRegistration = false;

        foreach (var generatedSyntaxTree in DriverResult.GeneratedTrees)
        {
            var generatedClass = generatedSyntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(x => x.Identifier.ValueText == className);

            if (generatedClass is not null)
            {
                foundGeneratedClass = generatedClass;
            }

            var generatedMetadataSyntax = generatedSyntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(x => x.Identifier.ValueText == $"Metadata_{generatedForName}_{typeArguments}");

            if (generatedMetadataSyntax is not null)
            {
                var generatedConfigureMethodSyntax = generatedMetadataSyntax.Members
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(x => x.Identifier.ValueText == "Configure");

                if (generatedConfigureMethodSyntax is not null)
                {
                    var generatedRegistrationStatement = generatedConfigureMethodSyntax.Body
                        .Statements
                        .SelectMany(x => x.DescendantNodes())
                        .OfType<IdentifierNameSyntax>()
                        .Where(x => x.Identifier.ValueText == className);
                    
                    foundGeneratedMetadataRegistration = true;
                }
            }
        }

        Assert.True(foundGeneratedClass is not null, $"Expected class {className} to have been generated for {generatedForName}. Found none");
        Assert.True(foundGeneratedMetadataRegistration, $"Expected class {className} to have been registered as metadata, found none");

        return foundGeneratedClass;
    }
}
