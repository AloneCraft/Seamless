using Microsoft.CodeAnalysis;

namespace Test.Seamless.Database.Rdbms;

// 取り込んだビューは、定義を LINQ へ訳せるまで、取り込んだ時点の写しを Tables.<スキーマ> の表として出す(合意8)。
// SQL Server へ直接問い合わせる実行用のビューだけは、行の型を Views.<スキーマ> に出す。
public sealed class RdbmsViewSourceGeneratorTests {
    private static global::Seamless.Database.Rdbms.DatabaseCatalog Catalog(global::Seamless.Database.Rdbms.UniqueKeyCatalog? key)=>new(
        "TinyStore",
        [new global::Seamless.Database.Rdbms.SchemaCatalog(
            "sales",
            [],
            [new(
                "ProductSummary",
                [
                    new(0,"ProductId",new(global::Seamless.Database.Rdbms.DatabaseTypeKind.Int32,false),false),
                    new(1,"Total",new(global::Seamless.Database.Rdbms.DatabaseTypeKind.Decimal,true),false),
                ],
                key)])]);

    [Fact,Trait("Category","Unit")]
    public void Generate_CreatesViewCopiesAsDeclaredTables() {
        var result=global::Seamless.Database.Rdbms.CSharpDatabaseGenerator.Generate(Catalog(new("UX_ProductSummary",[new("ProductId",false)])));

        var database=Assert.Single(result.Files,static file=>file.RelativePath=="TinyStore.cs").Content;
        Assert.DoesNotContain("MaterializedView",database,StringComparison.Ordinal);
        var view=Assert.Single(result.Files,static file=>file.RelativePath=="Tables/sales/ProductSummary.cs").Content;
        Assert.Contains("namespace TinyStore.Tables.sales;",view,StringComparison.Ordinal);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(view,@"\[global::Seamless\.Database\.Key\]"));
        Assert.Equal(global::Seamless.Database.Rdbms.GeneratedRelationKind.View,Assert.Single(result.Tables).Kind);
        var output=RdbmsSourceGeneratorTests.Compile(result);
        // キーはビューの一意キーの列だけ。
        Assert.Equal(["ProductId"],output.GetTypeByMetadataName("TinyStore.Keys.sales.ProductSummary")!.GetMembers().OfType<IPropertySymbol>().Select(static property=>property.Name));
    }

    [Fact,Trait("Category","Unit")]
    public void Generate_UsesAllViewColumnsWhenNoUniqueKeyExists() {
        var result=global::Seamless.Database.Rdbms.CSharpDatabaseGenerator.Generate(Catalog(null));
        var view=Assert.Single(result.Files,static file=>file.RelativePath=="Tables/sales/ProductSummary.cs").Content;
        // [Key] を付けないと、すべての列がキーになる。
        Assert.DoesNotContain("[global::Seamless.Database.Key]",view,StringComparison.Ordinal);
        var output=RdbmsSourceGeneratorTests.Compile(result);
        Assert.Equal(["ProductId","Total"],output.GetTypeByMetadataName("TinyStore.Keys.sales.ProductSummary")!.GetMembers().OfType<IPropertySymbol>().Select(static property=>property.Name));
    }

    [Fact,Trait("Category","Unit")]
    public void Generate_ExecutableViewsReadRowsFromSqlServer(){
        var result=global::Seamless.Database.Rdbms.CSharpDatabaseGenerator.Generate(Catalog(null),executableViews:true);
        Assert.Empty(result.Tables);
        var row=Assert.Single(result.Files,static file=>file.RelativePath=="Views/sales/ProductSummary.cs").Content;
        Assert.Contains("public readonly record struct ProductSummary(",row,StringComparison.Ordinal);
        var database=Assert.Single(result.Files,static file=>file.RelativePath=="TinyStore.cs").Content;
        Assert.Contains("IEnumerable<global::TinyStore.Views.sales.ProductSummary> ProductSummary",database,StringComparison.Ordinal);
        _=RdbmsSourceGeneratorTests.Compile(result);
    }
}
