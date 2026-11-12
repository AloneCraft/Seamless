using Seamless.Database.Compilation;

namespace Database.Compilation.Tests;

public sealed class SqlDefinitionConverter{
    [Theory]
    [InlineData(SqlVendor.SqlServer,"CREATE TABLE dbo.Items (Id int NOT NULL,Name nvarchar(20) NULL,PRIMARY KEY(Id));")]
    [InlineData(SqlVendor.MySql,"CREATE TABLE Items (Id int NOT NULL,Name varchar(20) NULL,PRIMARY KEY(Id));")]
    public void Convert(SqlVendor vendor,string sql){
        var converted=new global::Seamless.Database.Compilation.SqlDefinitionConverter().Convert(sql,vendor,"ImportedShop");
        Assert.Empty(converted.Diagnostics);
        Assert.NotNull(converted.CSharp);
        var snapshot=NativeCompilationSnapshot.Create("sql-import",converted.CSharp.Files.Select(f=>new NativeSourceFile(f.RelativePath,f.Content)),[typeof(DatabaseCatalog).Assembly.Location]);
        var analysis=new global::Seamless.Database.Compilation.NativeDatabaseCompiler().Analyze(snapshot,converted.CSharp.TypeNamespace+"."+converted.CSharp.DatabaseName);
        Assert.Empty(analysis.Diagnostics.Where(d=>d.Severity==NativeDiagnosticSeverity.CompilationError));
        var table=Assert.Single(analysis.Database.Tables);
        Assert.Equal("Items",table.Name);
        Assert.Equal("Id",Assert.Single(table.PrimaryKey));
        Assert.Equal(20,Assert.Single(table.Columns.Where(c=>c.Name=="Name")).LogicalMaximumLength);
    }
}
