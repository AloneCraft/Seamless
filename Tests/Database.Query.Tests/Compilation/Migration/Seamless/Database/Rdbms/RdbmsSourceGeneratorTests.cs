using Microsoft.CodeAnalysis;

namespace Test.Seamless.Database.Rdbms;

// 取込は表を Tables.<スキーマ> に [Key] 付きで書き、キーの型とスキーマの表のプロパティは DB の生成器が作る。
// 設計：Document/refactoring-designs/20261114-宣言と手続きと索引の統一.md の手順B(旧方式の Keys と Records の二重書きの削除)。
public sealed class RdbmsSourceGeneratorTests {
    [Fact,Trait("Category","Unit")]
    public void Generate_CreatesSchemaAndDeclaredTableSources() {
        var catalog = new global::Seamless.Database.Rdbms.DatabaseCatalog(
            "TinyStore",
            [new("sales",[new("Product",[
                new(0,"Product Id",new(global::Seamless.Database.Rdbms.DatabaseTypeKind.Int32,false),false),
                new(1,"Name",new(global::Seamless.Database.Rdbms.DatabaseTypeKind.String,false),false),
            ],new("PK_Product",[new("Product Id",false)]))])]);

        var result = global::Seamless.Database.Rdbms.CSharpDatabaseGenerator.Generate(catalog);

        Assert.Equal(2,result.Files.Count);
        Assert.Equal(64,result.SchemaFingerprint.Length);
        {
            //コンテナとスキーマ。スキーマのクラスは、DB の生成器が表のプロパティを足すので partial
            var database = Assert.Single(result.Files,static file=>file.RelativePath=="TinyStore.cs").Content;
            Assert.Contains("public global::TinyStore.Schemas.sales sales { get; }",database,StringComparison.Ordinal);
            Assert.Contains("this.sales=new(this);",database,StringComparison.Ordinal);
            Assert.Contains("public sealed partial class sales(",database,StringComparison.Ordinal);
            Assert.DoesNotContain("Records",database,StringComparison.Ordinal);
            Assert.DoesNotContain("Keys",database,StringComparison.Ordinal);
        }
        {
            //表の行。キーの列に [Key]、取込の実名は型に付ける
            var table=Assert.Single(result.Files,static file=>file.RelativePath=="Tables/sales/Product.cs").Content;
            Assert.Contains("namespace TinyStore.Tables.sales;",table,StringComparison.Ordinal);
            Assert.Contains("[global::Seamless.Database.ImportedDatabaseObject(\"sales\",\"Product\"",table,StringComparison.Ordinal);
            Assert.Matches(@"\[global::Seamless\.Database\.Key\]\s*\[global::Seamless\.Database\.ImportedDatabaseColumn\(""Product Id""",table);
        }
        {
            //DB の生成器と一緒にコンパイルすると、スキーマに表のプロパティができ、取込の実名の属性が写る
            var output=Compile(result);
            var schema=output.GetTypeByMetadataName("TinyStore.Schemas.sales")!;
            var property=schema.GetMembers("Product").OfType<IPropertySymbol>().Single();
            Assert.Equal("global::Seamless.Database.Table<global::TinyStore.Keys.sales.Product, global::TinyStore.Tables.sales.Product>",property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            var imported=property.GetAttributes().Single(static attribute=>attribute.AttributeClass?.Name=="ImportedDatabaseObjectAttribute");
            Assert.Equal(["sales","Product"],imported.ConstructorArguments.Take(2).Select(static argument=>(string)argument.Value!));
        }
    }

    [Fact,Trait("Category","Unit")]
    public void Generate_ReservesEveryContainingTypeName() {
        var catalog=new global::Seamless.Database.Rdbms.DatabaseCatalog(
            "Person",
            [new("Person",[new("Person",[
                new(0,"Person",new(global::Seamless.Database.Rdbms.DatabaseTypeKind.Int32,false),false),
            ],new("PK_Person",[new("Person",false)]))])]);

        var result=global::Seamless.Database.Rdbms.CSharpDatabaseGenerator.Generate(catalog);
        var database=Assert.Single(result.Files,static file=>file.RelativePath=="Person.cs").Content;
        var record=Assert.Single(result.Files,static file=>file.RelativePath.StartsWith("Tables/",StringComparison.Ordinal)).Content;

        Assert.DoesNotContain("class Person(",database,StringComparison.Ordinal);
        Assert.DoesNotContain(" int Person { get; }",record,StringComparison.Ordinal);
        Assert.All(result.Tables,static table=>Assert.NotEqual("Person.Person",table.ObjectName));
        _=Compile(result);
    }

    [Fact,Trait("Category","Unit")]
    public void Generate_RenamesColumnsThatCollideWithDeclaredMembers(){
        // 新方式の行の生成器が作るメンバー(Key・Create など)と同じ名前の列は、名前を付け替える(重なると宣言のエラーになる)。
        var catalog=new global::Seamless.Database.Rdbms.DatabaseCatalog(
            "TinyStore",
            [new("dbo",[new("Setting",[
                new(0,"Key",new(global::Seamless.Database.Rdbms.DatabaseTypeKind.Int32,false),false),
                new(1,"Create",new(global::Seamless.Database.Rdbms.DatabaseTypeKind.String,false),false),
            ],new("PK_Setting",[new("Key",false)]))])]);

        var result=global::Seamless.Database.Rdbms.CSharpDatabaseGenerator.Generate(catalog);
        var binding=Assert.Single(result.Tables);
        Assert.DoesNotContain(binding.Columns,static column=>column.PropertyName is "Key" or "Create");
        Assert.Equal(["Key","Create"],binding.Columns.Select(static column=>column.SourceName));
        _=Compile(result);
    }

    internal static Compilation Compile(global::Seamless.Database.Rdbms.GeneratedDatabase result){
        var compilation=global::Test.SourceGenerator.GeneratorTestHelpers.CreateCompilation(result.Files.Select(static file=>(file.RelativePath,file.Content)));
        global::Test.SourceGenerator.GeneratorTestHelpers.CreateDatabaseDriver().RunGeneratorsAndUpdateCompilation(compilation,out var output,out var diagnostics);
        Assert.DoesNotContain(diagnostics,static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error);
        var errors=output.GetDiagnostics().Where(static diagnostic=>diagnostic.Severity==DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length==0,string.Join(Environment.NewLine,errors.Select(static diagnostic=>diagnostic.ToString())));
        return output;
    }
}
