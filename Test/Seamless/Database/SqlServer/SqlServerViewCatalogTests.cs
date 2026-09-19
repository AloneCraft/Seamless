namespace Test.Seamless.Database.SqlServer;

public sealed class SqlServerViewCatalogTests {
    [Fact,Trait("Category","Integration")]
    public async Task ReadAsync_ReadsIndexedViewColumnsAndUniqueKey() {
        var connectionString=Environment.GetEnvironmentVariable("SEAMLESS_SQLSERVER_TEST_CONNECTION");
        if(string.IsNullOrWhiteSpace(connectionString)) {
            return;
        }
        var suffix=Guid.NewGuid().ToString("N");
        var tableName=$"SeamlessViewTable_{suffix}";
        var viewName=$"SeamlessIndexedView_{suffix}";
        var indexName=$"UX_{viewName}";
        await using var connection=new global::Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection,"SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET CONCAT_NULL_YIELDS_NULL ON; SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF;");
        await ExecuteAsync(connection,$"CREATE TABLE dbo.[{tableName}] (Id int NOT NULL,Name nvarchar(40) NULL);");
        await ExecuteAsync(
            connection,
            $"CREATE VIEW dbo.[{viewName}] WITH SCHEMABINDING AS SELECT Id,Name FROM dbo.[{tableName}];");
        await ExecuteAsync(connection,$"CREATE UNIQUE CLUSTERED INDEX [{indexName}] ON dbo.[{viewName}] (Id);");
        try {
            var catalog=await new global::Seamless.Database.SqlServer.SqlServerCatalogReader(connectionString).ReadAsync();
            var view=Assert.Single(catalog.Schemas.Single(schema=>schema.Name=="dbo").Views,value=>value.Name==viewName);

            Assert.Equal(["Id","Name"],view.Columns.Select(static column=>column.Name));
            Assert.Equal(indexName,view.UniqueKey!.Name);
            Assert.Equal(["Id"],view.UniqueKey.Columns.Select(static column=>column.Name));
        } finally {
            await ExecuteAsync(connection,$"DROP VIEW dbo.[{viewName}];");
            await ExecuteAsync(connection,$"DROP TABLE dbo.[{tableName}];");
        }
    }

    private static async Task ExecuteAsync(global::Microsoft.Data.SqlClient.SqlConnection connection,string commandText) {
        await using var command=new global::Microsoft.Data.SqlClient.SqlCommand(commandText,connection);
        await command.ExecuteNonQueryAsync();
    }
}
