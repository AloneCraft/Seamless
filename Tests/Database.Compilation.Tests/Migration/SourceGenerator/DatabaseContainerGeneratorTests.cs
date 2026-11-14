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
        // ビュー・関数は、生成される入口(Catalog.IReader)への拡張メンバーとして書く。ビューはプロパティ、関数はメソッド。
        const string source = """
            namespace Sample{
            internal static partial class DatabaseGenerator;
            public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer {
                public global::Seamless.Database.Table<int> Items=>this.Table<int>();
                public global::Seamless.Database.StoredProcedure<int> Rebuild()=>this.StoredProcedure(static()=>0);
                public int Count()=>0;
            }
            public static class CatalogQueries{
                extension(Catalog.IReader q){
                    public global::Seamless.Collections.ISet<Views.Names> Names=>throw new global::System.NotImplementedException();
                    public global::Seamless.Collections.ISet<int> Find(int minimum)=>throw new global::System.NotImplementedException();
                    public int Total()=>0;
                    public int Helper=>0;
                }
            }
            }
            namespace Sample.Views{
            public readonly record struct Names(string Name);
            }
            """;
        var result=Run(source);
        var generated=Assert.Single(result.GeneratedSources,static generated=>generated.HintName.Contains("Catalog.Container.g.cs",StringComparison.Ordinal)).SourceText.ToString();
        Assert.Contains("SystemTable",generated,StringComparison.Ordinal);
        Assert.Contains("DatabaseObjectKind.Table",generated,StringComparison.Ordinal);
        Assert.Contains("""new(new("Names"),global::Seamless.Database.System.DatabaseObjectKind.View,typeof(global::Sample.Views.Names))""",generated,StringComparison.Ordinal);
        Assert.Contains("""new(new("Find"),global::Seamless.Database.System.DatabaseObjectKind.TableFunction,typeof(int))""",generated,StringComparison.Ordinal);
        Assert.Contains("""new(new("Total"),global::Seamless.Database.System.DatabaseObjectKind.ScalarFunction,typeof(int))""",generated,StringComparison.Ordinal);
        Assert.Contains("DatabaseObjectKind.StoredProcedure",generated,StringComparison.Ordinal);
        Assert.Contains("DatabaseObjectKind.SystemTable",generated,StringComparison.Ordinal);
        // ISet を返さないプロパティ(Helper)と、コンテナの普通のメソッド(Count)は目録に載らない。
        Assert.DoesNotContain("""new(new("Count")""",generated,StringComparison.Ordinal);
        Assert.DoesNotContain("""new(new("Helper")""",generated,StringComparison.Ordinal);
        Assert.DoesNotContain("SqlName",generated,StringComparison.Ordinal);
        // 名前で呼ぶ入口(Manager・Gateway 用)を、拡張の名前空間の補助クラスから作る。
        var queries=Assert.Single(result.GeneratedSources,static generated=>generated.HintName.Contains("Catalog.Queries.g.cs",StringComparison.Ordinal)).SourceText.ToString();
        Assert.Contains("reader.Names",queries,StringComparison.Ordinal);
        Assert.Contains("reader.Find((int)arguments[0]!)",queries,StringComparison.Ordinal);
        Assert.Contains("public override global::System.Collections.Generic.IReadOnlyList<global::Seamless.Database.DatabaseQuery> Queries",queries,StringComparison.Ordinal);
        Assert.Empty(result.Diagnostics.Where(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error));
    }

    [Fact,Trait("Category","Unit")]
    public void DuplicateObjectNameReportsSdb001() {
        const string source = """
            namespace Sample;
            internal static class DatabaseGenerator;
            public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer;
            public static class CatalogQueries{
                extension(Catalog.IReader q){
                    public global::Seamless.Collections.ISet<int> Find(int minimum)=>throw new global::System.NotImplementedException();
                    public global::Seamless.Collections.ISet<int> Find(string minimum)=>throw new global::System.NotImplementedException();
                }
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
    public void ReadersReachTheRootThroughDatabase(){
        // 設計：Document/refactoring-designs/20261114-宣言と手続きと索引の統一.md の手順A。
        // すべての入口(ルート・スキーマ、読む側・書く側)が、ルートの入口を返す Database を持つ。スキーマのビューから他のスキーマの表を読める。
        const string source="""
            using System.Linq;using Seamless.Linq;
            namespace Sample{
                internal static class DatabaseGenerator;
                public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer;
                public static class CatalogQueries{
                    extension(Catalog.IReader q){
                        public global::Seamless.Collections.ISet<Views.Name> Names=>q.Database.Item.Select(static item=>new Views.Name(item.Id));
                    }
                    extension(Catalog.IPersonReader q){
                        public global::Seamless.Collections.ISet<Views.Person.Located> Located=>
                            q.Database.Person.Address.Join(q.Database.Employee.Job,static address=>address.JobId,static job=>job.Id,static(address,job)=>new Views.Person.Located(address.Id,job.Title));
                    }
                }
            }
            namespace Sample.Tables{public partial struct Item{[global::Seamless.Database.Key]public partial int Id{get;init;}}}
            namespace Sample.Tables.Person{public partial struct Address{[global::Seamless.Database.Key]public partial int Id{get;init;}public partial int JobId{get;init;}}}
            namespace Sample.Tables.Employee{public partial struct Job{[global::Seamless.Database.Key]public partial int Id{get;init;}public partial string Title{get;init;}}}
            namespace Sample.Views{public readonly record struct Name(int Id);}
            namespace Sample.Views.Person{public readonly record struct Located(int Id,string Title);}
            """;
        var output=Compile(source);
        var catalog=output.GetTypeByMetadataName("Sample.Catalog")!;
        INamedTypeSymbol Nested(string name)=>catalog.GetTypeMembers(name).Single();
        IPropertySymbol Property(INamedTypeSymbol type,string name)=>type.GetMembers(name).OfType<IPropertySymbol>().Single(static property=>property.ExplicitInterfaceImplementations.IsEmpty);
        var root=Nested("IReader");
        {
            //入口のインターフェースは、ルートでもスキーマでもルートの入口を返す
            Assert.Equal(root,Property(root,"Database").Type,SymbolEqualityComparer.Default);
            Assert.Equal(root,Property(Nested("IPersonReader"),"Database").Type,SymbolEqualityComparer.Default);
            Assert.Equal(root,Property(Nested("IEmployeeReader"),"Database").Type,SymbolEqualityComparer.Default);
        }
        {
            //読む側の実体は、スキーマの中からでも具体的な Reader を返す(FLinq が表の具体的な型を認識できるように)
            var reader=Nested("Reader");
            Assert.Equal(reader,Property(reader,"Database").Type,SymbolEqualityComparer.Default);
            var person=(INamedTypeSymbol)Property(reader,"Person").Type;
            Assert.Equal(reader,Property(person,"Database").Type,SymbolEqualityComparer.Default);
        }
        {
            //書く側の実体は、スキーマの中からでも具体的な TypedTransaction を返す(書ける表に届く)
            var transaction=Nested("TypedTransaction");
            Assert.Equal(transaction,Property(transaction,"Database").Type,SymbolEqualityComparer.Default);
            var person=(INamedTypeSymbol)Property(transaction,"Person").Type;
            Assert.Equal(transaction,Property(person,"Database").Type,SymbolEqualityComparer.Default);
            Assert.Equal("TransactionCollection",((INamedTypeSymbol)Property((INamedTypeSymbol)Property(transaction,"Employee").Type,"Job").Type).Name);
        }
    }

    [Fact,Trait("Category","Unit")]
    public void DatabaseNameIsReservedForTablesSchemasAndQueries(){
        static string Source(string body)=>"""
            namespace Sample{
                internal static class DatabaseGenerator;
                public sealed partial class Catalog:global::Seamless.Database.DatabaseContainer;
            }
            """+body;
        {
            //ルートの表
            var result=Run(Source("namespace Sample.Tables{public partial struct Database{[global::Seamless.Database.Key]public partial int Id{get;init;}}}"));
            Assert.Contains(result.Diagnostics,static diagnostic=>diagnostic.Id=="SDB002"&&diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("'Database'",StringComparison.Ordinal));
        }
        {
            //スキーマの表
            var result=Run(Source("namespace Sample.Tables.Sales{public partial struct Database{[global::Seamless.Database.Key]public partial int Id{get;init;}}}"));
            Assert.Contains(result.Diagnostics,static diagnostic=>diagnostic.Id=="SDB002"&&diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("'Sales.Database'",StringComparison.Ordinal));
        }
        {
            //スキーマ
            var result=Run(Source("namespace Sample.Tables.Database{public partial struct Item{[global::Seamless.Database.Key]public partial int Id{get;init;}}}"));
            Assert.Contains(result.Diagnostics,static diagnostic=>diagnostic.Id=="SDB002"&&diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("'Database.Item'",StringComparison.Ordinal));
        }
        {
            //ビュー
            var result=Run(Source("""
                namespace Sample{
                    public static class CatalogQueries{
                        extension(Catalog.IReader q){public global::Seamless.Collections.ISet<int> Database=>throw new global::System.NotImplementedException();}
                    }
                }
                """));
            Assert.Contains(result.Diagnostics,static diagnostic=>diagnostic.Id=="SDB002"&&diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("'Database'",StringComparison.Ordinal));
        }
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

    private static Compilation Compile(string source){
        var compilation=GeneratorTestHelpers.CreateCompilation([("Database.cs",source)]);
        GeneratorTestHelpers.CreateDatabaseDriver().RunGeneratorsAndUpdateCompilation(compilation,out var output,out var diagnostics);
        Assert.DoesNotContain(diagnostics,static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error);
        var errors=output.GetDiagnostics().Where(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length==0,string.Join(Environment.NewLine,errors.Select(static diagnostic=>diagnostic.ToString())));
        return output;
    }

    private static GeneratorRunResult Run(string source) {
        var compilation=GeneratorTestHelpers.CreateCompilation([("Database.cs",source)]);
        var driver=GeneratorTestHelpers.CreateDatabaseDriver().RunGenerators(compilation);
        var results=driver.GetRunResult().Results;
        Assert.Equal(2,results.Length);
        return results[0];
    }
}
