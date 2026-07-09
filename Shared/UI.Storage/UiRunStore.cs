using Microsoft.Data.Sqlite;

namespace Seamless.UI.Storage;

public sealed class UiRunStore{
    private const int SchemaVersion = 13;
    private readonly string databasePath;
    private readonly object schemaGate = new();
    private bool schemaEnsured;

    public UiRunStore(string databasePath){
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        this.databasePath=databasePath;
    }

    public string DatabasePath => this.databasePath;

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

        if(this.HasMigratableSchemaShape()){
            return;
        }

        throw new InvalidOperationException(
            "UI run database schema mismatch. Update Work\\UI\\runs.db explicitly with a manual backup before starting the application.");
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
            using var connection = this.OpenConnectionWithoutCreate();
            using(var legacyCommand = connection.CreateCommand()){
                legacyCommand.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN('Apps','Runs');";
                if((long)(legacyCommand.ExecuteScalar()??0L)>0L){
                    return false;
                }
            }

            using var command = connection.CreateCommand();
            command.CommandText="""
                SELECT
                    (SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='実行')
                    +(SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ベンチマーク項目')
                    +(SELECT COUNT(*) FROM sqlite_master WHERE type='view' AND name IN('基準時間比','基準メモリ比','履歴時間比','履歴メモリ比'))
                    +(SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='診断カウンター')
                    +(SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT IN('実行','ベンチマーク項目','診断カウンター','sqlite_sequence'))
                    +(SELECT COUNT(*) FROM pragma_table_info('実行') WHERE name IN('要素数一覧','選択要素数'))
                    +(SELECT COUNT(*) FROM pragma_table_info('診断カウンター') WHERE name='要素数')
                    +(SELECT COUNT(*) FROM pragma_table_info('ベンチマーク項目') WHERE name IN('左要素数','右要素数'))
                    +(SELECT COUNT(*) FROM pragma_table_info('ベンチマーク項目') WHERE name='行チェック')
                    +(SELECT COUNT(*) FROM pragma_table_info('ベンチマーク項目') WHERE name IN('左チェック','右チェック'));
                """;
            return (long)(command.ExecuteScalar()??-1L)==8L;
        }
        catch(SqliteException){
            return false;
        }
    }

    private bool HasMigratableSchemaShape(){
        try{
            using var connection = this.OpenConnectionWithoutCreate();
            using var command = connection.CreateCommand();
            command.CommandText="""
                SELECT
                    (SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='実行')
                    +(SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ベンチマーク項目')
                    +(SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='診断カウンター')
                    +(SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT IN('実行','ベンチマーク項目','診断カウンター','sqlite_sequence'))
                    +(SELECT COUNT(*) FROM pragma_table_info('ベンチマーク項目') WHERE name IN('左時間ns','右時間ns','左メモリバイト','右メモリバイト'))
                    +(SELECT COUNT(*) FROM pragma_table_info('ベンチマーク項目') WHERE name='行チェック');
                """;
            return (long)(command.ExecuteScalar()??-1L)==8L;
        }
        catch(SqliteException){
            return false;
        }
    }

    private static void CreateTables(SqliteConnection connection,SqliteTransaction transaction){
        ExecuteNonQuery(connection,transaction,"""
            CREATE TABLE IF NOT EXISTS "実行"(
                "実行ID" INTEGER PRIMARY KEY AUTOINCREMENT,
                "アプリ種別" TEXT NOT NULL,
                "表示名" TEXT NOT NULL,
                "状態" TEXT NOT NULL,
                "実行プロファイル" TEXT NOT NULL,
                "タブ名" TEXT NOT NULL,
                "更新日時" TEXT NOT NULL,
                "実行ルート" TEXT NOT NULL DEFAULT '',
                "Adaptive閾値ns" REAL NOT NULL DEFAULT 0,
                "論理CPU一覧" TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS "ベンチマーク項目"(
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
                "右状態" TEXT NOT NULL,
                "右失敗理由" TEXT NOT NULL DEFAULT '',
                "右日時" TEXT NULL,
                "右時間ns" REAL NULL,
                "右メモリバイト" INTEGER NULL,
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
            CREATE INDEX IF NOT EXISTS "IX_実行_アプリ種別_更新日時" ON "実行"("アプリ種別","更新日時");
            CREATE INDEX IF NOT EXISTS "IX_ベンチマーク項目_実行ID" ON "ベンチマーク項目"("実行ID");
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_ベンチマーク項目_行" ON "ベンチマーク項目"(
                "実行ID",
                "タブ名",
                "型",
                "ベンチマーク"
            );
            DROP VIEW IF EXISTS "BenchmarkPerformanceRatioDelta";
            DROP VIEW IF EXISTS "BenchmarkPerformanceRatioHistory";
            DROP VIEW IF EXISTS "BenchmarkMemoryRatioDelta";
            DROP VIEW IF EXISTS "BenchmarkMemoryRatioHistory";
            DROP VIEW IF EXISTS "基準時間比";
            DROP VIEW IF EXISTS "基準メモリ比";
            DROP VIEW IF EXISTS "履歴時間比";
            DROP VIEW IF EXISTS "履歴メモリ比";
            CREATE VIEW IF NOT EXISTS "履歴時間比" AS
            SELECT
                r."実行ID" AS "RunId",
                r."更新日時" AS "UpdatedAt",
                b."タブ名" AS "SourceTab",
                b."型" AS "Type",
                b."ベンチマーク" AS "Benchmark",
                b."左時間ns" AS "FLinqNs",
                b."右時間ns" AS "ZLinqNs",
                b."左時間ns" / NULLIF(b."右時間ns",0) AS "PerformanceRatio"
            FROM "ベンチマーク項目" b
            INNER JOIN "実行" r ON r."実行ID"=b."実行ID"
            WHERE b."左状態"='完了'
                AND b."右状態"='完了'
                AND b."左時間ns" IS NOT NULL
                AND b."右時間ns" IS NOT NULL;
            CREATE VIEW IF NOT EXISTS "基準時間比" AS
            WITH history AS (
                SELECT
                    h.*,
                    LAG(h."PerformanceRatio") OVER (
                        PARTITION BY h."SourceTab",h."Type",h."Benchmark"
                        ORDER BY h."RunId"
                    ) AS "PreviousPerformanceRatio"
                FROM "履歴時間比" h
            )
            SELECT
                "RunId",
                "UpdatedAt",
                "SourceTab",
                "Type",
                "Benchmark",
                "FLinqNs",
                "ZLinqNs",
                "PerformanceRatio",
                "PreviousPerformanceRatio",
                "PerformanceRatio" - "PreviousPerformanceRatio" AS "PerformanceRatioDelta"
            FROM history;
            CREATE VIEW IF NOT EXISTS "履歴メモリ比" AS
            SELECT
                r."実行ID" AS "RunId",
                r."更新日時" AS "UpdatedAt",
                b."タブ名" AS "SourceTab",
                b."型" AS "Type",
                b."ベンチマーク" AS "Benchmark",
                b."左メモリバイト" AS "FLinqBytes",
                b."右メモリバイト" AS "ZLinqBytes",
                CASE
                    WHEN b."左メモリバイト"=0 AND b."右メモリバイト"=0 THEN 0
                    ELSE CAST(b."左メモリバイト" AS REAL) / NULLIF(b."右メモリバイト",0)
                END AS "PerformanceRatio"
            FROM "ベンチマーク項目" b
            INNER JOIN "実行" r ON r."実行ID"=b."実行ID"
            WHERE b."左状態"='完了'
                AND b."右状態"='完了'
                AND b."左メモリバイト" IS NOT NULL
                AND b."右メモリバイト" IS NOT NULL;
            CREATE VIEW IF NOT EXISTS "基準メモリ比" AS
            WITH history AS (
                SELECT
                    h.*,
                    LAG(h."PerformanceRatio") OVER (
                        PARTITION BY h."SourceTab",h."Type",h."Benchmark"
                        ORDER BY h."RunId"
                    ) AS "PreviousPerformanceRatio"
                FROM "履歴メモリ比" h
            )
            SELECT
                "RunId",
                "UpdatedAt",
                "SourceTab",
                "Type",
                "Benchmark",
                "FLinqBytes",
                "ZLinqBytes",
                "PerformanceRatio",
                "PreviousPerformanceRatio",
                "PerformanceRatio" - "PreviousPerformanceRatio" AS "PerformanceRatioDelta"
            FROM history;
            """);
    }

    public UiRunRecord CreateRun(UiRunCreateRequest request){
        this.EnsureSchema();
        var now = request.CreatedAt;
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            INSERT INTO "実行"("アプリ種別","表示名","状態","実行プロファイル","タブ名","更新日時")
            VALUES($appKind,$displayName,$status,$executionProfile,$sourceTab,$updatedAt);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$appKind",ToAppKindText(request.AppKind));
        command.Parameters.AddWithValue("$displayName",request.DisplayName);
        command.Parameters.AddWithValue("$status",ToStatusText(UiRunStatus.Queued));
        command.Parameters.AddWithValue("$executionProfile",request.ExecutionProfile);
        command.Parameters.AddWithValue("$sourceTab",request.SourceTab);
        command.Parameters.AddWithValue("$updatedAt",ToText(now));
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
        using var transaction = BeginTransactionWithLockRetry(connection);
        foreach(var item in items){
            using var command = connection.CreateCommand();
            command.Transaction=transaction;
            command.CommandText="""
                INSERT OR REPLACE INTO "ベンチマーク項目"(
                    "実行ID","タブ名","型","ベンチマーク","CPU番号",
                    "行チェック",
                    "左状態","左失敗理由","左日時","左時間ns","左メモリバイト",
                    "右状態","右失敗理由","右日時","右時間ns","右メモリバイト")
                VALUES(
                    $runId,$tabName,$type,$benchmark,$cpu,
                    $rowSelected,
                    $leftStatus,$leftFailure,$leftMeasuredAt,$leftMean,$leftAllocated,
                    $rightStatus,$rightFailure,$rightMeasuredAt,$rightMean,$rightAllocated);
                """;
            command.Parameters.AddWithValue("$runId",runId);
            command.Parameters.AddWithValue("$tabName",item.TabName);
            command.Parameters.AddWithValue("$type",item.TypeName);
            command.Parameters.AddWithValue("$benchmark",item.BenchmarkName);
            command.Parameters.AddWithValue("$cpu",ToDbValue(item.CpuNumber));
            command.Parameters.AddWithValue("$rowSelected",item.IsSelected ? 1 : 0);
            AddSideParameters(command,"left",item.Left);
            AddSideParameters(command,"right",item.Right);
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

    public void DeleteAllRecords(){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="DELETE FROM \"診断カウンター\";DELETE FROM \"ベンチマーク項目\";DELETE FROM \"実行\";";
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
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        if(appKind is { } value){
            command.CommandText="""
                SELECT "実行ID","アプリ種別","表示名","状態","実行プロファイル","タブ名","更新日時"
                FROM "実行"
                WHERE "アプリ種別"=$appKind
                ORDER BY "更新日時" DESC,"実行ID" DESC;
                """;
            command.Parameters.AddWithValue("$appKind",ToAppKindText(value));
        }else{
            command.CommandText="""
                SELECT "実行ID","アプリ種別","表示名","状態","実行プロファイル","タブ名","更新日時"
                FROM "実行"
                ORDER BY "更新日時" DESC,"実行ID" DESC;
                """;
        }

        using var reader = command.ExecuteReader();
        var values = new List<UiRunRecord>();
        while(reader.Read()){
            values.Add(new(
                reader.GetInt64(0),
                FromAppKindText(reader.GetString(1)),
                reader.GetString(2),
                FromStatusText(reader.GetString(3)),
                reader.GetString(4),
                reader.GetString(5),
                FromText(reader.GetString(6))));
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
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT "実行ID","タブ名","型","ベンチマーク","CPU番号",
                "行チェック",
                "左状態","左失敗理由","左日時","左時間ns","左メモリバイト",
                "右状態","右失敗理由","右日時","右時間ns","右メモリバイト"
            FROM "ベンチマーク項目"
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
                ReadSide(reader,6),
                ReadSide(reader,11)));
        }

        return values;
    }

    public IReadOnlyList<UiComparisonRecord> ListComparisons(long runId) => [];

    public static IReadOnlyList<string> AnalysisViewNames { get; } = [
        "履歴時間比",
        "基準時間比",
        "履歴メモリ比",
        "基準メモリ比",
        "BenchmarkFallbackDiagnostics",
    ];

    public IReadOnlyList<IReadOnlyDictionary<string,object?>> QueryAnalysisView(string viewName,int limit = 1000){
        if(string.Equals(viewName,"履歴時間比",StringComparison.Ordinal)
            ||string.Equals(viewName,"基準時間比",StringComparison.Ordinal)
            ||string.Equals(viewName,"履歴メモリ比",StringComparison.Ordinal)
            ||string.Equals(viewName,"基準メモリ比",StringComparison.Ordinal)){
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
            ORDER BY "RunId" DESC
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

    private static UiBenchmarkGridSideRecord ReadSide(SqliteDataReader reader,int offset){
        var measuredAt = reader.IsDBNull(offset+2)
            ? (DateTimeOffset?)null
            : FromText(reader.GetString(offset+2));
        return new(
            FromStatusText(reader.GetString(offset)),
            reader.GetString(offset+1),
            measuredAt,
            reader.IsDBNull(offset+3) ? null : reader.GetDouble(offset+3),
            reader.IsDBNull(offset+4) ? null : reader.GetInt64(offset+4));
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
