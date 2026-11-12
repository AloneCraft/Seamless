namespace Test.Seamless.Database.SqlServer;

public sealed class SqlServerBitGenerationTests {
    [Fact,Trait("Category","Unit")]
    public void Generate_UsesSqlServerBitInsteadOfBoolean() {
        var type=global::Seamless.Database.SqlServer.SqlServerTypeMapper.Map("bit",false,1,1,0);
        var catalog=new global::Seamless.Database.Rdbms.DatabaseCatalog(
            "BitStore",
            [new("dbo",[new("Feature",[new(0,"Flag",type,false)],null)])]);

        var generated=global::Seamless.Database.Rdbms.CSharpDatabaseGenerator.Generate(catalog);
        var record=Assert.Single(generated.Files,static file=>file.RelativePath=="Records/dbo/Feature.cs").Content;

        Assert.Contains(
            "public partial global::Seamless.Database.SqlServer.Types.Bit Flag { get; }",
            record,
            StringComparison.Ordinal);
    }
}
