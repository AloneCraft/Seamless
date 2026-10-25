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
            コマンド.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
            using(var Reader=コマンド.ExecuteReader()){
                var Tables=new List<string>();
                while(Reader.Read()){
                    Tables.Add(Reader.GetString(0));
                }
                Assert.Equal([
                    "BenchmarkBaselines",
                    "BenchmarkChanges",
                    "BenchmarkComparisons",
                    "BenchmarkImports",
                    "BenchmarkMeasurements",
                    "BenchmarkProfiles",
                    "BenchmarkRuns",
                    "BenchmarkSamples",
                    "BenchmarkSchema",
                    "DynamicDiagnostics",
                    "DynamicMeasurementSamples",
                    "DynamicMeasurements",
                    "DynamicOptimizationEvents",
                    "DynamicQueries",
                    "DynamicRunEvents",
                    "DynamicRunSources",
                    "DynamicRuns",
                    "DynamicSchemaInfo",
                    "DynamicSourceExecutions",
                    "DynamicValidationResults",
                ],Tables);
                Assert.Equal(DynamicInfrastructureSchema.UserTableCount,Tables.Count);
            }
            using var Columns=接続.CreateCommand();
            Columns.CommandText="SELECT name FROM pragma_table_info('DynamicValidationResults') ORDER BY cid;";
            using var ColumnReader=Columns.ExecuteReader();
            var ValidationColumns=new List<string>();
            while(ColumnReader.Read()){
                ValidationColumns.Add(ColumnReader.GetString(0));
            }
            Assert.Contains("execution_id",ValidationColumns);
            Assert.Contains("implementation_kind",ValidationColumns);
            Assert.Contains("full_result",ValidationColumns);
            Assert.Contains("exception_json",ValidationColumns);
            Assert.Contains("symbol_validation_json",ValidationColumns);
            ColumnReader.Close();
            using var SourceColumnsCommand=接続.CreateCommand();
            SourceColumnsCommand.CommandText="SELECT name FROM pragma_table_info('DynamicSourceExecutions') ORDER BY cid;";
            using var SourceColumnReader=SourceColumnsCommand.ExecuteReader();
            var SourceColumns=new List<string>();
            while(SourceColumnReader.Read()){
                SourceColumns.Add(SourceColumnReader.GetString(0));
            }
            Assert.Contains("capability_profile_id",SourceColumns);
            Assert.Contains("capability_profile_json",SourceColumns);
            Assert.Contains("generated_source",SourceColumns);
            Assert.Contains("source_hash",SourceColumns);
            Assert.Contains("source_emission_ns",SourceColumns);
            Assert.Contains("source_text_ns",SourceColumns);
            Assert.Contains("syntax_tree_ns",SourceColumns);
            Assert.Contains("compilation_ns",SourceColumns);
            Assert.Contains("generator_ns",SourceColumns);
            Assert.Contains("emit_ns",SourceColumns);
            Assert.Contains("load_ns",SourceColumns);
            Assert.Contains("bind_ns",SourceColumns);
            Assert.Contains("rewrite_summary_json",SourceColumns);
            Assert.Contains("fixed_point_termination",SourceColumns);
            Assert.Contains("assembly_unloaded",SourceColumns);
            SourceColumnReader.Close();
            using var OptimizationColumnsCommand=接続.CreateCommand();
            OptimizationColumnsCommand.CommandText="SELECT name FROM pragma_table_info('DynamicOptimizationEvents') ORDER BY cid;";
            using var OptimizationColumnReader=OptimizationColumnsCommand.ExecuteReader();
            var OptimizationColumns=new List<string>();
            while(OptimizationColumnReader.Read()){
                OptimizationColumns.Add(OptimizationColumnReader.GetString(0));
            }
            Assert.Contains("execution_id",OptimizationColumns);
            Assert.Contains("layer",OptimizationColumns);
            Assert.Contains("phase",OptimizationColumns);
            Assert.Contains("rule_id",OptimizationColumns);
            Assert.Contains("round",OptimizationColumns);
            Assert.Contains("pass",OptimizationColumns);
            Assert.Contains("visit",OptimizationColumns);
            Assert.Contains("node_id",OptimizationColumns);
            Assert.Contains("node_path",OptimizationColumns);
            Assert.Contains("depth",OptimizationColumns);
            Assert.Contains("stage_index",OptimizationColumns);
            Assert.Contains("parent_kind",OptimizationColumns);
            Assert.Contains("is_left",OptimizationColumns);
            Assert.Contains("is_right",OptimizationColumns);
            Assert.Contains("is_inner",OptimizationColumns);
            Assert.Contains("before_text",OptimizationColumns);
            Assert.Contains("after_text",OptimizationColumns);
            Assert.Contains("changed",OptimizationColumns);
            Assert.Contains("revisited",OptimizationColumns);
        } finally {
            一時ルートを削除(一時ルート);
        }
    }

    [Fact,Trait("Category","Integration")]
    public void OpenWritableConnection() {
        var 一時ルート=一時ルートを作成();
        var DBパス=Path.Combine(一時ルート,"Benchmark.Dynamic.db");
        try {
            var 未来バージョン=global::Benchmark.Dynamic.Infrastructure.DynamicInfrastructureSchema.CurrentVersion+1;
            var DB=new global::Benchmark.Dynamic.Infrastructure.DynamicDatabase(DBパス);
            DB.InitializeIfMissing();
            using(var 接続=DB.OpenWritableConnection()) {
                using var コマンド=接続.CreateCommand();
                コマンド.CommandText=$"PRAGMA user_version={未来バージョン};";
                コマンド.ExecuteNonQuery();
            }
            var 例外=Assert.Throws<DynamicDatabaseVersionMismatchException>(()=>DB.OpenWritableConnection());
            Assert.Equal(global::Benchmark.Dynamic.Infrastructure.DynamicInfrastructureSchema.CurrentVersion,例外.ExpectedVersion);
            Assert.Equal(未来バージョン,例外.ActualVersion);
            Assert.True(File.Exists(DBパス));
            Assert.Equal(未来バージョン,DB.ReadVersion());
        } finally {
            一時ルートを削除(一時ルート);
        }
    }

    [Fact,Trait("Category","Integration")]
    public void ReadConnectionを開いたままWritableConnectionで書き込める() {
        var 一時ルート=一時ルートを作成();
        var DBパス=Path.Combine(一時ルート,"Benchmark.Dynamic.db");
        try {
            var DB=new global::Benchmark.Dynamic.Infrastructure.DynamicDatabase(DBパス);
            DB.InitializeIfMissing();
            using var 読み取り=DB.OpenReadConnection();
            using var 書き込み=DB.OpenWritableConnection();
            using var コマンド=書き込み.CreateCommand();
            コマンド.CommandText="CREATE TABLE IF NOT EXISTS \"_dynamic_write_probe\"(value INTEGER); INSERT INTO \"_dynamic_write_probe\"(value) VALUES(1);";
            コマンド.ExecuteNonQuery();
            using var 確認=書き込み.CreateCommand();
            確認.CommandText="SELECT COUNT(*) FROM \"_dynamic_write_probe\";";
            Assert.Equal(1,Convert.ToInt32(確認.ExecuteScalar(),System.Globalization.CultureInfo.InvariantCulture));
        } finally {
            一時ルートを削除(一時ルート);
        }
    }

    internal static string 一時ルートを作成() {
        var ルート=Path.Combine(Path.GetTempPath(),"Benchmark.Dynamic.Tests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ルート);
        return ルート;
    }

    // テスト一式を回すと、閉じた直後の DB ファイルを外部(ウイルス対策や索引付け)が短い間つかんで、削除が使用中の IOException になることがある
    // (単独では通る。接続は Pooling=false で閉じている)。少し待って数回やり直す(Test/Benchmark/Pattern.cs の 一時パスを削除Async と同じ形)。
    internal static void 一時ルートを削除(string ルート) {
        const int 最大試行回数 = 40;
        for(var 試行 = 1;試行<=最大試行回数;試行++) {
            try {
                SqliteConnection.ClearAllPools();
                if(Directory.Exists(ルート)) {
                    Directory.Delete(ルート,true);
                }
                return;
            } catch(IOException) when(試行<最大試行回数) {
                Thread.Sleep(50);
            }
        }
    }
}
