using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NuvyntraLabs.NET.ObjectKit;

namespace NuvyntraLabs.NET.ObjectKit.Tests;

public class ObjectKitGeneratorTests
{
    [Theory]
    [InlineData("[Copyable] public partial record Box(string Name);", "OBK001")]
    [InlineData("[Copyable] public partial struct Box { }", "OBK001")]
    [InlineData("[Copyable] public class Box { }", "OBK002")]
    [InlineData("[Copyable] public abstract partial class Box { }", "OBK003")]
    [InlineData("[Copyable] public static partial class Box { }", "OBK003")]
    [InlineData("[Copyable] public partial class Box<T> { }", "OBK004")]
    [InlineData("public partial class Outer { [Copyable] public partial class Box { } }", "OBK005")]
    [InlineData("[Copyable] public partial class Box { public Box(int value) { } }", "OBK006")]
    [InlineData("[Copyable] public partial class Box { public int Age { private get; set; } }", "OBK007")]
    [InlineData("[Copyable] public partial class Box { public int Age { get; } }", "OBK008")]
    [InlineData("[Copyable] public partial class Box { public System.IO.Stream Data { get; set; } = null!; }", "OBK009")]
    public void Reports_each_diagnostic(string declaration, string id)
    {
        Run(declaration, out string generated, out IReadOnlyList<Diagnostic> diagnostics);

        Assert.Contains(diagnostics, diagnostic => diagnostic.Id == id && diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Equal("", generated);
    }

    [Fact]
    public void Emits_copy_for_supported_properties_and_skips_hidden_static_and_indexer_members()
    {
        const string source = """
            using NuvyntraLabs.NET.ObjectKit;
            public enum Level { Guest, Admin }
            public class Named
            {
                public string Name { get; set; } = "";
            }
            namespace Sample
            {
                [Copyable]
                public partial class Employee : Named
                {
                    public new string Name { get; set; } = "";
                    public int Age { get; init; }
                    public int? Score { get; set; }
                    public Level Level { get; set; }
                    public int @class { get; set; }
                    public static int Count { get; set; }
                    public int this[int index] { get => index; set { } }
                }
            }
            [Copyable]
            public partial class EmptyBox
            {
            }
            """;

        Run(source, out string generated, out IReadOnlyList<Diagnostic> diagnostics);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Contains("namespace Sample;", generated, StringComparison.Ordinal);
        Assert.Contains("@class = this.@class", generated, StringComparison.Ordinal);
        Assert.Equal(1, Count(generated, "Name = this.Name"));
        Assert.DoesNotContain("Count = this.Count", generated, StringComparison.Ordinal);
        Assert.Contains("partial class EmptyBox", generated, StringComparison.Ordinal);
        Assert.Contains("return true;\n        return true;", generated.Replace("\r\n", "\n"), StringComparison.Ordinal);
        Assert.DoesNotContain("namespace ;", generated, StringComparison.Ordinal);
    }

    private static void Run(string source, out string generated, out IReadOnlyList<Diagnostic> diagnostics)
    {
        string text = source.Contains("using NuvyntraLabs.NET.ObjectKit;", StringComparison.Ordinal)
            ? source
            : "using NuvyntraLabs.NET.ObjectKit;\n" + source;
        IEnumerable<MetadataReference> references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(CopyableAttribute).Assembly.Location));
        CSharpCompilation compilation = CSharpCompilation.Create(
            "ObjectKitProbe",
            [CSharpSyntaxTree.ParseText(text)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ObjectKitGenerator()).RunGenerators(compilation);
        GeneratorRunResult result = driver.GetRunResult().Results.Single();

        diagnostics = result.Diagnostics;
        generated = string.Concat(result.GeneratedSources.Select(source => source.SourceText.ToString()));
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
