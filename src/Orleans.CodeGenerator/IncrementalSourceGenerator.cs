using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Orleans.CodeGenerator.SyntaxGeneration;

namespace Orleans.CodeGenerator;

[Generator(LanguageNames.CSharp)]
public partial class IncrementalSourceGenerator : IIncrementalGenerator
{
    private readonly CodeGeneratorOptions _options;

    public IncrementalSourceGenerator()
    {
        _options = new CodeGeneratorOptions
        {
            GenerateSerializerAttributes =
            {
                // TODO: Should this be part of the defaults in the Options class?
                "Orleans.GenerateSerializerAttribute"
            }
        };
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var generateSerializerAttributesProvider = context.CompilationProvider
            .SelectMany((c, _) => _options.GenerateSerializerAttributes.Select(c.GetTypeByMetadataName))
            .Where(x => x is not null)
            .Collect();

        var libraryTypesProvider = context.CompilationProvider
            .Select((c, _) => LibraryTypes.FromCompilation(c, _options));

        var generateSerializersProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is TypeDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (node, _) => node
            )
            .Combine(generateSerializerAttributesProvider)
            .Combine(libraryTypesProvider)
            .Select(GetSemanticTargetForGeneration)
            .Where(x => x != default);

        context.RegisterSourceOutput(generateSerializersProvider, EmitSourceFile);
    }
}   
