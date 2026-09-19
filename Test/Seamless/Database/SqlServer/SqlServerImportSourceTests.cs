using System.Data;
using Seamless.Database.Rdbms;

namespace Test.Seamless.Database.SqlServer;

public sealed class SqlServerImportSourceTests {
    [Fact,Trait("Category","Unit")]
    public void SelectIsolationLevel_UsesSnapshotForDiskTablesOnly() {
        var isolationLevel=global::Seamless.Database.SqlServer.SqlServerImportSource.SelectIsolationLevel(false);

        Assert.Equal(IsolationLevel.Snapshot,isolationLevel);
    }

    [Fact,Trait("Category","Unit")]
    public void SelectIsolationLevel_UsesReadCommittedWhenMemoryOptimizedTableExists() {
        var isolationLevel=global::Seamless.Database.SqlServer.SqlServerImportSource.SelectIsolationLevel(true);

        Assert.Equal(IsolationLevel.ReadCommitted,isolationLevel);
    }

    [Fact,Trait("Category","Unit")]
    public void CreateSelect_AddsSnapshotHintOnlyToMemoryOptimizedTable() {
        var table=CreateBinding();

        var diskSql=global::Seamless.Database.SqlServer.SqlServerImportSource.CreateSelect(table,false);
        var memorySql=global::Seamless.Database.SqlServer.SqlServerImportSource.CreateSelect(table,true);

        Assert.Equal("SELECT [Value] FROM [Sales].[Orders];",diskSql);
        Assert.Equal("SELECT [Value] FROM [Sales].[Orders] WITH (SNAPSHOT);",memorySql);
    }

    [Fact,Trait("Category","Unit")]
    public void CreateSelect_DoesNotConvertAlwaysEncryptedCiphertext() {
        var table=CreateBinding(DatabaseTypeKind.Binary);
        var encryptedColumns=new global::System.Collections.Generic.HashSet<(string Schema,string Table,string Column)> {
            ("Sales","Orders","Value"),
        };

        var sql=global::Seamless.Database.SqlServer.SqlServerImportSource.CreateSelect(
            table,false,encryptedColumns);

        Assert.Equal("SELECT [Value] FROM [Sales].[Orders];",sql);
    }

    private static GeneratedTableBinding CreateBinding(DatabaseTypeKind kind=DatabaseTypeKind.Int32)=>new(
        "Orders",
        "Sales",
        "Orders",
        [new("Value","Value",new(kind,false))]);
}
