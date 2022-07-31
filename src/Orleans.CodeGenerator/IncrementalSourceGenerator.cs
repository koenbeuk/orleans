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
                transform: static (context, _) => context
            )
            .Combine(generateSerializerAttributesProvider)
            .Combine(libraryTypesProvider)
            .Select(GetSemanticTargetForSerializerGeneration)
            .Where(x => x != default);

        context.RegisterSourceOutput(generateSerializersProvider, EmitSerializerSourceFile);

        var generateInvokablesProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                transform: static (context, _) => context
            )
            .Combine(libraryTypesProvider)
            .SelectMany(GetSemanticTargetForInvokableGeneration)
            .Where(x => x != default);

        context.RegisterSourceOutput(generateInvokablesProvider, EmitInvokablesSourceFile);
    }

    private static AttributeData HasAttribute(INamedTypeSymbol symbol, ISymbol attributeType, bool inherited = false)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
            {
                return attribute;
            }
        }

        if (inherited)
        {
            foreach (var iface in symbol.AllInterfaces)
            {
                foreach (var attribute in iface.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
                    {
                        return attribute;
                    }
                }
            }

            while ((symbol = symbol.BaseType) != null)
            {
                foreach (var attribute in symbol.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
                    {
                        return attribute;
                    }
                }
            }
        }

        return null;
    }

    internal static ushort? GetId(LibraryTypes libraryTypes, ISymbol memberSymbol)
    {
        var idAttr = memberSymbol.GetAttributes().FirstOrDefault(attr => libraryTypes.IdAttributeTypes.Any(t => SymbolEqualityComparer.Default.Equals(t, attr.AttributeClass)));
        if (idAttr is null)
        {
            return null;
        }

        var id = (ushort)idAttr.ConstructorArguments.First().Value;
        return id;
    }

    private static string GetTypeAlias(ISymbol symbol, LibraryTypes libraryTypes)
    {
        var attr = symbol.GetAttributes().FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(libraryTypes.WellKnownAliasAttribute, attr.AttributeClass));
        if (attr is null)
        {
            return null;
        }

        var value = (string)attr.ConstructorArguments.First().Value;
        return value;
    }
}   
