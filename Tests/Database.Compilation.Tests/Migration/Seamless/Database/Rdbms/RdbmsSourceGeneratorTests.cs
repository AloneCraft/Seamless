namespace Test.Seamless.Database.Rdbms;

public sealed class RdbmsSourceGeneratorTests {
    [Fact,Trait("Category","Unit")]
    public void Generate_CreatesSchemaTableRecordAndKeySources() {
        var catalog = new global::Seamless.Database.Rdbms.DatabaseCatalog(
            "TinyStore",
            [new("sales",[new("Product",[
                new(0,"Product Id",new(global::Seamless.Database.Rdbms.DatabaseTypeKind.Int32,false),false),
                new(1,"Name",new(global::Seamless.Database.Rdbms.DatabaseTypeKind.String,false),false),
            ],new("PK_Product",[new("Product Id",false)]))])]);

        var result = global::Seamless.Database.Rdbms.CSharpDatabaseGenerator.Generate(catalog);

        Assert.Equal(3,result.Files.Count);
        Assert.Equal(64,result.SchemaFingerprint.Length);
        var database = Assert.Single(result.Files,static file=>file.RelativePath=="TinyStore.cs").Content;
        Assert.Contains("public global::TinyStore.Schemas.sales sales { get; }",database,StringComparison.Ordinal);
        Assert.Contains("this.sales=new(this);",database,StringComparison.Ordinal);
        Assert.DoesNotContain("_salesSchema",database,StringComparison.Ordinal);
        Assert.Contains(
            "public global::Seamless.Database.Table<global::TinyStore.Keys.Product,global::TinyStore.Records.Product> Product",
            database,
            StringComparison.Ordinal);
        Assert.Contains("static record=>new(record.Product_Id)",database,StringComparison.Ordinal);
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
        var record=Assert.Single(result.Files,static file=>file.RelativePath.StartsWith("Records/",StringComparison.Ordinal)).Content;

        Assert.DoesNotContain("class Person(",database,StringComparison.Ordinal);
        Assert.DoesNotContain(" int Person { get; }",record,StringComparison.Ordinal);
        Assert.All(result.Tables,static table=>Assert.NotEqual("Person.Person",table.ObjectName));
    }
}
