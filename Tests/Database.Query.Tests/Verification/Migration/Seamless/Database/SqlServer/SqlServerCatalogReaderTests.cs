using FactAttribute=Database.Verification.Tests.SqlServerFactAttribute;
namespace Test.Seamless.Database.SqlServer;

public sealed class SqlServerCatalogReaderTests {
    [Fact,Trait("Category","Integration")]
    public async Task ReadAsync_ReadsConfiguredDatabaseCatalog() {
        var connectionString=Environment.GetEnvironmentVariable("SEAMLESS_SQLSERVER_TEST_CONNECTION")??throw new InvalidOperationException("SQL Server test connection is required.");

        var catalog=await new global::Seamless.Database.SqlServer.SqlServerCatalogReader(connectionString).ReadAsync();

        Assert.False(string.IsNullOrWhiteSpace(catalog.Name));
        Assert.All(catalog.Schemas,static schema=>Assert.NotEmpty(schema.Tables));
    }

    [Fact,Trait("Category","Integration")]
    public async Task GenerateAsync_ConvertsRealSqlServerTableToCSharp() {
        var connectionString=Environment.GetEnvironmentVariable("SEAMLESS_SQLSERVER_TEST_CONNECTION")??throw new InvalidOperationException("SQL Server test connection is required.");
        var tableName=$"SeamlessCatalogFixture_{Guid.NewGuid():N}";
        await using var connection=new global::Microsoft.Data.SqlClient.SqlConnection(connectionString);
        await connection.OpenAsync();
        await using(var create=new global::Microsoft.Data.SqlClient.SqlCommand(
            $"CREATE TABLE dbo.[{tableName}] (Id int NOT NULL,Name nvarchar(40) NULL,CONSTRAINT [PK_{tableName}] PRIMARY KEY (Id));",
            connection)) {
            await create.ExecuteNonQueryAsync();
        }
        try {
            var generated=await global::Seamless.Database.SqlServer.SqlServerDatabaseGenerator.GenerateAsync(connectionString);
            var source=Assert.Single(generated.Files,file=>file.RelativePath==$"Tables/dbo/{tableName}.cs").Content;
            Assert.Contains("public partial int Id { get; }",source,StringComparison.Ordinal);
            Assert.Contains("public partial string? Name { get; }",source,StringComparison.Ordinal);
            Assert.Contains("[global::Seamless.Database.Key]",source,StringComparison.Ordinal);
            Assert.DoesNotContain(generated.Files,file=>file.RelativePath.StartsWith("Keys/",StringComparison.Ordinal));
        } finally {
            await using var drop=new global::Microsoft.Data.SqlClient.SqlCommand($"DROP TABLE dbo.[{tableName}];",connection);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
