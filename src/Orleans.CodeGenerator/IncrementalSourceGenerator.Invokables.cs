using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Orleans.CodeGenerator.SyntaxGeneration;

namespace Orleans.CodeGenerator;

partial class IncrementalSourceGenerator
{
    static IEnumerable<(InvokableInterfaceDescription invokableInterfaceDescription, LibraryTypes libraryTypes)> GetSemanticTargetForInvokableGeneration((GeneratorSyntaxContext context, LibraryTypes libraryTypes) _, CancellationToken cancellationToken)
    {
        var (context, libraryTypes) = _;

        var symbol = context.SemanticModel.GetDeclaredSymbol(context.Node, cancellationToken) as INamedTypeSymbol;
        if (symbol is not null)
        {
            var attribute = HasAttribute(symbol, libraryTypes.GenerateMethodSerializersAttribute, inherited: true);
            if (attribute is not null)
            {
                var prop = symbol.GetAllMembers<IPropertySymbol>().FirstOrDefault();
                if (prop is { })
                {
                    throw new InvalidOperationException($"Invokable type {symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} contains property {prop.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}. Invokable types cannot contain properties.");
                }

                var baseClass = (INamedTypeSymbol)attribute.ConstructorArguments[0].Value;
                var isExtension = (bool)attribute.ConstructorArguments[1].Value;
                var invokableBaseTypes = GetInvokableBaseTypes(baseClass, libraryTypes);

                var description = new InvokableInterfaceDescription(
                    libraryTypes,
                    context.SemanticModel,
                    symbol,
                    GetTypeAlias(symbol, libraryTypes) ?? symbol.Name,
                    baseClass,
                    isExtension,
                    invokableBaseTypes);

                yield return (description, libraryTypes);
            }
        }
    }


    static void EmitInvokablesSourceFile(SourceProductionContext context, (InvokableInterfaceDescription invokableInterfaceDescription, LibraryTypes libraryTypes) _)
    {
        var (invokableInterfaceDescription, libraryTypes) = _;

        var sb = new StringBuilder(1024);
        sb.AppendLine("using global::Orleans.Serialization.Codecs;");
        sb.AppendLine("using global::Orleans.Serialization.GeneratedCodeHelpers;");
        sb.AppendLine();

        sb.AppendLine(MetadataGenerator.GenerateMetadataProviderAttribute(invokableInterfaceDescription, libraryTypes).NormalizeWhitespace().ToFullString());
        sb.AppendLine();

        sb.AppendLine($"namespace {invokableInterfaceDescription.GeneratedNamespace};");
        sb.AppendLine();

        var metadataModel = new MetadataModel();

        foreach (var method in invokableInterfaceDescription.Methods)
        {
            var (generatedInvokerClassSyntax, generatedInvokerDescription) = InvokableGenerator.Generate(libraryTypes, invokableInterfaceDescription, method);

            metadataModel.GeneratedInvokables[method] = generatedInvokerDescription;

            sb.AppendLine(generatedInvokerClassSyntax.NormalizeWhitespace().ToFullString());
            sb.AppendLine();

            sb.AppendLine(SerializerGenerator.GenerateSerializer(libraryTypes, generatedInvokerDescription).NormalizeWhitespace().ToFullString());
            sb.AppendLine();

            sb.AppendLine(CopierGenerator.GenerateCopier(libraryTypes, generatedInvokerDescription).NormalizeWhitespace().ToFullString());
            sb.AppendLine();

            sb.AppendLine();
            sb.AppendLine(ActivatorGenerator.GenerateActivator(libraryTypes, generatedInvokerDescription).NormalizeWhitespace().ToFullString());
        }

        var (proxyClassSyntax, _) = ProxyGenerator.Generate(libraryTypes, invokableInterfaceDescription, metadataModel);

        sb.AppendLine();
        sb.AppendLine(proxyClassSyntax.NormalizeWhitespace().ToFullString());

        sb.AppendLine(MetadataGenerator.GenerateMetadata(invokableInterfaceDescription, metadataModel, libraryTypes).NormalizeWhitespace().ToFullString());

        var generatedFileName = invokableInterfaceDescription.TypeParameters switch {
            { Count: > 0 } => $"{invokableInterfaceDescription.GeneratedNamespace}.{invokableInterfaceDescription.Name}+{invokableInterfaceDescription.TypeParameters.Count}.g.cs",
            _ => $"{invokableInterfaceDescription.GeneratedNamespace}.{invokableInterfaceDescription.Name}.g.cs"
        };

        context.AddSource(generatedFileName, sb.ToString());
    }

    static IEnumerable<(INamedTypeSymbol, INamedTypeSymbol)> GetInvokableBaseTypes(INamedTypeSymbol baseClass, LibraryTypes libraryTypes)
    {
        if (baseClass.GetAttributes(libraryTypes.DefaultInvokableBaseTypeAttribute, out var invokableBaseTypeAttributes))
        {
            foreach (var attr in invokableBaseTypeAttributes)
            {
                var ctorArgs = attr.ConstructorArguments;
                var returnType = (INamedTypeSymbol)ctorArgs[0].Value;
                var invokableBaseType = (INamedTypeSymbol)ctorArgs[1].Value;

                yield return (returnType, invokableBaseType);
            }
        }
    }
}
    
