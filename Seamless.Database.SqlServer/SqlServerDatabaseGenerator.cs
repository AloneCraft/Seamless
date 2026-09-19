namespace Seamless.Database.SqlServer;

public static class SqlServerDatabaseGenerator {
    public static async Task<GeneratedDatabase> GenerateAsync(
        string connectionString,
        CancellationToken cancellationToken=default) {
        var catalog = await new SqlServerCatalogReader(connectionString).ReadAsync(cancellationToken).ConfigureAwait(false);
        return CSharpDatabaseGenerator.Generate(catalog);
    }
}
