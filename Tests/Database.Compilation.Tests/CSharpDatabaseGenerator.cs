using Seamless.Database.Compilation;
using Seamless.Database.Rdbms;

namespace Database.Compilation.Tests;

public sealed class CSharpDatabaseGenerator{
    [Fact]public void GenerateNative(){
        var catalog=new DatabaseCatalog("NativeShop",[new SchemaCatalog("sales",[new TableCatalog("order details",[
            new(0,"item id",new(DatabaseTypeKind.Int32,false,MaximumLength:4),false),
            new(1,"short name",new(DatabaseTypeKind.String,true,MaximumLength:20,SqlTypeName:"nvarchar"),false),
            new(2,"long name",new(DatabaseTypeKind.String,true,MaximumLength:4000,SqlTypeName:"nvarchar"),false),
        ],new PrimaryKeyCatalog("pk",[new("item id",false)]))])]);
        var generated=global::Seamless.Database.Rdbms.CSharpDatabaseGenerator.GenerateNative(catalog);
        Assert.Contains(generated.Files,f=>f.Content.Contains("FixedText<global::Seamless.Collections.TextBuffer32>",StringComparison.Ordinal));
        var snapshot=NativeCompilationSnapshot.Create("generated-v1",generated.Files.Select(f=>new NativeSourceFile(f.RelativePath,f.Content)),[typeof(DatabaseCatalog).Assembly.Location]);
        var analysis=new global::Seamless.Database.Compilation.NativeDatabaseCompiler().Analyze(snapshot,generated.TypeNamespace+"."+generated.DatabaseName);
        Assert.Empty(analysis.Diagnostics.Where(d=>d.Severity==NativeDiagnosticSeverity.CompilationError));
        var table=Assert.Single(analysis.Database.Tables);
        Assert.Equal("sales",table.Schema);
        Assert.Equal("order details",table.Name);
        var column=Assert.Single(table.Columns.Where(c=>c.Name=="short name"));
        Assert.Equal(32,column.TextCapacity);
        Assert.Equal(20,column.LogicalMaximumLength);
        Assert.Null(Assert.Single(table.Columns.Where(c=>c.Name=="long name")).TextCapacity);
        Assert.Contains("nvarchar(20)",new VendorSqlGenerator().Generate(analysis.Database,SqlVendor.SqlServer).SchemaSql);
        Assert.Contains("[item id] int NOT NULL",new VendorSqlGenerator().Generate(analysis.Database,SqlVendor.SqlServer).SchemaSql);
        {
            var source=catalog.Schemas[0].Tables[0];
            var columns=source.Columns.Select(c=>{
                if(c.Name=="item id"){return c with{DefaultDefinition="((0))"};}
                return c;
            }).ToArray();
            var unsupported=new DatabaseCatalog("DefaultShop",[new SchemaCatalog("sales",[new TableCatalog(source.Name,columns,source.PrimaryKey)])]);
            var imported=global::Seamless.Database.Rdbms.CSharpDatabaseGenerator.GenerateNative(unsupported);
            var code=NativeCompilationSnapshot.Create("preserve-features",imported.Files.Select(f=>new NativeSourceFile(f.RelativePath,f.Content)),[typeof(DatabaseCatalog).Assembly.Location]);
            var definition=new global::Seamless.Database.Compilation.NativeDatabaseCompiler().Analyze(code,imported.TypeNamespace+"."+imported.DatabaseName);
            Assert.Empty(definition.Diagnostics);
            Assert.Contains("Default",Assert.Single(definition.Database.Tables).SourceFeatures);
            var export=new VendorSqlGenerator().Generate(definition.Database,SqlVendor.SqlServer);
            Assert.Contains(export.Diagnostics,d=>d.Severity==NativeDiagnosticSeverity.Unsupported);
            Assert.DoesNotContain("CREATE TABLE",export.SchemaSql);
        }
    }
}
