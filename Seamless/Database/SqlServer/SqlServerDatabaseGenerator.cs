using Microsoft.Data.SqlClient;
using Seamless.Database.Rdbms;
namespace Seamless.Database.SqlServer;

public static class SqlServerDatabaseGenerator {
    public static async Task<GeneratedDatabase> GenerateAsync(
        string connectionString,
        CancellationToken cancellationToken=default) {
        var catalog = await new SqlServerCatalogReader(connectionString).ReadAsync(cancellationToken).ConfigureAwait(false);
        return CSharpDatabaseGenerator.Generate(catalog);
    }

    /// <summary>
    /// ビューを C# のビューへ訳して生成する。訳せないビューは取り込まず、生成物の Compatibility に回避策つきで入れる。
    /// <paramref name="snapshotViews"/>(「スキーマ.名前」)に挙げたビューだけ、取り込んだ時点の写しにする。
    /// </summary>
    public static async Task<GeneratedDatabase> GenerateWithTranslatedViewsAsync(
        string connectionString,
        IReadOnlyCollection<string> snapshotViews,
        CancellationToken cancellationToken=default) {
        ArgumentNullException.ThrowIfNull(snapshotViews);
        var catalog = await new SqlServerCatalogReader(connectionString).ReadAsync(cancellationToken).ConfigureAwait(false);
        return CSharpDatabaseGenerator.Generate(catalog,new ViewImportOptions(new SqlServerViewTranslator()){SnapshotViews=snapshotViews});
    }
}
