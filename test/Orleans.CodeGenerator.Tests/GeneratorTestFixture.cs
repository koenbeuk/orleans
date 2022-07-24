using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Options;

namespace Orleans.CodeGenerator.Tests;

public class GeneratorTestFixture<TUnitTest>
    where TUnitTest : GeneratorTestBase<TUnitTest>
{
    public GeneratorTestFixture()
    {
        var sourceTextInstance = Activator.CreateInstance(typeof(TUnitTest), this);
        var sourceText = typeof(TUnitTest).GetProperty("SourceText", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(sourceTextInstance) as string;

        Compilation = CreateCompilation(sourceText);
        Driver = RunGenerator(Compilation);
    }

    public Compilation Compilation { get; }

    public GeneratorDriverRunResult Driver { get; }

    public Compilation CreateCompilation(string source, IEnumerable<SyntaxTree> additionalSyntaxTrees = null)
    {
        var references = Basic.Reference.Assemblies.Net60.All.ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(GenerateSerializerAttribute).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(IdAttribute).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Serialization.Configuration.ITypeManifestProvider).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(IOptions<>).Assembly.Location));

        var syntaxTrees = (additionalSyntaxTrees ?? Enumerable.Empty<SyntaxTree>())
            .Append(CSharpSyntaxTree.ParseText(source));

        var compilation = CSharpCompilation.Create("compilation",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation;
    }

    public GeneratorDriverRunResult RunGenerator(Compilation compilation)
    {
        var subject = new IncrementalSourceGenerator();
        var driver = CSharpGeneratorDriver
            .Create(subject)
            .RunGenerators(compilation);

        return driver.GetRunResult();
    }
}
