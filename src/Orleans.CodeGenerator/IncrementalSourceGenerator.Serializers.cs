using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Orleans.CodeGenerator.SyntaxGeneration;

namespace Orleans.CodeGenerator;

public partial class IncrementalSourceGenerator
{
    (ISerializableTypeDescription serializableTypeDescription, LibraryTypes libraryTypes) GetSemanticTargetForSerializerGeneration(((GeneratorSyntaxContext context, ImmutableArray<INamedTypeSymbol> attributeTypeSymbols), LibraryTypes libraryTypes) _, CancellationToken cancellationToken)
    {
        var ((context, attributeTypeSymbols), libraryTypes) = _;

        var symbol = context.SemanticModel.GetDeclaredSymbol(context.Node, cancellationToken) as INamedTypeSymbol;

        if (symbol is not null)
        {
            // Find a GenerateSerializerAttribute
            AttributeData generateSerializerAttributeData = null;

            foreach (var attributeData in symbol.GetAttributes())
            {
                foreach (var attributeTypeSymbol in attributeTypeSymbols)
                {
                    if (SymbolEqualityComparer.Default.Equals(attributeTypeSymbol, attributeData.AttributeClass))
                    {
                        generateSerializerAttributeData = attributeData;
                        break;
                    }
                }
            }

            if (generateSerializerAttributeData is not null)
            {
                if (symbol is not null)
                {
                    // Regular type
                    var supportsPrimaryContstructorParameters = ShouldSupportPrimaryConstructorParameters(symbol, generateSerializerAttributeData);
                    return (new SerializableTypeDescription(context.SemanticModel, symbol, supportsPrimaryContstructorParameters, GetDataMembers(symbol, libraryTypes), libraryTypes), libraryTypes);
                }
            }
        }

        return default;
    }

    static bool ShouldSupportPrimaryConstructorParameters(INamedTypeSymbol type, AttributeData attributeData)
    {
        if (type.IsRecord)
        {
            foreach (var namedArgument in attributeData.NamedArguments)
            {
                if (namedArgument.Key == "IncludePrimaryConstructorParameters")
                {
                    if (namedArgument.Value.Kind == TypedConstantKind.Primitive && namedArgument.Value.Value is bool b && b == false)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        return false;
    }

    // Returns descriptions of all data members (fields and properties) 
    IEnumerable<IMemberDescription> GetDataMembers(INamedTypeSymbol symbol, LibraryTypes libraryTypes)
    {
        var members = new Dictionary<(ushort, bool), IMemberDescription>();
        var hasAttributes = false;
        foreach (var member in symbol.GetMembers())
        {
            if (member.IsStatic || member.IsAbstract)
            {
                continue;
            }

            if (member.HasAttribute(libraryTypes.NonSerializedAttribute))
            {
                continue;
            }

            if (libraryTypes.IdAttributeTypes.Any(t => member.HasAttribute(t)))
            {
                hasAttributes = true;
                break;
            }
        }

        var nextFieldId = (ushort)0;

        ImmutableArray<IParameterSymbol> primaryConstructorParameters = ImmutableArray<IParameterSymbol>.Empty;
        if (symbol.IsRecord)
        {
            // If there is a primary constructor then that will be declared before the copy constructor
            // A record always generates a copy constructor and marks it as implicitly declared
            // todo: find an alternative to this magic
            var potentialPrimaryConstructor = symbol.Constructors[0];
            if (!potentialPrimaryConstructor.IsImplicitlyDeclared)
            {
                primaryConstructorParameters = potentialPrimaryConstructor.Parameters;
            }
        }

        foreach (var member in symbol.GetMembers().OrderBy(m => m.MetadataName))
        {
            if (member.IsStatic || member.IsAbstract)
            {
                continue;
            }

            // Only consider fields and properties.
            if (!(member is IFieldSymbol || member is IPropertySymbol))
            {
                continue;
            }

            if (member.HasAttribute(libraryTypes.NonSerializedAttribute))
            {
                continue;
            }

            if (member is IPropertySymbol prop)
            {
                var id = GetId(libraryTypes, prop);

                if (!id.HasValue)
                {
                    if (hasAttributes || !_options.GenerateFieldIds)
                    {
                        continue;
                    }

                    id = ++nextFieldId;
                }

                // FieldDescription takes precedence over PropertyDescription
                if (!members.TryGetValue((id.Value, false), out var existing))
                {
                    members[(id.Value, false)] = new PropertyDescription(id.Value, prop);
                }
            }

            if (member is IFieldSymbol field)
            {
                var id = GetId(libraryTypes, field);
                var isPrimaryConstructorParameter = false;

                if (!id.HasValue)
                {
                    prop = PropertyUtility.GetMatchingProperty(field);

                    if (prop is null)
                    {
                        continue;
                    }

                    if (prop.HasAttribute(libraryTypes.NonSerializedAttribute))
                    {
                        continue;
                    }

                    id = GetId(libraryTypes, prop);

                    if (!id.HasValue)
                    {
                        var primaryConstructorParameter = primaryConstructorParameters.FirstOrDefault(x => x.Name == prop.Name);
                        if (primaryConstructorParameter is not null)
                        {
                            id = (ushort)primaryConstructorParameters.IndexOf(primaryConstructorParameter);
                            isPrimaryConstructorParameter = true;
                        }
                    }
                }

                if (!id.HasValue)
                {
                    if (hasAttributes || !_options.GenerateFieldIds)
                    {
                        continue;
                    }

                    id = nextFieldId++;
                }

                // FieldDescription takes precedence over PropertyDescription
                if (!members.TryGetValue((id.Value, isPrimaryConstructorParameter), out var existing) || existing is PropertyDescription)
                {
                    members[(id.Value, isPrimaryConstructorParameter)] = new FieldDescription(id.Value, isPrimaryConstructorParameter, field);
                    continue;
                }
            }
        }

        return members.Values;
    }

    static void EmitSerializerSourceFile(SourceProductionContext context, (ISerializableTypeDescription serializableTypeDescription, LibraryTypes libraryTypes) _)
    {
        var (serializableTypeDescription, libraryTypes) = _;

        var sb = new StringBuilder(1024);
        sb.AppendLine("using global::Orleans.Serialization.Codecs;");
        sb.AppendLine("using global::Orleans.Serialization.GeneratedCodeHelpers;");
        sb.AppendLine();

        sb.AppendLine(MetadataGenerator.GenerateMetadataProviderAttribute(serializableTypeDescription, libraryTypes).NormalizeWhitespace().ToFullString());
        sb.AppendLine();

        sb.AppendLine($"namespace {serializableTypeDescription.GeneratedNamespace};");
        sb.AppendLine();

        sb.AppendLine(SerializerGenerator.GenerateSerializer(libraryTypes, serializableTypeDescription).NormalizeWhitespace().ToFullString());
        sb.AppendLine();
        sb.AppendLine(CopierGenerator.GenerateCopier(libraryTypes, serializableTypeDescription).NormalizeWhitespace().ToFullString());

        if (serializableTypeDescription.IsEmptyConstructable || serializableTypeDescription.HasActivatorConstructor)
        {
            sb.AppendLine();
            sb.AppendLine(ActivatorGenerator.GenerateActivator(libraryTypes, serializableTypeDescription).NormalizeWhitespace().ToFullString());
        }

        sb.AppendLine();
        sb.AppendLine(MetadataGenerator.GenerateMetadata(serializableTypeDescription, libraryTypes).NormalizeWhitespace().ToFullString());

        context.AddSource($"{serializableTypeDescription.Name}.g.cs", sb.ToString());
    }
}
    
