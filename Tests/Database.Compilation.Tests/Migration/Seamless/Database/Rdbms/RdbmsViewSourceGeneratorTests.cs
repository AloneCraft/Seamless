namespace Test.Seamless.Database.Rdbms;

public sealed class RdbmsViewSourceGeneratorTests {
    [Fact,Trait("Category","Unit")]
    public void Generate_CreatesKeyedMaterializedViewSources() {
        var catalog=new global::Seamless.Database.Rdbms.DatabaseCatalog(
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
                    new("UX_ProductSummary",[new("ProductId",false)]))])]);

        var result=global::Seamless.Database.Rdbms.CSharpDatabaseGenerator.Generate(catalog);

        var database=Assert.Single(result.Files,static file=>file.RelativePath=="TinyStore.cs").Content;
        Assert.Contains(
            "MaterializedView<global::TinyStore.Keys.ProductSummary,global::TinyStore.Records.ProductSummary> ProductSummary",
            database,
            StringComparison.Ordinal);
        Assert.Contains("static record=>new(record.ProductId)",database,StringComparison.Ordinal);
        Assert.Contains(result.Files,static file=>file.RelativePath=="Records/sales/ProductSummary.cs");
        Assert.Contains(result.Files,static file=>file.RelativePath=="Keys/sales/ProductSummary.cs");
        Assert.Equal(global::Seamless.Database.Rdbms.GeneratedRelationKind.View,Assert.Single(result.Tables).Kind);
    }

    [Fact,Trait("Category","Unit")]
    public void Generate_UsesAllViewColumnsWhenNoUniqueKeyExists() {
        var catalog=new global::Seamless.Database.Rdbms.DatabaseCatalog(
            "TinyStore",
            [new global::Seamless.Database.Rdbms.SchemaCatalog(
                "sales",
                [],
                [new(
                    "ProductSummary",
                    [
                        new(0,"ProductId",new(global::Seamless.Database.Rdbms.DatabaseTypeKind.Int32,false),false),
                        new(1,"Name",new(global::Seamless.Database.Rdbms.DatabaseTypeKind.String,false),false),
                    ],
                    null)])]);

        var result=global::Seamless.Database.Rdbms.CSharpDatabaseGenerator.Generate(catalog);
        var key=Assert.Single(result.Files,static file=>file.RelativePath=="Keys/sales/ProductSummary.cs").Content;

        Assert.Contains("ProductId",key,StringComparison.Ordinal);
        Assert.Contains("Name",key,StringComparison.Ordinal);
    }
}
