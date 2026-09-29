using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NuvyntraLabs.NET.ObjectKit;

/// <summary>Generates Copy, Equals, and GetHashCode for types marked with CopyableAttribute.</summary>
[Generator]
public sealed class ObjectKitGenerator : IIncrementalGenerator
{
    private const string AttributeName = "NuvyntraLabs.NET.ObjectKit.CopyableAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<Generation> generations = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (context, _) => Create((TypeDeclarationSyntax)context.TargetNode, context.SemanticModel));

        context.RegisterSourceOutput(generations, static (production, generation) =>
        {
            foreach (Diagnostic diagnostic in generation.Diagnostics)
            {
                production.ReportDiagnostic(diagnostic);
            }

            if (generation.Source is not null && generation.Hint is not null)
            {
                production.AddSource(generation.Hint, SourceText.From(generation.Source, Encoding.UTF8));
            }
        });
    }

    private static Generation Create(TypeDeclarationSyntax syntax, SemanticModel model)
    {
        if (model.GetDeclaredSymbol(syntax) is not INamedTypeSymbol type)
        {
            return Generation.Empty;
        }

        Location location = syntax.Identifier.GetLocation();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        if (syntax is not ClassDeclarationSyntax classSyntax || type.TypeKind != TypeKind.Class)
        {
            diagnostics.Add(Diagnostic.Create(Descriptors.NotAClass, location, type.Name));
            return new Generation(null, null, diagnostics.ToImmutable());
        }

        if (!classSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            diagnostics.Add(Diagnostic.Create(Descriptors.NotPartial, location, type.Name));
        }

        if (type.IsAbstract || type.IsStatic)
        {
            diagnostics.Add(Diagnostic.Create(Descriptors.NotConstructable, location, type.Name));
        }

        if (type.IsGenericType)
        {
            diagnostics.Add(Diagnostic.Create(Descriptors.Generic, location, type.Name));
        }

        if (type.ContainingType is not null)
        {
            diagnostics.Add(Diagnostic.Create(Descriptors.Nested, location, type.Name));
        }

        bool hasConstructor = type.InstanceConstructors.Any(constructor =>
            constructor.Parameters.Length == 0 && constructor.DeclaredAccessibility == Accessibility.Public);
        if (!hasConstructor)
        {
            diagnostics.Add(Diagnostic.Create(Descriptors.NoConstructor, location, type.Name));
        }

        var properties = new List<IPropertySymbol>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (INamedTypeSymbol? current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (IPropertySymbol property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.IsStatic || property.IsIndexer || property.DeclaredAccessibility != Accessibility.Public || !seen.Add(property.Name))
                {
                    continue;
                }

                if (property.GetMethod is not { DeclaredAccessibility: Accessibility.Public })
                {
                    diagnostics.Add(Diagnostic.Create(Descriptors.Unreadable, property.Locations.FirstOrDefault(), property.Name));
                    continue;
                }

                if (property.SetMethod is not { DeclaredAccessibility: Accessibility.Public })
                {
                    diagnostics.Add(Diagnostic.Create(Descriptors.NoSetter, property.Locations.FirstOrDefault(), property.Name));
                    continue;
                }

                if (!IsCopyable(property.Type))
                {
                    diagnostics.Add(Diagnostic.Create(Descriptors.UnsupportedType, property.Locations.FirstOrDefault(), property.Name, property.Type.ToDisplayString()));
                    continue;
                }

                properties.Add(property);
            }
        }

        if (diagnostics.Count > 0)
        {
            return new Generation(null, null, diagnostics.ToImmutable());
        }

        string hint = "ObjectKit." + type.ToDisplayString().Replace('.', '_').Replace('+', '_') + ".g.cs";
        return new Generation(hint, Emit(type, properties), diagnostics.ToImmutable());
    }

    private static bool IsCopyable(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { IsGenericType: true } named
            && named.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
        {
            return true;
        }

        return type.SpecialType == SpecialType.System_String
            || type.TypeKind == TypeKind.Enum
            || type.IsValueType;
    }

    private static string Emit(INamedTypeSymbol type, List<IPropertySymbol> properties)
    {
        string display = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("#nullable enable");
        string ns = type.ContainingNamespace.IsGlobalNamespace
            ? ""
            : type.ContainingNamespace.ToDisplayString();
        if (ns.Length > 0)
        {
            builder.Append("namespace ").Append(ns).AppendLine(";");
            builder.AppendLine();
        }

        builder.Append("partial class ").Append(display).AppendLine(" : global::System.IEquatable<").Append(display).AppendLine(">");
        builder.AppendLine("{");
        builder.Append("    public ").Append(display).AppendLine(" Copy()");
        builder.AppendLine("    {");
        builder.Append("        return new ").Append(display).AppendLine();
        builder.AppendLine("        {");
        foreach (IPropertySymbol property in properties)
        {
            string name = Identifier(property.Name);
            builder.Append("            ").Append(name).Append(" = this.").Append(name).AppendLine(",");
        }

        builder.AppendLine("        };");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.Append("    public bool Equals(").Append(display).AppendLine("? other)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (other is null) return false;");
        builder.AppendLine("        if (global::System.Object.ReferenceEquals(this, other)) return true;");
        if (properties.Count == 0)
        {
            builder.AppendLine("        return true;");
        }
        else
        {
            builder.Append("        return ");
            for (int index = 0; index < properties.Count; index++)
            {
                if (index > 0)
                {
                    builder.AppendLine();
                    builder.Append("            && ");
                }

                string name = Identifier(properties[index].Name);
                string typeName = properties[index].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                builder.Append("global::System.Collections.Generic.EqualityComparer<").Append(typeName).Append(">.Default.Equals(this.").Append(name).Append(", other.").Append(name).Append(')');
            }

            builder.AppendLine(";");
        }

        builder.AppendLine("    }");
        builder.AppendLine();
        builder.Append("    public override bool Equals(object? obj) => obj is ").Append(display).AppendLine(" other && Equals(other);");
        builder.AppendLine();
        builder.AppendLine("    public override int GetHashCode()");
        builder.AppendLine("    {");
        builder.AppendLine("        var hash = new global::System.HashCode();");
        foreach (IPropertySymbol property in properties)
        {
            builder.Append("        hash.Add(this.").Append(Identifier(property.Name)).AppendLine(");");
        }

        builder.AppendLine("        return hash.ToHashCode();");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string Identifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

    private sealed class Generation
    {
        public Generation(string? hint, string? source, ImmutableArray<Diagnostic> diagnostics)
        {
            Hint = hint;
            Source = source;
            Diagnostics = diagnostics;
        }

        public static Generation Empty { get; } = new(null, null, ImmutableArray<Diagnostic>.Empty);

        public string? Hint { get; }

        public string? Source { get; }

        public ImmutableArray<Diagnostic> Diagnostics { get; }
    }
}

internal static class Descriptors
{
    public static readonly DiagnosticDescriptor NotAClass = Error("OBK001", "ObjectKit copies classes", "'{0}' must be a class.");
    public static readonly DiagnosticDescriptor NotPartial = Error("OBK002", "ObjectKit type must be partial", "'{0}' must be partial.");
    public static readonly DiagnosticDescriptor NotConstructable = Error("OBK003", "ObjectKit type must be constructable", "'{0}' must be a concrete class.");
    public static readonly DiagnosticDescriptor Generic = Error("OBK004", "ObjectKit type must be non-generic", "'{0}' must not be generic.");
    public static readonly DiagnosticDescriptor Nested = Error("OBK005", "ObjectKit type must be top-level", "'{0}' must not be nested.");
    public static readonly DiagnosticDescriptor NoConstructor = Error("OBK006", "ObjectKit type needs a public constructor", "'{0}' needs a public parameterless constructor.");
    public static readonly DiagnosticDescriptor Unreadable = Error("OBK007", "ObjectKit cannot read a property", "Property '{0}' needs a public getter.");
    public static readonly DiagnosticDescriptor NoSetter = Error("OBK008", "ObjectKit cannot assign a property", "Property '{0}' needs a public set or init accessor.");
    public static readonly DiagnosticDescriptor UnsupportedType = Error("OBK009", "ObjectKit cannot copy this property type", "Property '{0}' has type '{1}', which ObjectKit cannot copy. Use a string, enum, or value type.");

    private static DiagnosticDescriptor Error(string id, string title, string message) =>
        new(id, title, message, "ObjectKit", DiagnosticSeverity.Error, isEnabledByDefault: true);
}
