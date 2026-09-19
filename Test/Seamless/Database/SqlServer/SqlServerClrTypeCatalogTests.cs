namespace Test.Seamless.Database.SqlServer;

public sealed class SqlServerClrTypeCatalogTests {
    [Fact,Trait("Category","Integration")]
    public async Task ReadAsync_PreservesSqlServerClrTypeColumnsAndPrimaryKey() {
        var connectionString=Environment.GetEnvironmentVariable("SEAMLESS_SQLSERVER_TEST_CONNECTION");
        if(string.IsNullOrWhiteSpace(connectionString)) {
            return;
        }
        var tableName=$"SeamlessClrTypeFixture_{Guid.NewGuid():N}";
        await using var connection=new global::Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await connection.OpenAsync();
        await using(var create=new global::Microsoft.Data.SqlClient.SqlCommand(
            $"CREATE TABLE dbo.[{tableName}] (Id int NOT NULL,Node hierarchyid NOT NULL,CONSTRAINT [PK_{tableName}] PRIMARY KEY (Id,Node));",
            connection)) {
            await create.ExecuteNonQueryAsync();
        }
        try {
            var catalog=await new global::Seamless.Database.SqlServer.SqlServerCatalogReader(connectionString).ReadAsync();
            var table=Assert.Single(catalog.Schemas.Single(schema=>schema.Name=="dbo").Tables,value=>value.Name==tableName);

            Assert.Equal(2,table.Columns.Count);
            var node=table.Columns.Single(column=>column.Name=="Node");
            Assert.Equal(global::Seamless.Database.Rdbms.DatabaseTypeKind.Binary,node.Type.Kind);
            Assert.Equal(["Id","Node"],table.PrimaryKey!.Columns.Select(static column=>column.Name));
        } finally {
            await using var drop=new global::Microsoft.Data.SqlClient.SqlCommand($"DROP TABLE dbo.[{tableName}];",connection);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
