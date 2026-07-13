using Microsoft.Data.Sqlite;

namespace Seamless.UI.Storage;

public sealed class UiRunStore{
    private const int SchemaVersion = 19;
    private readonly string databasePath;
    private readonly UiRunAppKind appKind;
    private readonly object schemaGate = new();
    private bool schemaEnsured;

    public UiRunStore(string databasePath,UiRunAppKind appKind=UiRunAppKind.Benchmark){
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        this.databasePath=databasePath;
        this.appKind=appKind;
    }

    public string DatabasePath => this.databasePath;
    public UiRunAppKind AppKind => this.appKind;

    public void 分析ビューを更新(){
        using var connection=this.OpenConnection();
        using var transaction=BeginTransactionWithLockRetry(connection);
        分析ビューを作成(connection,transaction,this.appKind);
        transaction.Commit();
    }

    private static void 分析ビューを作成(SqliteConnection connection,SqliteTransaction transaction,UiRunAppKind appKind){
        ExecuteNonQuery(connection,transaction,"""
            DROP VIEW IF EXISTS "BenchmarkPerformanceRatioDelta";
            DROP VIEW IF EXISTS "BenchmarkPerformanceRatioHistory";
            DROP VIEW IF EXISTS "BenchmarkMemoryRatioDelta";
            DROP VIEW IF EXISTS "BenchmarkMemoryRatioHistory";
            DROP VIEW IF EXISTS "基準時間比";
            DROP VIEW IF EXISTS "基準メモリ比";
            DROP VIEW IF EXISTS "履歴時間比";
            DROP VIEW IF EXISTS "履歴メモリ比";
            DROP VIEW IF EXISTS "履歴時間シェア";
            DROP VIEW IF EXISTS "基準時間シェア";
            DROP VIEW IF EXISTS "履歴メモリシェア";
            DROP VIEW IF EXISTS "基準メモリシェア";
            DROP VIEW IF EXISTS "Scalable履歴";
            DROP VIEW IF EXISTS "Scalable前回比較";
            DROP VIEW IF EXISTS "Scalable前回シェア";
            """);
        if(appKind is UiRunAppKind.Benchmark){
            ExecuteNonQuery(connection,transaction,"""
                CREATE VIEW "履歴時間シェア" AS
                SELECT r."実行ID",r."更新日時",b."タブ名",b."型",b."ベンチマーク",b."左時間ns",b."右時間ns",
                    CASE WHEN b."左時間ns"=0 AND b."右時間ns"=0 THEN 0.0 ELSE CAST(b."左時間ns" AS REAL)/(b."左時間ns"+b."右時間ns") END AS "左シェア",
                    CASE WHEN b."左時間ns"=0 AND b."右時間ns"=0 THEN 0.0 ELSE CAST(b."右時間ns" AS REAL)/(b."左時間ns"+b."右時間ns") END AS "右シェア"
                FROM "Benchmark" b INNER JOIN "実行" r ON r."実行ID"=b."実行ID"
                WHERE b."左状態"='完了' AND b."右状態"='完了' AND b."左時間ns" IS NOT NULL AND b."右時間ns" IS NOT NULL
                    AND b."左時間ns">=0 AND b."右時間ns">=0;
                CREATE VIEW "履歴メモリシェア" AS
                SELECT r."実行ID",r."更新日時",b."タブ名",b."型",b."ベンチマーク",b."左メモリバイト",b."右メモリバイト",
                    CASE WHEN b."左メモリバイト"=0 AND b."右メモリバイト"=0 THEN 0.0 ELSE CAST(b."左メモリバイト" AS REAL)/(b."左メモリバイト"+b."右メモリバイト") END AS "左シェア",
                    CASE WHEN b."左メモリバイト"=0 AND b."右メモリバイト"=0 THEN 0.0 ELSE CAST(b."右メモリバイト" AS REAL)/(b."左メモリバイト"+b."右メモリバイト") END AS "右シェア"
                FROM "Benchmark" b INNER JOIN "実行" r ON r."実行ID"=b."実行ID"
                WHERE b."左状態"='完了' AND b."右状態"='完了' AND b."左メモリバイト" IS NOT NULL AND b."右メモリバイト" IS NOT NULL
                    AND b."左メモリバイト">=0 AND b."右メモリバイト">=0;
                """);
        }else if(appKind==UiRunAppKind.ScalableBenchmark){
            ExecuteNonQuery(connection,transaction,"""
                CREATE VIEW "Scalable履歴" AS SELECT r."実行ID",r."前回実行ID",r."更新日時",b."タブ名",b."型",b."ベンチマーク",b."Count",b."Initial",b."Data",b."Index",b."Page",b."Segment",b."Memory",b."Fraction",b."今回時間ns",b."今回メモリバイト" FROM "Benchmark" b INNER JOIN "実行" r ON r."実行ID"=b."実行ID" WHERE b."今回状態"='完了';
                CREATE VIEW "Scalable前回シェア" AS SELECT c.*,p."今回時間ns" AS "前回時間ns",
                    CASE WHEN c."今回時間ns" IS NULL OR p."今回時間ns" IS NULL OR c."今回時間ns"<0 OR p."今回時間ns"<0 THEN NULL WHEN c."今回時間ns"=0 AND p."今回時間ns"=0 THEN 0.0 ELSE CAST(c."今回時間ns" AS REAL)/(c."今回時間ns"+p."今回時間ns") END AS "今回時間シェア",
                    CASE WHEN c."今回時間ns" IS NULL OR p."今回時間ns" IS NULL OR c."今回時間ns"<0 OR p."今回時間ns"<0 THEN NULL WHEN c."今回時間ns"=0 AND p."今回時間ns"=0 THEN 0.0 ELSE CAST(p."今回時間ns" AS REAL)/(c."今回時間ns"+p."今回時間ns") END AS "前回時間シェア",
                    p."今回メモリバイト" AS "前回メモリバイト",
                    CASE WHEN c."今回メモリバイト" IS NULL OR p."今回メモリバイト" IS NULL OR c."今回メモリバイト"<0 OR p."今回メモリバイト"<0 THEN NULL WHEN c."今回メモリバイト"=0 AND p."今回メモリバイト"=0 THEN 0.0 ELSE CAST(c."今回メモリバイト" AS REAL)/(c."今回メモリバイト"+p."今回メモリバイト") END AS "今回メモリシェア",
                    CASE WHEN c."今回メモリバイト" IS NULL OR p."今回メモリバイト" IS NULL OR c."今回メモリバイト"<0 OR p."今回メモリバイト"<0 THEN NULL WHEN c."今回メモリバイト"=0 AND p."今回メモリバイト"=0 THEN 0.0 ELSE CAST(p."今回メモリバイト" AS REAL)/(c."今回メモリバイト"+p."今回メモリバイト") END AS "前回メモリシェア"
                FROM "Scalable履歴" c LEFT JOIN "Scalable履歴" p ON p."実行ID"=c."前回実行ID" AND p."タブ名"=c."タブ名" AND p."型"=c."型" AND p."ベンチマーク"=c."ベンチマーク" AND p."Count" IS c."Count" AND p."Initial" IS c."Initial" AND p."Data" IS c."Data" AND p."Index" IS c."Index" AND p."Page" IS c."Page" AND p."Segment" IS c."Segment" AND p."Memory" IS c."Memory" AND p."Fraction" IS c."Fraction";
                """);
        }
    }

    public void EnsureSchema(){
        lock(this.schemaGate){
            if(this.schemaEnsured){
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(this.databasePath)??".");
            if(File.Exists(this.databasePath)
                &&this.ReadStoredSchemaVersion()==SchemaVersion
                &&this.HasExpectedSchemaShape()){
                this.schemaEnsured=true;
                return;
            }
            this.ResolveSchemaMismatch();
            using var connection = this.OpenConnection();
            using var transaction = BeginTransactionWithLockRetry(connection);
            CreateTables(connection,transaction);
            FinalizeTablesForAppKind(connection,transaction,this.appKind);
            分析ビューを作成(connection,transaction,this.appKind);
            ExecuteNonQuery(connection,transaction,$"PRAGMA user_version={SchemaVersion};");
            transaction.Commit();
            this.schemaEnsured=true;
        }
    }

    private void ResolveSchemaMismatch(){
        if(!File.Exists(this.databasePath)){
            return;
        }

        var version = this.ReadStoredSchemaVersion();
        if(version==SchemaVersion&&this.HasExpectedSchemaShape()){
            return;
        }

        throw new InvalidOperationException(
            "UI run database schema mismatch. Update the configured database explicitly with a manual backup before starting the application: "+this.databasePath);
    }

    private long ReadStoredSchemaVersion(){
        try{
            using var connection = this.OpenConnectionWithoutCreate();
            using var command = connection.CreateCommand();
            command.CommandText="PRAGMA user_version;";
            return (long)(command.ExecuteScalar()??0L);
        }
        catch(SqliteException){
            return 0;
        }
    }

    private bool HasExpectedSchemaShape(){
        try{
            using var connection=this.OpenConnectionWithoutCreate();
            using var command=connection.CreateCommand();
            if(this.appKind==UiRunAppKind.ParameterSweep){
                command.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN('実行','Benchmark','診断カウンター');";
                if((long)(command.ExecuteScalar()??0L)!=3L){
                    return false;
                }
                command.CommandText="SELECT COUNT(*) FROM pragma_table_info('Benchmark') WHERE name IN('Collection','Generic','Backend','Sweep','MaxSuccessfulCount','EntryCount','追加時間ms','検索時間ns');";
                return (long)(command.ExecuteScalar()??0L)==8L;
            }

            command.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN('実行','Benchmark','診断カウンター');";
            if((long)(command.ExecuteScalar()??0L)!=3L){
                return false;
            }
            if(this.appKind==UiRunAppKind.ScalableBenchmark){
                command.CommandText="SELECT COUNT(*) FROM pragma_table_info('Benchmark') WHERE name IN('Count','Initial','Data','Index','Page','Segment','Memory','Fraction');";
                if((long)(command.ExecuteScalar()??0L)!=8L){
                    return false;
                }
                command.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='view' AND name IN('Scalable履歴','Scalable前回シェア');";
                if((long)(command.ExecuteScalar()??0L)!=2L){
                    return false;
                }
                command.CommandText="SELECT COUNT(*) FROM pragma_table_info('Scalable前回シェア') WHERE name IN('実行ID','前回実行ID','更新日時','タブ名','型','ベンチマーク','今回時間シェア','前回時間シェア','今回メモリシェア','前回メモリシェア');";
                if((long)(command.ExecuteScalar()??0L)!=10L){
                    return false;
                }
            }else{
                command.CommandText="SELECT COUNT(*) FROM pragma_table_info('Benchmark') WHERE name IN('左LoopCount','右LoopCount');";
                if((long)(command.ExecuteScalar()??0L)!=2L){
                    return false;
                }
                command.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='view' AND name IN('履歴時間シェア','履歴メモリシェア');";
                if((long)(command.ExecuteScalar()??0L)!=2L){
                    return false;
                }
                command.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='view' AND name IN('基準時間シェア','基準メモリシェア');";
                if((long)(command.ExecuteScalar()??0L)!=0L){
                    return false;
                }
            }
            command.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='view' AND name IN('履歴時間比','基準時間比','履歴メモリ比','基準メモリ比','Scalable前回比較');";
            return (long)(command.ExecuteScalar()??0L)==0L;
        }
        catch(SqliteException){
            return false;
        }
    }

    private static void CreateTables(SqliteConnection connection,SqliteTransaction transaction){
        ExecuteNonQuery(connection,transaction,"""
            CREATE TABLE IF NOT EXISTS "実行"(
                "実行ID" INTEGER PRIMARY KEY AUTOINCREMENT,
                "表示名" TEXT NOT NULL,
                "状態" TEXT NOT NULL,
                "実行プロファイル" TEXT NOT NULL,
                "タブ名" TEXT NOT NULL,
                "更新日時" TEXT NOT NULL,
                "実行ルート" TEXT NOT NULL DEFAULT '',
                "Adaptive閾値ns" REAL NOT NULL DEFAULT 0,
                "論理CPU一覧" TEXT NOT NULL DEFAULT '',
                "前回実行ID" INTEGER NULL,
                FOREIGN KEY("前回実行ID") REFERENCES "実行"("実行ID") ON DELETE SET NULL
            );
            CREATE TABLE IF NOT EXISTS "Benchmark"(
                "実行ID" INTEGER NOT NULL,
                "タブ名" TEXT NOT NULL,
                "型" TEXT NOT NULL,
                "ベンチマーク" TEXT NOT NULL,
                "CPU番号" INTEGER NULL,
                "行チェック" INTEGER NOT NULL DEFAULT 0,
                "左状態" TEXT NOT NULL,
                "左失敗理由" TEXT NOT NULL DEFAULT '',
                "左日時" TEXT NULL,
                "左時間ns" REAL NULL,
                "左メモリバイト" INTEGER NULL,
                "左LoopCount" INTEGER NULL,
                "右状態" TEXT NOT NULL,
                "右失敗理由" TEXT NOT NULL DEFAULT '',
                "右日時" TEXT NULL,
                "右時間ns" REAL NULL,
                "右メモリバイト" INTEGER NULL,
                "右LoopCount" INTEGER NULL,
                FOREIGN KEY("実行ID") REFERENCES "実行"("実行ID") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS "ScalableBenchmark"(
                "実行ID" INTEGER NOT NULL,
                "タブ名" TEXT NOT NULL,
                "型" TEXT NOT NULL,
                "ベンチマーク" TEXT NOT NULL,
                "Count" INTEGER NULL,
                "Initial" INTEGER NULL,
                "Data" INTEGER NULL,
                "Index" INTEGER NULL,
                "Page" INTEGER NULL,
                "Segment" INTEGER NULL,
                "Memory" INTEGER NULL,
                "Fraction" REAL NULL,
                "CPU番号" INTEGER NULL,
                "行チェック" INTEGER NOT NULL DEFAULT 0,
                "今回状態" TEXT NOT NULL,
                "今回失敗理由" TEXT NOT NULL DEFAULT '',
                "今回日時" TEXT NULL,
                "今回時間ns" REAL NULL,
                "今回メモリバイト" INTEGER NULL,
                FOREIGN KEY("実行ID") REFERENCES "実行"("実行ID") ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS "診断カウンター"(
                "実行ID" INTEGER NOT NULL,
                "タブ名" TEXT NOT NULL DEFAULT '',
                "プロジェクト名" TEXT NOT NULL,
                "ベンチマーク" TEXT NOT NULL,
                "引数キー" TEXT NOT NULL DEFAULT '',
                "カウンター名" TEXT NOT NULL,
                "カウンター値" REAL NOT NULL,
                "記録日時" TEXT NOT NULL,
                FOREIGN KEY("実行ID") REFERENCES "実行"("実行ID") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_実行_更新日時" ON "実行"("更新日時");
            CREATE INDEX IF NOT EXISTS "IX_Benchmark_実行ID" ON "Benchmark"("実行ID");
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_Benchmark_行" ON "Benchmark"(
                "実行ID",
                "タブ名",
                "型",
                "ベンチマーク"
            );
            CREATE INDEX IF NOT EXISTS "IX_ScalableBenchmark_実行ID" ON "ScalableBenchmark"("実行ID");
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_ScalableBenchmark_行" ON "ScalableBenchmark"(
                "実行ID","タブ名","型","ベンチマーク",
                COALESCE("Count",-1),COALESCE("Initial",-1),COALESCE("Data",-1),COALESCE("Index",-1),
                COALESCE("Page",-1),COALESCE("Segment",-1),COALESCE("Memory",-1),COALESCE("Fraction",-1)
            );
            """);
    }

    private static void FinalizeTablesForAppKind(SqliteConnection connection,SqliteTransaction transaction,UiRunAppKind appKind){
        if(appKind is UiRunAppKind.Benchmark){
            ExecuteNonQuery(connection,transaction,"DROP TABLE IF EXISTS \"ScalableBenchmark\";");
        }else if(appKind==UiRunAppKind.ScalableBenchmark){
            DropViews(connection,transaction);
            ExecuteNonQuery(connection,transaction,"DROP TABLE IF EXISTS \"Benchmark\";ALTER TABLE \"ScalableBenchmark\" RENAME TO \"Benchmark\";");
        }else if(appKind==UiRunAppKind.ParameterSweep){
            DropViews(connection,transaction);
            ExecuteNonQuery(connection,transaction,"""
                DROP TABLE IF EXISTS "Benchmark";
                DROP TABLE IF EXISTS "ScalableBenchmark";
                CREATE TABLE "Benchmark"(
                    "実行ID" INTEGER NOT NULL,
                    "ケース名" TEXT NOT NULL,
                    "Collection" TEXT NOT NULL,
                    "Generic" TEXT NOT NULL,
                    "Backend" TEXT NOT NULL,
                    "Sweep" TEXT NOT NULL,
                    "IndexSize" INTEGER NOT NULL,
                    "PageSize" INTEGER NOT NULL,
                    "SegmentSize" INTEGER NOT NULL,
                    "MemorySize" INTEGER NOT NULL,
                    "MutableFraction" REAL NOT NULL,
                    "MaxSuccessfulCount" INTEGER NOT NULL,
                    "EntryCount" INTEGER NOT NULL,
                    "失敗種別" TEXT NOT NULL,
                    "失敗理由" TEXT NOT NULL,
                    "追加時間ms" REAL NOT NULL,
                    "検索時間ns" REAL NULL,
                    "測定日時" TEXT NOT NULL,
                    PRIMARY KEY("実行ID","ケース名"),
                    FOREIGN KEY("実行ID") REFERENCES "実行"("実行ID") ON DELETE CASCADE
                );
                """);
        }
    }

    private static void DropViews(SqliteConnection connection,SqliteTransaction transaction){
        using var command=connection.CreateCommand();
        command.Transaction=transaction;
        command.CommandText="SELECT name FROM sqlite_master WHERE type='view';";
        using var reader=command.ExecuteReader();
        var names=new List<string>();
        while(reader.Read()){
            names.Add(reader.GetString(0));
        }
        reader.Close();
        foreach(var name in names){
            ExecuteNonQuery(connection,transaction,"DROP VIEW \""+name.Replace("\"","\"\"")+"\";");
        }
    }

    public UiRunRecord CreateRun(UiRunCreateRequest request){
        if(request.AppKind!=this.appKind){
            throw new InvalidOperationException($"The store is configured for {this.appKind}, not {request.AppKind}.");
        }
        this.EnsureSchema();
        var now = request.CreatedAt;
        using var connection = this.OpenConnection();
        long? previousRunId=null;
        if(request.AppKind==UiRunAppKind.ScalableBenchmark){
            using var previousCommand=connection.CreateCommand();
            previousCommand.CommandText="""
                SELECT "実行ID" FROM "実行"
                WHERE "タブ名"=$sourceTab AND "状態"=$completed
                ORDER BY "更新日時" DESC,"実行ID" DESC LIMIT 1;
                """;
            previousCommand.Parameters.AddWithValue("$sourceTab",request.SourceTab);
            previousCommand.Parameters.AddWithValue("$completed",ToStatusText(UiRunStatus.Completed));
            previousRunId=previousCommand.ExecuteScalar() as long?;
        }
        using var command = connection.CreateCommand();
        command.CommandText="""
            INSERT INTO "実行"("表示名","状態","実行プロファイル","タブ名","更新日時","前回実行ID")
            VALUES($displayName,$status,$executionProfile,$sourceTab,$updatedAt,$previousRunId);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$displayName",request.DisplayName);
        command.Parameters.AddWithValue("$status",ToStatusText(UiRunStatus.Queued));
        command.Parameters.AddWithValue("$executionProfile",request.ExecutionProfile);
        command.Parameters.AddWithValue("$sourceTab",request.SourceTab);
        command.Parameters.AddWithValue("$updatedAt",ToText(now));
        command.Parameters.AddWithValue("$previousRunId",ToDbValue(previousRunId));
        var runId = (long)(ExecuteScalarWithLockRetry(command)??0L);
        return new(runId,request.AppKind,request.DisplayName,UiRunStatus.Queued,request.ExecutionProfile,request.SourceTab,now);
    }

    public void SetRunStatus(long runId,UiRunStatus status,string message = ""){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            UPDATE "実行"
            SET "状態"=$status,"更新日時"=$updatedAt
            WHERE "実行ID"=$runId;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        command.Parameters.AddWithValue("$status",ToStatusText(status));
        command.Parameters.AddWithValue("$updatedAt",ToText(DateTimeOffset.UtcNow));
        ExecuteWithLockRetry(() => command.ExecuteNonQuery());
    }

    public void SaveRunSettings(long runId,string runRoot,IReadOnlyList<int> elementCounts,double adaptiveThresholdNanoseconds,IReadOnlyList<int> logicalCpus){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            UPDATE "実行"
            SET "実行ルート"=$runRoot,
                "Adaptive閾値ns"=$adaptiveThreshold,
                "論理CPU一覧"=$logicalCpus,
                "更新日時"=$updatedAt
            WHERE "実行ID"=$runId;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        command.Parameters.AddWithValue("$runRoot",runRoot);
        command.Parameters.AddWithValue("$adaptiveThreshold",adaptiveThresholdNanoseconds);
        command.Parameters.AddWithValue("$logicalCpus",string.Join(",",logicalCpus));
        command.Parameters.AddWithValue("$updatedAt",ToText(DateTimeOffset.UtcNow));
        command.ExecuteNonQuery();
    }

    public UiRunSettingsRecord? GetRunSettings(long runId){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT "実行ID","実行ルート","Adaptive閾値ns","論理CPU一覧"
            FROM "実行"
            WHERE "実行ID"=$runId;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new UiRunSettingsRecord(reader.GetInt64(0),reader.GetString(1),reader.GetDouble(2),reader.GetString(3))
            : null;
    }

    public long? GetPreviousRunId(long runId){
        this.EnsureSchema();
        using var connection=this.OpenConnection();
        using var command=connection.CreateCommand();
        command.CommandText="SELECT \"前回実行ID\" FROM \"実行\" WHERE \"実行ID\"=$runId;";
        command.Parameters.AddWithValue("$runId",runId);
        return command.ExecuteScalar() is long value ? value : null;
    }

    public UiCaseRecord UpsertCase(
        long runId,
        string caseKey,
        string name,
        string project,
        string assemblyName,
        string method,
        string typeFullName,
        IReadOnlyDictionary<string,string>? arguments = null,
        string sourceTabId = "",
        string argumentKey = "",
        string side = "",
        string pairDiscriminator = "",
        long? elementCount = null,
        long? loopCount = null){
        this.EnsureSchema();
        var caseId = CreateLegacyRecordId(runId,sourceTabId,typeFullName,method,side);
        return new(caseId,runId,caseKey,name,project,assemblyName,method,typeFullName,sourceTabId,argumentKey,side,pairDiscriminator);
    }

    public void SaveDetectedBenchmarkItems(long runId,IReadOnlyList<UiBenchmarkItemWrite> items){
        var rows = items
            .GroupBy(static item => string.Join("|",item.TabName,item.TypeName,item.BenchmarkName),StringComparer.OrdinalIgnoreCase)
            .Select(static group => {
                var left = group.FirstOrDefault(static item => string.Equals(item.Side,"Left",StringComparison.OrdinalIgnoreCase));
                var right = group.FirstOrDefault(static item => string.Equals(item.Side,"Right",StringComparison.OrdinalIgnoreCase));
                var first = left??right??group.First();
                return new UiBenchmarkSnapshotItemWrite(
                    first.TabName,
                    first.TypeName,
                    first.BenchmarkName,
                    first.CpuNumber,
                    group.Any(static item => item.IsSelected),
                    CreateDetectedSide(left),
                    CreateDetectedSide(right));
            })
            .ToArray();
        this.SaveBenchmarkSnapshotItems(runId,rows);
    }

    public void SaveBenchmarkSnapshotItems(long runId,IReadOnlyList<UiBenchmarkSnapshotItemWrite> items){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        var appKind=ReadRunAppKind(connection,runId)??throw new InvalidOperationException($"Run {runId} was not found.");
        using var transaction = BeginTransactionWithLockRetry(connection);
        foreach(var item in items){
            using var command = connection.CreateCommand();
            command.Transaction=transaction;
            if(appKind==UiRunAppKind.ScalableBenchmark){
                var arguments=ReadScalableArguments(item.LeftCase??item.RightCase);
                command.CommandText="""
                    INSERT OR REPLACE INTO "Benchmark"(
                        "実行ID","タブ名","型","ベンチマーク","Count","Initial","Data","Index","Page","Segment","Memory","Fraction",
                        "CPU番号","行チェック","今回状態","今回失敗理由","今回日時","今回時間ns","今回メモリバイト")
                    VALUES($runId,$tabName,$type,$benchmark,$count,$initial,$data,$index,$page,$segment,$memory,$fraction,
                        $cpu,$rowSelected,$leftStatus,$leftFailure,$leftMeasuredAt,$leftMean,$leftAllocated);
                    """;
                AddScalableArgumentParameters(command,arguments);
            }else{
                command.CommandText="""
                    INSERT OR REPLACE INTO "Benchmark"(
                        "実行ID","タブ名","型","ベンチマーク","CPU番号","行チェック",
                        "左状態","左失敗理由","左日時","左時間ns","左メモリバイト","左LoopCount",
                        "右状態","右失敗理由","右日時","右時間ns","右メモリバイト","右LoopCount")
                    VALUES($runId,$tabName,$type,$benchmark,$cpu,$rowSelected,
                        $leftStatus,$leftFailure,$leftMeasuredAt,$leftMean,$leftAllocated,$leftLoopCount,
                        $rightStatus,$rightFailure,$rightMeasuredAt,$rightMean,$rightAllocated,$rightLoopCount);
                    """;
                AddSideParameters(command,"right",item.Right);
                command.Parameters.AddWithValue("$rightLoopCount",ToDbValue(item.Right.LoopCount));
            }
            command.Parameters.AddWithValue("$runId",runId);
            command.Parameters.AddWithValue("$tabName",item.TabName);
            command.Parameters.AddWithValue("$type",item.TypeName);
            command.Parameters.AddWithValue("$benchmark",item.BenchmarkName);
            command.Parameters.AddWithValue("$cpu",ToDbValue(item.CpuNumber));
            command.Parameters.AddWithValue("$rowSelected",item.IsSelected ? 1 : 0);
            AddSideParameters(command,"left",item.Left);
            command.Parameters.AddWithValue("$leftLoopCount",ToDbValue(item.Left.LoopCount));
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public UiJobRecord UpsertJob(
        long runId,
        long caseId,
        string jobKey,
        UiRunStatus status,
        long? elementCount,
        long? loopCount,
        int? assignedLogicalCpu,
        DateTimeOffset? startedAt,
        DateTimeOffset? finishedAt,
        string failureKind,
        string failureReason){
        this.EnsureSchema();
        return new(caseId,runId,caseId,jobKey,status,elementCount,loopCount,assignedLogicalCpu,startedAt,finishedAt,failureKind,failureReason);
    }

    public void SaveParameterSweepResult(UiParameterSweepResult result){
        if(this.appKind!=UiRunAppKind.ParameterSweep){
            throw new InvalidOperationException("ParameterSweep results require a ParameterSweep store.");
        }
        this.EnsureSchema();
        using var connection=this.OpenConnection();
        using var command=connection.CreateCommand();
        command.CommandText="""
            INSERT OR REPLACE INTO "Benchmark"(
                "実行ID","ケース名","Collection","Generic","Backend","Sweep","IndexSize","PageSize","SegmentSize","MemorySize","MutableFraction",
                "MaxSuccessfulCount","EntryCount","失敗種別","失敗理由","追加時間ms","検索時間ns","測定日時")
            VALUES($runId,$caseName,$collection,$generic,$backend,$sweep,$indexSize,$pageSize,$segmentSize,$memorySize,$fraction,
                $maxCount,$entryCount,$failureKind,$failureReason,$addElapsed,$lookupMean,$measuredAt);
            """;
        command.Parameters.AddWithValue("$runId",result.RunId);
        command.Parameters.AddWithValue("$caseName",result.CaseName);
        command.Parameters.AddWithValue("$collection",result.Collection);
        command.Parameters.AddWithValue("$generic",result.GenericName);
        command.Parameters.AddWithValue("$backend",result.Backend);
        command.Parameters.AddWithValue("$sweep",result.Sweep);
        command.Parameters.AddWithValue("$indexSize",result.IndexSize);
        command.Parameters.AddWithValue("$pageSize",result.PageSize);
        command.Parameters.AddWithValue("$segmentSize",result.SegmentSize);
        command.Parameters.AddWithValue("$memorySize",result.MemorySize);
        command.Parameters.AddWithValue("$fraction",result.MutableFraction);
        command.Parameters.AddWithValue("$maxCount",result.MaxSuccessfulCount);
        command.Parameters.AddWithValue("$entryCount",result.EntryCount);
        command.Parameters.AddWithValue("$failureKind",result.FailureKind);
        command.Parameters.AddWithValue("$failureReason",result.FailureReason);
        command.Parameters.AddWithValue("$addElapsed",result.AddElapsedMilliseconds);
        command.Parameters.AddWithValue("$lookupMean",ToDbValue(result.LookupMeanNanoseconds));
        command.Parameters.AddWithValue("$measuredAt",ToText(result.MeasuredAt));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<UiParameterSweepResult> ListParameterSweepResults(long runId){
        if(this.appKind!=UiRunAppKind.ParameterSweep){
            throw new InvalidOperationException("ParameterSweep results require a ParameterSweep store.");
        }
        this.EnsureSchema();
        using var connection=this.OpenConnection();
        using var command=connection.CreateCommand();
        command.CommandText="""
            SELECT "実行ID","ケース名","Collection","Generic","Backend","Sweep","IndexSize","PageSize","SegmentSize","MemorySize","MutableFraction",
                   "MaxSuccessfulCount","EntryCount","失敗種別","失敗理由","追加時間ms","検索時間ns","測定日時"
            FROM "Benchmark" WHERE "実行ID"=$runId ORDER BY "ケース名";
            """;
        command.Parameters.AddWithValue("$runId",runId);
        using var reader=command.ExecuteReader();
        var values=new List<UiParameterSweepResult>();
        while(reader.Read()){
            values.Add(new(
                reader.GetInt64(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),reader.GetString(5),
                reader.GetInt64(6),reader.GetInt64(7),reader.GetInt64(8),reader.GetInt64(9),reader.GetDouble(10),reader.GetInt64(11),reader.GetInt64(12),
                reader.GetString(13),reader.GetString(14),reader.GetDouble(15),reader.IsDBNull(16)?null:reader.GetDouble(16),FromText(reader.GetString(17))));
        }
        return values;
    }

    public void DeleteAllRecords(){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="DELETE FROM \"診断カウンター\";DELETE FROM \"Benchmark\";DELETE FROM \"実行\";";
        command.ExecuteNonQuery();
    }

    public void DeleteRun(long runId){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var transaction = BeginTransactionWithLockRetry(connection);
        if(TableExists(connection,transaction,"UI選択")){
            using var selectionCommand = connection.CreateCommand();
            selectionCommand.Transaction=transaction;
            selectionCommand.CommandText="DELETE FROM \"UI選択\" WHERE \"RunId\"=$runId;";
            selectionCommand.Parameters.AddWithValue("$runId",runId);
            selectionCommand.ExecuteNonQuery();
        }
        using var command = connection.CreateCommand();
        command.Transaction=transaction;
        command.CommandText="DELETE FROM \"実行\" WHERE \"実行ID\"=$runId;";
        command.Parameters.AddWithValue("$runId",runId);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void SaveMeasurement(long jobId,IReadOnlyList<UiMetricValueWrite> metrics,DateTimeOffset? measuredAt = null){
        this.EnsureSchema();
    }

    public void SaveComparisons(long runId,IReadOnlyList<UiComparisonWrite> comparisons){
    }

    public void SaveFallbackDiagnostics(long runId,IReadOnlyList<UiFallbackDiagnosticWrite> diagnostics,DateTimeOffset? recordedAt = null){
        this.EnsureSchema();
        var timestamp = recordedAt??DateTimeOffset.UtcNow;
        using var connection = this.OpenConnection();
        using var transaction = BeginTransactionWithLockRetry(connection);
        foreach(var diagnostic in diagnostics){
            using var command = connection.CreateCommand();
            command.Transaction=transaction;
            command.CommandText="""
                INSERT INTO "診断カウンター"(
                    "実行ID","タブ名","プロジェクト名","ベンチマーク","引数キー","カウンター名","カウンター値","記録日時")
                VALUES($runId,$sourceTab,$project,$method,$argumentKey,$counterName,$counterValue,$recordedAt);
                """;
            command.Parameters.AddWithValue("$runId",runId);
            command.Parameters.AddWithValue("$sourceTab",diagnostic.SourceTab);
            command.Parameters.AddWithValue("$project",diagnostic.Project);
            command.Parameters.AddWithValue("$method",diagnostic.Method);
            command.Parameters.AddWithValue("$argumentKey",diagnostic.ArgumentKey);
            command.Parameters.AddWithValue("$counterName",diagnostic.CounterName);
            command.Parameters.AddWithValue("$counterValue",diagnostic.CounterValue);
            command.Parameters.AddWithValue("$recordedAt",ToText(timestamp));
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public IReadOnlyList<UiFallbackDiagnosticRecord> ListFallbackDiagnostics(long runId){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT "実行ID","タブ名","プロジェクト名","ベンチマーク","引数キー","カウンター名","カウンター値","記録日時"
            FROM "診断カウンター"
            WHERE "実行ID"=$runId
            ORDER BY "プロジェクト名","ベンチマーク","引数キー","カウンター名";
            """;
        command.Parameters.AddWithValue("$runId",runId);
        using var reader = command.ExecuteReader();
        var records = new List<UiFallbackDiagnosticRecord>();
        while(reader.Read()){
            records.Add(new(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                null,
                reader.GetString(5),
                reader.GetDouble(6),
                DateTimeOffset.Parse(reader.GetString(7),System.Globalization.CultureInfo.InvariantCulture)));
        }

        return records;
    }

    public IReadOnlyList<UiRunRecord> ListRuns(UiRunAppKind? appKind = null){
        if(appKind is { } requestedAppKind&&requestedAppKind!=this.appKind){
            return [];
        }
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT "実行ID","表示名","状態","実行プロファイル","タブ名","更新日時"
            FROM "実行"
            ORDER BY "更新日時" DESC,"実行ID" DESC;
            """;

        using var reader = command.ExecuteReader();
        var values = new List<UiRunRecord>();
        while(reader.Read()){
            values.Add(new(
                reader.GetInt64(0),
                this.appKind,
                reader.GetString(1),
                FromStatusText(reader.GetString(2)),
                reader.GetString(3),
                reader.GetString(4),
                FromText(reader.GetString(5))));
        }

        return values;
    }

    public IReadOnlyList<UiMetricValueRecord> ListMetrics(long runId){
        var values = new List<UiMetricValueRecord>();
        var jobId = 1L;
        foreach(var item in this.ListBenchmarkSnapshotItems(runId)){
            AddSideMetrics(values,jobId++,item.Left);
            AddSideMetrics(values,jobId++,item.Right);
        }

        return values;
    }

    public IReadOnlyList<UiRunStateRecord> ListRunStates(long runId) => [];

    public IReadOnlyList<UiCaseRecord> ListCases(long runId){
        this.EnsureSchema();
        return [];
    }

    public IReadOnlyDictionary<string,string> ListCaseArguments(long caseId){
        this.EnsureSchema();
        return new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<UiJobRecord> ListJobs(long runId){
        this.EnsureSchema();
        return [];
    }

    public IReadOnlyList<UiBenchmarkSnapshotItemRecord> ListBenchmarkSnapshotItems(long runId){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        var appKind=ReadRunAppKind(connection,runId);
        if(appKind is null){
            return [];
        }
        if(appKind==UiRunAppKind.ScalableBenchmark){
            return ListScalableBenchmarkSnapshotItems(connection,runId);
        }
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT "実行ID","タブ名","型","ベンチマーク","CPU番号",
                "行チェック",
                "左状態","左失敗理由","左日時","左時間ns","左メモリバイト","左LoopCount",
                "右状態","右失敗理由","右日時","右時間ns","右メモリバイト","右LoopCount"
            FROM "Benchmark"
            WHERE "実行ID"=$runId
            ORDER BY "タブ名","型","ベンチマーク";
            """;
        command.Parameters.AddWithValue("$runId",runId);
        using var reader = command.ExecuteReader();
        var values = new List<UiBenchmarkSnapshotItemRecord>();
        while(reader.Read()){
            values.Add(new(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.GetInt32(5)!=0,
                ReadSide(reader,6,reader.IsDBNull(11) ? null : reader.GetInt32(11)),
                ReadSide(reader,12,reader.IsDBNull(17) ? null : reader.GetInt32(17))));
        }

        return values;
    }

    public IReadOnlyList<UiComparisonRecord> ListComparisons(long runId) => [];

    public static IReadOnlyList<string> AnalysisViewNames { get; } = [
        "履歴時間シェア",
        "履歴メモリシェア",
        "Scalable履歴",
        "Scalable前回シェア",
        "BenchmarkFallbackDiagnostics",
    ];

    public IReadOnlyList<IReadOnlyDictionary<string,object?>> QueryAnalysisView(string viewName,int limit = 1000){
        if(string.Equals(viewName,"履歴時間シェア",StringComparison.Ordinal)
            ||string.Equals(viewName,"履歴メモリシェア",StringComparison.Ordinal)
            ||string.Equals(viewName,"Scalable履歴",StringComparison.Ordinal)
            ||string.Equals(viewName,"Scalable前回シェア",StringComparison.Ordinal)){
            return this.QuerySqlAnalysisView(viewName,limit);
        }

        if(!string.Equals(viewName,"BenchmarkFallbackDiagnostics",StringComparison.OrdinalIgnoreCase)){
            return [];
        }
        return this.ListRuns()
            .SelectMany(run => this.ListFallbackDiagnostics(run.RunId))
            .OrderByDescending(static item => item.RecordedAt)
            .Take(limit)
            .Select(static item => new Dictionary<string,object?>{
                ["RunId"]=item.RunId,
                ["SourceTab"]=item.SourceTab,
                ["Project"]=item.Project,
                ["Method"]=item.Method,
                ["ArgumentKey"]=item.ArgumentKey,
                ["CounterName"]=item.CounterName,
                ["CounterValue"]=item.CounterValue,
                ["RecordedAt"]=item.RecordedAt,
            })
            .ToArray();
    }

    private IReadOnlyList<IReadOnlyDictionary<string,object?>> QuerySqlAnalysisView(string viewName,int limit){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText=$"""
            SELECT *
            FROM "{viewName}"
            ORDER BY "実行ID" DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit",limit);
        using var reader = command.ExecuteReader();
        var rows = new List<IReadOnlyDictionary<string,object?>>();
        while(reader.Read()){
            var values = new Dictionary<string,object?>(StringComparer.OrdinalIgnoreCase);
            for(var i=0;i<reader.FieldCount;i++){
                values[reader.GetName(i)]=reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            rows.Add(values);
        }
        return rows;
    }

    public IReadOnlyList<UiShareHistoryPoint> ListShareHistory(UiShareHistoryQuery query,UiShareMetric metric,int limit = 1000){
        ArgumentNullException.ThrowIfNull(query);
        if(query.AppKind!=this.appKind){
            throw new InvalidDataException(
                "Share history app kind mismatch. Store="+this.appKind+" Query="+query.AppKind);
        }
        if(this.appKind==UiRunAppKind.ParameterSweep){
            throw new InvalidDataException("ParameterSweep does not provide benchmark share history.");
        }
        if(string.IsNullOrWhiteSpace(query.TabName)
            ||string.IsNullOrWhiteSpace(query.TypeName)
            ||string.IsNullOrWhiteSpace(query.BenchmarkName)){
            throw new InvalidDataException(
                "Share history identity is incomplete. Tab="+query.TabName
                +" Type="+query.TypeName
                +" Benchmark="+query.BenchmarkName);
        }

        this.EnsureSchema();
        using var connection=this.OpenConnection();
        using var command=connection.CreateCommand();
        if(this.appKind==UiRunAppKind.ScalableBenchmark){
            if(query.BenchmarkCase is null){
                throw new InvalidDataException(
                    "Scalable share history identity requires benchmark arguments. Tab="+query.TabName
                    +" Type="+query.TypeName
                    +" Benchmark="+query.BenchmarkName);
            }
            var leftColumn=metric==UiShareMetric.Time?"今回時間シェア":"今回メモリシェア";
            var rightColumn=metric==UiShareMetric.Time?"前回時間シェア":"前回メモリシェア";
            command.CommandText=$"""
                SELECT "実行ID","{leftColumn}","{rightColumn}"
                FROM "Scalable前回シェア"
                WHERE "タブ名"=$tab AND "型"=$type AND "ベンチマーク"=$benchmark
                  AND "Count" IS $count AND "Initial" IS $initial AND "Data" IS $data
                  AND "Index" IS $index AND "Page" IS $page AND "Segment" IS $segment
                  AND "Memory" IS $memory AND "Fraction" IS $fraction
                  AND "{leftColumn}" IS NOT NULL AND "{rightColumn}" IS NOT NULL
                ORDER BY "実行ID" DESC
                LIMIT $limit;
                """;
            var arguments=ReadScalableArguments(query.BenchmarkCase,true);
            AddScalableArgumentParameters(command,arguments);
        }else{
            var viewName=metric==UiShareMetric.Time?"履歴時間シェア":"履歴メモリシェア";
            command.CommandText=$"""
                SELECT "実行ID","左シェア","右シェア"
                FROM "{viewName}"
                WHERE "タブ名"=$tab AND "型"=$type AND "ベンチマーク"=$benchmark
                ORDER BY "実行ID" DESC
                LIMIT $limit;
                """;
        }
        command.Parameters.AddWithValue("$tab",query.TabName);
        command.Parameters.AddWithValue("$type",query.TypeName);
        command.Parameters.AddWithValue("$benchmark",query.BenchmarkName);
        command.Parameters.AddWithValue("$limit",Math.Max(1,limit));
        using var reader=command.ExecuteReader();
        var points=new List<UiShareHistoryPoint>();
        while(reader.Read()){
            points.Add(new(reader.GetInt64(0),reader.GetDouble(1),reader.GetDouble(2)));
        }
        points.Reverse();
        return points;
    }

    private SqliteConnection OpenConnection(){
        var builder = new SqliteConnectionStringBuilder{
            DataSource=this.databasePath,
            Mode=SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout=30,
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText="PRAGMA busy_timeout=30000;PRAGMA journal_mode=WAL;PRAGMA foreign_keys=ON;";
        command.ExecuteNonQuery();
        return connection;
    }

    private SqliteConnection OpenConnectionWithoutCreate(){
        var builder = new SqliteConnectionStringBuilder{
            DataSource=this.databasePath,
            Mode=SqliteOpenMode.ReadWrite,
            DefaultTimeout=30,
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        return connection;
    }

    private static void ExecuteNonQuery(SqliteConnection connection,SqliteTransaction transaction,string text){
        using var command = connection.CreateCommand();
        command.Transaction=transaction;
        command.CommandText=text;
        command.ExecuteNonQuery();
    }

    private static bool TableExists(SqliteConnection connection,SqliteTransaction transaction,string tableName){
        using var command = connection.CreateCommand();
        command.Transaction=transaction;
        command.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name;";
        command.Parameters.AddWithValue("$name",tableName);
        return (long)(command.ExecuteScalar()??0L)>0;
    }

    private static SqliteTransaction BeginTransactionWithLockRetry(SqliteConnection connection){
        const int attempts = 5;
        for(var attempt = 1;attempt<=attempts;attempt++){
            try{
                return connection.BeginTransaction();
            }
            catch(SqliteException ex) when(IsBusyOrLocked(ex)&&attempt<attempts){
                Thread.Sleep(TimeSpan.FromMilliseconds(200*attempt));
            }
        }

        return connection.BeginTransaction();
    }

    private static void ExecuteWithLockRetry(Action action){
        const int attempts = 5;
        for(var attempt = 1;attempt<=attempts;attempt++){
            try{
                action();
                return;
            }
            catch(SqliteException ex) when(IsBusyOrLocked(ex)&&attempt<attempts){
                Thread.Sleep(TimeSpan.FromMilliseconds(200*attempt));
            }
        }
    }

    private static bool IsBusyOrLocked(SqliteException exception) =>
        exception.SqliteErrorCode is 5 or 6;

    private static object? ExecuteScalarWithLockRetry(SqliteCommand command){
        const int attempts = 5;
        for(var attempt = 1;attempt<=attempts;attempt++){
            try{
                return command.ExecuteScalar();
            }
            catch(SqliteException ex) when(IsBusyOrLocked(ex)&&attempt<attempts){
                Thread.Sleep(TimeSpan.FromMilliseconds(200*attempt));
            }
        }

        return command.ExecuteScalar();
    }

    private static void AddMetric(
        List<UiMetricValueRecord> values,
        long jobId,
        string name,
        SqliteDataReader reader,
        int ordinal,
        string unit,
        DateTimeOffset measuredAt){
        if(reader.IsDBNull(ordinal)){
            return;
        }
        values.Add(new(jobId,name,reader.GetDouble(ordinal),unit,measuredAt));
    }

    private static void AddSideMetrics(List<UiMetricValueRecord> values,long jobId,UiBenchmarkGridSideRecord side){
        var measuredAt = side.MeasuredAt??DateTimeOffset.MinValue;
        if(side.MeanNanoseconds is { } meanNanoseconds){
            values.Add(new(jobId,"MeanNanoseconds",meanNanoseconds,"ns",measuredAt));
        }
        if(side.AllocatedBytes is { } allocatedBytes){
            values.Add(new(jobId,"AllocatedBytes",allocatedBytes,"bytes",measuredAt));
        }
    }

    private static UiBenchmarkGridSideWrite CreateDetectedSide(UiBenchmarkItemWrite? item){
        if(item is null){
            return new(UiRunStatus.Queued,string.Empty,null,null,null);
        }

        return new(
            UiRunStatus.Queued,
            string.Empty,
            null,
            null,
            null);
    }

    private static void AddSideParameters(SqliteCommand command,string prefix,UiBenchmarkGridSideWrite side){
        command.Parameters.AddWithValue("$"+prefix+"Status",ToStatusText(side.Status));
        command.Parameters.AddWithValue("$"+prefix+"Failure",side.FailureReason);
        command.Parameters.AddWithValue("$"+prefix+"MeasuredAt",ToDbValue(side.MeasuredAt));
        command.Parameters.AddWithValue("$"+prefix+"Mean",ToDbValue(side.MeanNanoseconds));
        command.Parameters.AddWithValue("$"+prefix+"Allocated",ToDbValue(side.AllocatedBytes));
    }

    private UiRunAppKind? ReadRunAppKind(SqliteConnection connection,long runId){
        using var command=connection.CreateCommand();
        command.CommandText="SELECT 1 FROM \"実行\" WHERE \"実行ID\"=$runId;";
        command.Parameters.AddWithValue("$runId",runId);
        return command.ExecuteScalar() is null ? null : this.appKind;
    }

    private static ScalableArguments ReadScalableArguments(UiBenchmarkSnapshotCaseWrite? identity,bool strict = false){
        var values=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        if(identity is not null&&!string.IsNullOrWhiteSpace(identity.ArgumentsJson)){
            try{
                using var document=System.Text.Json.JsonDocument.Parse(identity.ArgumentsJson);
                if(document.RootElement.ValueKind!=System.Text.Json.JsonValueKind.Array){
                    throw new System.Text.Json.JsonException("Benchmark arguments must be a JSON array.");
                }
                foreach(var item in document.RootElement.EnumerateArray()){
                    if(item.TryGetProperty("Name",out var name)&&item.TryGetProperty("Value",out var value)){
                        values[name.GetString()??string.Empty]=value.GetString()??value.ToString();
                    }
                }
            }catch(System.Text.Json.JsonException ex){
                if(strict){
                    throw new InvalidDataException("Scalable share history benchmark arguments are invalid JSON.",ex);
                }
            }
        }else if(strict){
            throw new InvalidDataException("Scalable share history benchmark arguments are missing.");
        }
        long? Long(string name)=>values.TryGetValue(name,out var text)&&long.TryParse(text,System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out var value) ? value : null;
        double? Double(string name)=>values.TryGetValue(name,out var text)&&double.TryParse(text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var value) ? value : null;
        return new(Long("Count"),Long("Initial"),Long("Data"),Long("Index"),Long("Page"),Long("Segment"),Long("Memory"),Double("Fraction"));
    }

    private static void AddScalableArgumentParameters(SqliteCommand command,ScalableArguments arguments){
        command.Parameters.AddWithValue("$count",ToDbValue(arguments.Count));
        command.Parameters.AddWithValue("$initial",ToDbValue(arguments.Initial));
        command.Parameters.AddWithValue("$data",ToDbValue(arguments.Data));
        command.Parameters.AddWithValue("$index",ToDbValue(arguments.Index));
        command.Parameters.AddWithValue("$page",ToDbValue(arguments.Page));
        command.Parameters.AddWithValue("$segment",ToDbValue(arguments.Segment));
        command.Parameters.AddWithValue("$memory",ToDbValue(arguments.Memory));
        command.Parameters.AddWithValue("$fraction",ToDbValue(arguments.Fraction));
    }

    private static IReadOnlyList<UiBenchmarkSnapshotItemRecord> ListScalableBenchmarkSnapshotItems(SqliteConnection connection,long runId){
        using var command=connection.CreateCommand();
        command.CommandText="""
            SELECT c."実行ID",c."タブ名",c."型",c."ベンチマーク",c."CPU番号",c."行チェック",
                   c."Count",c."Initial",c."Data",c."Index",c."Page",c."Segment",c."Memory",c."Fraction",
                   c."今回状態",c."今回失敗理由",c."今回日時",c."今回時間ns",c."今回メモリバイト",
                   p."今回状態",p."今回失敗理由",p."今回日時",p."今回時間ns",p."今回メモリバイト"
            FROM "Benchmark" c
            INNER JOIN "実行" r ON r."実行ID"=c."実行ID"
            LEFT JOIN "Benchmark" p
              ON p."実行ID"=r."前回実行ID" AND p."タブ名"=c."タブ名" AND p."型"=c."型" AND p."ベンチマーク"=c."ベンチマーク"
             AND p."Count" IS c."Count" AND p."Initial" IS c."Initial" AND p."Data" IS c."Data" AND p."Index" IS c."Index"
             AND p."Page" IS c."Page" AND p."Segment" IS c."Segment" AND p."Memory" IS c."Memory" AND p."Fraction" IS c."Fraction"
            WHERE c."実行ID"=$runId
            ORDER BY c."タブ名",c."型",c."ベンチマーク",c."Count";
            """;
        command.Parameters.AddWithValue("$runId",runId);
        using var reader=command.ExecuteReader();
        var values=new List<UiBenchmarkSnapshotItemRecord>();
        while(reader.Read()){
            var arguments=new ScalableArguments(
                reader.IsDBNull(6)?null:reader.GetInt64(6),reader.IsDBNull(7)?null:reader.GetInt64(7),
                reader.IsDBNull(8)?null:reader.GetInt64(8),reader.IsDBNull(9)?null:reader.GetInt64(9),
                reader.IsDBNull(10)?null:reader.GetInt64(10),reader.IsDBNull(11)?null:reader.GetInt64(11),
                reader.IsDBNull(12)?null:reader.GetInt64(12),reader.IsDBNull(13)?null:reader.GetDouble(13));
            var identity=CreateScalableIdentity(reader.GetString(3),arguments);
            var current=ReadSide(reader,14,null);
            var previous=reader.IsDBNull(19)
                ? new UiBenchmarkGridSideRecord(UiRunStatus.Queued,string.Empty,null,null,null)
                : ReadSide(reader,19,null);
            values.Add(new(reader.GetInt64(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.IsDBNull(4)?null:reader.GetInt32(4),reader.GetInt32(5)!=0,current,previous,identity,identity));
        }
        return values;
    }

    private static UiBenchmarkSnapshotCaseWrite CreateScalableIdentity(string methodName,ScalableArguments arguments){
        var pairs=new List<Dictionary<string,string>>();
        void Add(string name,object? value){ if(value is not null) pairs.Add(new(){{"Name",name},{"Value",Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture)??string.Empty}}); }
        Add("Count",arguments.Count);Add("Initial",arguments.Initial);Add("Data",arguments.Data);Add("Index",arguments.Index);
        Add("Page",arguments.Page);Add("Segment",arguments.Segment);Add("Memory",arguments.Memory);Add("Fraction",arguments.Fraction);
        var argumentKey=string.Join(",",pairs.Select(static pair=>pair["Name"]+"="+pair["Value"]));
        return new(string.Empty,string.Empty,methodName,argumentKey,System.Text.Json.JsonSerializer.Serialize(pairs));
    }

    private sealed record ScalableArguments(long? Count,long? Initial,long? Data,long? Index,long? Page,long? Segment,long? Memory,double? Fraction);

    private static UiBenchmarkGridSideRecord ReadSide(SqliteDataReader reader,int offset,int? loopCount){
        var measuredAt = reader.IsDBNull(offset+2)
            ? (DateTimeOffset?)null
            : FromText(reader.GetString(offset+2));
        return new(
            FromStatusText(reader.GetString(offset)),
            reader.GetString(offset+1),
            measuredAt,
            reader.IsDBNull(offset+3) ? null : reader.GetDouble(offset+3),
            reader.IsDBNull(offset+4) ? null : reader.GetInt64(offset+4),
            loopCount);
    }

    private static long CreateLegacyRecordId(long runId,string sourceTabId,string typeFullName,string method,string side){
        var hash = new HashCode();
        hash.Add(runId);
        hash.Add(sourceTabId,StringComparer.OrdinalIgnoreCase);
        hash.Add(typeFullName,StringComparer.OrdinalIgnoreCase);
        hash.Add(method,StringComparer.OrdinalIgnoreCase);
        hash.Add(side,StringComparer.OrdinalIgnoreCase);
        return Math.Abs((long)hash.ToHashCode());
    }

    private static double? GetMetric(IReadOnlyList<UiMetricValueWrite> metrics,string name) =>
        metrics.LastOrDefault(item => string.Equals(item.MetricName,name,StringComparison.OrdinalIgnoreCase)) is { } value
            ? value.NumericValue
            : null;

    private static double? GetNullableMetric(IReadOnlyList<UiMetricValueRecord>? metrics,string name) =>
        metrics?.LastOrDefault(item => string.Equals(item.MetricName,name,StringComparison.OrdinalIgnoreCase)) is { } value
            ? value.NumericValue
            : null;

    private static object ToDbValue(int? value) => value is { } actual ? actual : DBNull.Value;

    private static object ToDbValue(long? value) => value is { } actual ? actual : DBNull.Value;

    private static object ToDbValue(double? value) => value is { } actual ? actual : DBNull.Value;

    private static object ToDbValue(DateTimeOffset? value) => value is { } actual ? ToText(actual) : DBNull.Value;

    private static string ToText(DateTimeOffset value) => value.ToString("O",System.Globalization.CultureInfo.InvariantCulture);

    private static DateTimeOffset FromText(string value) =>
        DateTimeOffset.Parse(value,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind);

    private static string ToAppKindText(UiRunAppKind value) =>
        value switch{
            UiRunAppKind.ScalableBenchmark => "スケーラブル",
            UiRunAppKind.ParameterSweep => "ParameterSweep",
            _ => "標準",
        };

    private static UiRunAppKind FromAppKindText(string value) =>
        value switch{
            "スケーラブル" => UiRunAppKind.ScalableBenchmark,
            "ParameterSweep" => UiRunAppKind.ParameterSweep,
            _ => UiRunAppKind.Benchmark,
        };

    private static string ToStatusText(UiRunStatus value) =>
        value switch{
            UiRunStatus.Queued => "待機",
            UiRunStatus.Running => "実行中",
            UiRunStatus.Paused => "一時停止",
            UiRunStatus.Stopped => "停止",
            UiRunStatus.Completed => "完了",
            UiRunStatus.Failed => "失敗",
            _ => "未計測",
        };

    private static UiRunStatus FromStatusText(string value) =>
        value switch{
            "待機" => UiRunStatus.Queued,
            "実行中" => UiRunStatus.Running,
            "一時停止" => UiRunStatus.Paused,
            "停止" => UiRunStatus.Stopped,
            "完了" => UiRunStatus.Completed,
            "失敗" => UiRunStatus.Failed,
            _ => UiRunStatus.Queued,
        };

    private static string ToArgumentDisplay(IReadOnlyDictionary<string,string>? arguments){
        if(arguments is null||arguments.Count==0){
            return string.Empty;
        }
        return string.Join(",",
            arguments
                .Where(static item => !string.Equals(item.Key,"ElementCount",StringComparison.OrdinalIgnoreCase)
                    &&!string.Equals(item.Key,"LoopCount",StringComparison.OrdinalIgnoreCase)
                    &&!string.Equals(item.Key,"SourceTab",StringComparison.OrdinalIgnoreCase))
                .OrderBy(static item => item.Key,StringComparer.OrdinalIgnoreCase)
                .Select(static item => item.Key+"="+item.Value));
    }

    private static string ToGenericTypeName(string typeFullName){
        if(string.IsNullOrWhiteSpace(typeFullName)){
            return string.Empty;
        }

        var tick = typeFullName.IndexOf('`');
        if(tick<0){
            var dot = typeFullName.LastIndexOf('.');
            return dot>=0&&dot<typeFullName.Length-1 ? typeFullName[(dot+1)..] : typeFullName;
        }

        var start = typeFullName.IndexOf("[[",StringComparison.Ordinal);
        if(start<0){
            return string.Empty;
        }

        var names = new List<string>();
        var index = start;
        while(index>=0&&index<typeFullName.Length){
            var nextStart = typeFullName.IndexOf("[[",index,StringComparison.Ordinal);
            if(nextStart<0){
                break;
            }
            var nextEnd = typeFullName.IndexOf(',',nextStart+2);
            if(nextEnd<0){
                break;
            }
            var name = typeFullName.Substring(nextStart+2,nextEnd-nextStart-2);
            var dot = name.LastIndexOf('.');
            names.Add(dot>=0&&dot<name.Length-1 ? name[(dot+1)..] : name);
            index=typeFullName.IndexOf("]]",nextEnd,StringComparison.Ordinal);
            if(index<0){
                break;
            }
            index+=2;
        }

        return names.Count==0 ? string.Empty : string.Join(",",names.Select(ToKnownAlias));
    }

    private static string ToKnownAlias(string name) =>
        name switch{
            "Int32" => "int",
            "Int64" => "long",
            "String" => "string",
            "Boolean" => "bool",
            "Double" => "double",
            "Single" => "float",
            "Decimal" => "decimal",
            "Object" => "object",
            _ => name,
        };
}
