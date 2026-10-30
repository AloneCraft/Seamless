using Benchmark.Dynamic.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Benchmark.Dynamic.Tests;

public sealed class DynamicDatabaseMigrationTests{
    private static string ReadResource(string name){
        using var stream=typeof(global::Benchmark.Dynamic.Infrastructure.DynamicDatabase).Assembly.GetManifestResourceStream(name)
            ??throw new InvalidDataException(name);
        using var reader=new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // 版2のDB(element_type 列なし)に 002 の移行SQLを当てると、列が足され、既存の行は Int として読める。
    [Fact,Trait("Category","Integration")]
    public void 版2のDBに要素型の列を足す移行が既存の行をIntにする(){
        var initial=ReadResource("Benchmark.Dynamic.Infrastructure.Storage.Schema.001_initial.sql");
        var version2=initial
            .Replace("    \"element_type\" TEXT NOT NULL DEFAULT 'Int',\r\n",string.Empty,StringComparison.Ordinal)
            .Replace("    \"element_type\" TEXT NOT NULL DEFAULT 'Int',\n",string.Empty,StringComparison.Ordinal)
            .Replace("VALUES(3,strftime","VALUES(2,strftime",StringComparison.Ordinal)
            .Replace("PRAGMA user_version=3;","PRAGMA user_version=2;",StringComparison.Ordinal);
        Assert.DoesNotContain("element_type",version2,StringComparison.Ordinal);

        using var connection=new SqliteConnection("Data Source=:memory:");
        connection.Open();
        Execute(connection,"PRAGMA foreign_keys=ON;");
        Execute(connection,version2);
        Execute(connection,"""
            INSERT INTO "DynamicRuns"("run_id","status","seed","profile","settings_json","created_at","updated_at")
            VALUES('run-1','Completed',1,'p','{}','2026-10-22T00:00:00Z','2026-10-22T00:00:00Z');
            INSERT INTO "DynamicQueries"("query_id","run_id","ordinal","path_key","generation_seed","canonical_ir_json","source_text","query_hash","status")
            VALUES('q-1','run-1',0,'k',1,'{}','','h','Completed');
            """);

        Execute(connection,ReadResource("Benchmark.Dynamic.Infrastructure.Storage.Schema.Migrations.002_element_type.sql"));

        using var command=connection.CreateCommand();
        command.CommandText="SELECT \"element_type\" FROM \"DynamicQueries\" WHERE \"query_id\"='q-1';";
        Assert.Equal("Int",command.ExecuteScalar());
        command.CommandText="SELECT MAX(\"version\") FROM \"DynamicSchemaInfo\";";
        Assert.Equal(3L,command.ExecuteScalar());
    }

    private static void Execute(SqliteConnection connection,string sql){
        using var command=connection.CreateCommand();
        command.CommandText=sql;
        command.ExecuteNonQuery();
    }
}
