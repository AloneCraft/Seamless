using Benchmark.Dynamic.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Benchmark.Dynamic.Tests;

public sealed class DynamicDatabase {
    [Fact,Trait("Category","Integration")]
    public void InitializeIfMissing() {
        var 一時ルート=一時ルートを作成();
        var DBパス=Path.Combine(一時ルート,"Benchmark.Dynamic.db");
        try {
            var DB=new global::Benchmark.Dynamic.Infrastructure.DynamicDatabase(DBパス);
            Assert.True(DB.InitializeIfMissing());
            Assert.False(DB.InitializeIfMissing());
            Assert.Equal(DynamicInfrastructureSchema.CurrentVersion,DB.ReadVersion());
            using var 接続=DB.OpenReadConnection();
            using var コマンド=接続.CreateCommand();
            コマンド.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
            Assert.Equal(DynamicInfrastructureSchema.UserTableCount,Convert.ToInt32(コマンド.ExecuteScalar()));
        } finally {
            一時ルートを削除(一時ルート);
        }
    }

    [Fact,Trait("Category","Integration")]
    public void OpenWritableConnection() {
        var 一時ルート=一時ルートを作成();
        var DBパス=Path.Combine(一時ルート,"Benchmark.Dynamic.db");
        try {
            var DB=new global::Benchmark.Dynamic.Infrastructure.DynamicDatabase(DBパス);
            DB.InitializeIfMissing();
            using(var 接続=DB.OpenWritableConnection()) {
                using var コマンド=接続.CreateCommand();
                コマンド.CommandText="PRAGMA user_version=2;";
                コマンド.ExecuteNonQuery();
            }
            var 例外=Assert.Throws<DynamicDatabaseVersionMismatchException>(()=>DB.OpenWritableConnection());
            Assert.Equal(1,例外.ExpectedVersion);
            Assert.Equal(2,例外.ActualVersion);
            Assert.True(File.Exists(DBパス));
            Assert.Equal(2,DB.ReadVersion());
        } finally {
            一時ルートを削除(一時ルート);
        }
    }

    internal static string 一時ルートを作成() {
        var ルート=Path.Combine(Path.GetTempPath(),"Benchmark.Dynamic.Tests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ルート);
        return ルート;
    }

    internal static void 一時ルートを削除(string ルート) {
        SqliteConnection.ClearAllPools();
        if(Directory.Exists(ルート)) {
            Directory.Delete(ルート,true);
        }
    }
}
