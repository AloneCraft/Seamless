using Microsoft.CodeAnalysis;

namespace Test.SourceGenerator;

public sealed class DatabaseContainerGeneratorTests {
    [Fact,Trait("Category","Unit")]
    public void MarkerControlsGeneration() {
        {
            //マーカーと同じ名前空間にあるDatabaseContainerを生成対象にする
            const string source = """
                namespace Sample;
                internal static class DatabaseGenerator;
                public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer {
                    public global::Seamless.Database.Table<int> Items=>this.Table<int>();
                }
                """;
            var result=Run(source);
            Assert.Contains(result.GeneratedSources,static generated=>generated.HintName.Contains("Sample.Catalog.Container.g.cs",StringComparison.Ordinal));
        }
        {
            //マーカーがない名前空間には何も生成しない
            const string source = """
                namespace Sample;
                public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer {
                    public global::Seamless.Database.Table<int> Items=>this.Table<int>();
                }
                """;
            var result=Run(source);
            Assert.Empty(result.GeneratedSources);
            Assert.Empty(result.Diagnostics);
        }
        {
            //マーカーの子名前空間にあるDatabaseContainerは生成対象にしない
            const string source = """
                namespace Sample {
                    internal static class DatabaseGenerator;
                }
                namespace Sample.Child {
                    public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer {
                        public global::Seamless.Database.Table<int> Items=>this.Table<int>();
                    }
                }
                """;
            var result=Run(source);
            Assert.DoesNotContain(result.GeneratedSources,static generated=>generated.HintName.Contains("Catalog.Container.g.cs",StringComparison.Ordinal));
        }
    }

    [Fact,Trait("Category","Unit")]
    public void DatabaseObjectsGeneratedAsSystemTable() {
        // ビューは Views 名前空間の partial 行型の Query だけで宣言する。
        const string source = """
            namespace Sample{
            internal static partial class DatabaseGenerator;
            public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer {
                public global::Seamless.Database.Table<int> Items=>this.Table<int>();
                public global::Seamless.Database.TableFunction<int> Find(int minimum)=>this.TableFunction(()=>[]);
                public global::Seamless.Database.ScalarFunction<int> Total()=>this.ScalarFunction(static()=>0);
                public global::Seamless.Database.StoredProcedure<int> Rebuild()=>this.StoredProcedure(static()=>0);
                public int Count()=>0;
            }
            }
            namespace Sample.Views{
            public readonly partial record struct Names(string Name){
                public static global::System.Collections.Generic.IEnumerable<Names> Query(global::Sample.Catalog db)=>[];
            }
            }
            """;
        var result=Run(source);
        var generated=Assert.Single(result.GeneratedSources,static generated=>generated.HintName.Contains("Catalog.Container.g.cs",StringComparison.Ordinal)).SourceText.ToString();
        Assert.Contains("SystemTable",generated,StringComparison.Ordinal);
        Assert.Contains("DatabaseObjectKind.Table",generated,StringComparison.Ordinal);
        Assert.Contains("DatabaseObjectKind.View",generated,StringComparison.Ordinal);
        Assert.Contains("DatabaseObjectKind.TableFunction",generated,StringComparison.Ordinal);
        Assert.Contains("DatabaseObjectKind.ScalarFunction",generated,StringComparison.Ordinal);
        Assert.Contains("DatabaseObjectKind.StoredProcedure",generated,StringComparison.Ordinal);
        Assert.Contains("DatabaseObjectKind.SystemTable",generated,StringComparison.Ordinal);
        Assert.Contains("typeof(int)",generated,StringComparison.Ordinal);
        Assert.DoesNotContain("""new(new("Count")""",generated,StringComparison.Ordinal);
        Assert.DoesNotContain("SqlName",generated,StringComparison.Ordinal);
    }

    [Fact,Trait("Category","Unit")]
    public void DuplicateObjectNameReportsSdb001() {
        const string source = """
            namespace Sample;
            internal static class DatabaseGenerator;
            public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer {
                public global::Seamless.Database.TableFunction<int> Find(int minimum)=>this.TableFunction(()=>[]);
                public global::Seamless.Database.TableFunction<int> Find(string minimum)=>this.TableFunction(()=>[]);
            }
            """;
        var result=Run(source);
        Assert.Contains(result.Diagnostics,static diagnostic=>diagnostic.Id=="SDB001"&&diagnostic.Severity==DiagnosticSeverity.Error);
    }

    [Fact,Trait("Category","Unit")]
    public void ReservedDatabaseObjectsNameReportsSdb002() {
        const string source = """
            namespace Sample;
            internal static class DatabaseGenerator;
            public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer {
                public new global::Seamless.Database.Table<int> DatabaseObjects=>this.Table<int>();
            }
            """;
        var result=Run(source);
        Assert.Contains(result.Diagnostics,static diagnostic=>diagnostic.Id=="SDB002"&&diagnostic.Severity==DiagnosticSeverity.Error);
    }

    [Fact,Trait("Category","Unit")]
    public void SchemaGraphUsesQualifiedObjectNames() {
        const string source = """
            namespace Sample {
                internal static class DatabaseGenerator;
                public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer {
                    public Schemas.sales sales=>new(this);
                    public Schemas.inventory inventory=>new(this);
                }
            }
            namespace Sample.Schemas {
                public sealed class sales(global::Seamless.Database.DatabaseContainer database):global::Seamless.Database.DatabaseSchema(database,"sales") {
                    public global::Seamless.Database.Table<int> Product=>this.Table<int>();
                }
                public sealed class inventory(global::Seamless.Database.DatabaseContainer database):global::Seamless.Database.DatabaseSchema(database,"inventory") {
                    public global::Seamless.Database.Table<int> Product=>this.Table<int>();
                }
            }
            """;

        var result=Run(source);
        var generated=Assert.Single(result.GeneratedSources,static generated=>generated.HintName.Contains("Catalog.Container.g.cs",StringComparison.Ordinal)).SourceText.ToString();
        Assert.Contains("""new("sales.Product")""",generated,StringComparison.Ordinal);
        Assert.Contains("""new("inventory.Product")""",generated,StringComparison.Ordinal);
        Assert.Contains("this.sales.Product.Add(row)",generated,StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics,static diagnostic=>diagnostic.Id=="SDB001");
    }

    [Fact,Trait("Category","Unit")]
    public void SchemaRecordUsesArrayMemoryPackApis() {
        const string source = """
            namespace Sample {
                internal static class DatabaseGenerator;
            }
            namespace Sample.Records {
                public partial struct ImageRow {
                    public partial byte[]? Data { get; }
                }
            }
            """;
        var compilation=GeneratorTestHelpers.CreateCompilation([("Database.cs",source)]);
        GeneratorDriver driver=GeneratorTestHelpers.CreateDatabaseDriver();
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var outputCompilation,out var diagnostics);
        var result=driver.GetRunResult().Results.Single(static candidate=>
            candidate.GeneratedSources.Any(static generated=>generated.HintName=="Seamless.Database.Schema.g.cs"));
        var generated=Assert.Single(result.GeneratedSources).SourceText.ToString();

        Assert.Contains("writer.WriteArray(value._value0)",generated,StringComparison.Ordinal);
        Assert.Contains("reader.ReadArray(ref value._value0)",generated,StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics,static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error);
        Assert.DoesNotContain(outputCompilation.GetDiagnostics(),static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error);
    }

    [Fact,Trait("Category","Unit")]
    public void IncrementalSyntaxAddition_GeneratesContainerPropertiesWithoutDiskSave(){
        const string initialSource="""
            namespace Sample{
                internal static class DatabaseGenerator;
                public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer;
            }
            """;
        var compilation=GeneratorTestHelpers.CreateCompilation([("Database.cs",initialSource)]);
        GeneratorDriver driver=GeneratorTestHelpers.CreateDatabaseDriver();
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var initialOutput,out _);

        // 未ビルド・未保存の状態でエディタ入力された構文木をメモリ上で追加
        const string ordersSource="""
            namespace Sample.Tables{
                public partial struct Orders{
                    [global::Seamless.Database.Key]
                    public partial int Id{get;init;}
                    public partial decimal Amount{get;init;}
                }
            }
            """;
        var ordersTree=Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(ordersSource,GeneratorTestHelpers.ParseOptions,path:"Orders.cs");
        var updatedCompilation=compilation.AddSyntaxTrees(ordersTree);

        driver.RunGeneratorsAndUpdateCompilation(updatedCompilation,out var finalOutput,out var diagnostics);
        Assert.DoesNotContain(diagnostics,static d=>d.Severity==DiagnosticSeverity.Error);
        Assert.DoesNotContain(finalOutput.GetDiagnostics(),static d=>d.Severity==DiagnosticSeverity.Error);

        var catalogType=finalOutput.GetTypeByMetadataName("Sample.Catalog")!;
        Assert.NotNull(catalogType);
        var ordersProperty=catalogType.GetMembers("Orders").OfType<IPropertySymbol>().FirstOrDefault();
        Assert.NotNull(ordersProperty);
        Assert.Equal("Orders",ordersProperty.Name);
        Assert.Contains("Table",ordersProperty.Type.ToDisplayString());
    }

    [Fact,Trait("Category","Unit")]
    public void TableAndViewModifications_IncrementallyUpdateSymbols(){
        const string baseSource="""
            namespace Sample{
                internal static class DatabaseGenerator;
                public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer;
            }
            namespace Sample.Tables{
                public partial struct Items{
                    [global::Seamless.Database.Key]
                    public partial int Id{get;init;}
                }
            }
            """;
        var compilation=GeneratorTestHelpers.CreateCompilation([("Database.cs",baseSource)]);
        GeneratorDriver driver=GeneratorTestHelpers.CreateDatabaseDriver();
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var baseOutput,out _);

        var initialItemType=baseOutput.GetTypeByMetadataName("Sample.Tables.Items")!;
        Assert.Null(initialItemType.GetMembers("Price").FirstOrDefault());

        // 列を追加した構文木に差し替え
        const string modifiedSource="""
            namespace Sample{
                internal static class DatabaseGenerator;
                public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer;
            }
            namespace Sample.Tables{
                public partial struct Items{
                    [global::Seamless.Database.Key]
                    public partial int Id{get;init;}
                    public partial decimal Price{get;init;}
                }
            }
            """;
        var modifiedTree=Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(modifiedSource,GeneratorTestHelpers.ParseOptions,path:"Database.cs");
        var updatedCompilation=compilation.ReplaceSyntaxTree(compilation.SyntaxTrees[0],modifiedTree);

        driver.RunGeneratorsAndUpdateCompilation(updatedCompilation,out var modifiedOutput,out var diagnostics);
        Assert.DoesNotContain(diagnostics,static d=>d.Severity==DiagnosticSeverity.Error);
        Assert.DoesNotContain(modifiedOutput.GetDiagnostics(),static d=>d.Severity==DiagnosticSeverity.Error);

        var updatedItemType=modifiedOutput.GetTypeByMetadataName("Sample.Tables.Items")!;
        var priceProp=updatedItemType.GetMembers("Price").OfType<IPropertySymbol>().FirstOrDefault();
        Assert.NotNull(priceProp);
        Assert.Equal("decimal",priceProp.Type.ToDisplayString());
    }

    [Fact,Trait("Category","Unit")]
    public void IncompleteSyntaxDuringTyping_MaintainsExistingSymbolsWithoutCrash(){
        const string validSource="""
            namespace Sample{
                internal static class DatabaseGenerator;
                public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer;
            }
            namespace Sample.Tables{
                public partial struct Items{
                    [global::Seamless.Database.Key]
                    public partial int Id{get;init;}
                }
            }
            """;
        var compilation=GeneratorTestHelpers.CreateCompilation([("Database.cs",validSource)]);
        GeneratorDriver driver=GeneratorTestHelpers.CreateDatabaseDriver();
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out _,out _);

        // 入力途中の不完全な構文（波括弧が閉じられていない等の文法エラー構文木）を追加
        const string typingIncompleteSource="""
            namespace Sample.Tables{
                public partial struct DraftTyping{
                    [global::Seamless.Database.Key]
                    public int Incomplete
            """;
        var typingTree=Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(typingIncompleteSource,GeneratorTestHelpers.ParseOptions,path:"DraftTyping.cs");
        var updatedCompilation=compilation.AddSyntaxTrees(typingTree);

        // 生成器自体が例外スロー（Crash）しないことを検証（不完全入力に対する診断があっても生成器は停止しない）
        var runResult=driver.RunGenerators(updatedCompilation).GetRunResult();
        Assert.DoesNotContain(runResult.Diagnostics,static d=>d.Id=="RS1024"||d.Descriptor.Category=="GeneratorCrash");

        driver.RunGeneratorsAndUpdateCompilation(updatedCompilation,out var typingOutput,out _);

        // 既存の正常なテーブルのシンボルが維持されていることを検証（IDE補完が継続動作する）
        var catalogType=typingOutput.GetTypeByMetadataName("Sample.Catalog")!;
        Assert.NotNull(catalogType);
        var itemsProperty=catalogType.GetMembers("Items").OfType<IPropertySymbol>().FirstOrDefault();
        Assert.NotNull(itemsProperty);
    }

    private static GeneratorRunResult Run(string source) {
        var compilation=GeneratorTestHelpers.CreateCompilation([("Database.cs",source)]);
        var driver=GeneratorTestHelpers.CreateDatabaseDriver().RunGenerators(compilation);
        var results=driver.GetRunResult().Results;
        Assert.Equal(2,results.Length);
        return results[0];
    }
}
