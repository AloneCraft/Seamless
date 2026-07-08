using Microsoft.Data.Sqlite;

namespace Seamless.UI.Storage;

public sealed class UiRunStore{
    private const int SchemaVersion = 7;
    private readonly string databasePath;
    private readonly UiRunSchemaMismatchPolicy mismatchPolicy;
    private readonly object schemaGate = new();
    private bool schemaEnsured;

    public UiRunStore(string databasePath,UiRunSchemaMismatchPolicy mismatchPolicy = UiRunSchemaMismatchPolicy.Throw){
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        this.databasePath=databasePath;
        this.mismatchPolicy=mismatchPolicy;
    }

    public string DatabasePath => this.databasePath;

    public void EnsureSchema(){
        lock(this.schemaGate){
            if(this.schemaEnsured){
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(this.databasePath)??".");
            this.ResolveSchemaMismatch();
            using var connection = this.OpenConnection();
            using var transaction = connection.BeginTransaction();
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

        SqliteConnection.ClearAllPools();
        this.BackupDatabaseFile();
        File.Delete(this.databasePath);
        DeleteIfExists(this.databasePath+"-wal");
        DeleteIfExists(this.databasePath+"-shm");
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
            using var command = connection.CreateCommand();
            command.CommandText="""
                SELECT
                    (SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='実行')
                    +(SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ベンチマーク項目')
                    +(SELECT COUNT(*) FROM sqlite_master WHERE type='view')
                    +(SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT IN('実行','ベンチマーク項目','sqlite_sequence'))
                    +(SELECT COUNT(*) FROM pragma_table_info('ベンチマーク項目') WHERE name IN('左要素数','右要素数'));
                """;
            return (long)(command.ExecuteScalar()??-1L)==2L;
        }
        catch(SqliteException){
            return false;
        }
    }

    private static void DeleteIfExists(string path){
        if(File.Exists(path)){
            File.Delete(path);
        }
    }

    private void BackupDatabaseFile(){
        if(!File.Exists(this.databasePath)){
            return;
        }

        var timestamp = DateTimeOffset.Now.ToString("yyyyMMddHHmmss",System.Globalization.CultureInfo.InvariantCulture);
        var backupPath = this.databasePath+"."+timestamp+".bak";
        File.Copy(this.databasePath,backupPath,overwrite:false);
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
                "作成日時" TEXT NOT NULL,
                "更新日時" TEXT NOT NULL,
                "実行ルート" TEXT NOT NULL DEFAULT '',
                "要素数一覧" TEXT NOT NULL DEFAULT '',
                "Adaptive閾値ns" REAL NOT NULL DEFAULT 0,
                "論理CPU一覧" TEXT NOT NULL DEFAULT '',
                "選択タブ" TEXT NOT NULL DEFAULT '',
                "選択要素数" TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS "ベンチマーク項目"(
                "実行ID" INTEGER NOT NULL,
                "タブ名" TEXT NOT NULL,
                "型" TEXT NOT NULL,
                "ベンチマーク" TEXT NOT NULL,
                "CPU番号" INTEGER NULL,
                "左チェック" INTEGER NOT NULL DEFAULT 0,
                "左状態" TEXT NOT NULL,
                "左失敗理由" TEXT NOT NULL DEFAULT '',
                "左日時" TEXT NULL,
                "左時間ns" REAL NULL,
                "左メモリバイト" INTEGER NULL,
                "右チェック" INTEGER NOT NULL DEFAULT 0,
                "右状態" TEXT NOT NULL,
                "右失敗理由" TEXT NOT NULL DEFAULT '',
                "右日時" TEXT NULL,
                "右時間ns" REAL NULL,
                "右メモリバイト" INTEGER NULL,
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
            """);
    }

    public UiRunRecord CreateRun(UiRunCreateRequest request){
        this.EnsureSchema();
        var now = request.CreatedAt;
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            INSERT INTO "実行"("アプリ種別","表示名","状態","実行プロファイル","タブ名","作成日時","更新日時")
            VALUES($appKind,$displayName,$status,$executionProfile,$sourceTab,$createdAt,$updatedAt);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$appKind",ToAppKindText(request.AppKind));
        command.Parameters.AddWithValue("$displayName",request.DisplayName);
        command.Parameters.AddWithValue("$status",ToStatusText(UiRunStatus.Queued));
        command.Parameters.AddWithValue("$executionProfile",request.ExecutionProfile);
        command.Parameters.AddWithValue("$sourceTab",request.SourceTab);
        command.Parameters.AddWithValue("$createdAt",ToText(now));
        command.Parameters.AddWithValue("$updatedAt",ToText(now));
        var runId = (long)(command.ExecuteScalar()??0L);
        return new(runId,request.AppKind,request.DisplayName,UiRunStatus.Queued,request.ExecutionProfile,request.SourceTab,now,now);
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
        command.ExecuteNonQuery();
    }

    public void SaveRunSettings(long runId,string runRoot,IReadOnlyList<int> elementCounts,double adaptiveThresholdNanoseconds,IReadOnlyList<int> logicalCpus){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            UPDATE "実行"
            SET "実行ルート"=$runRoot,
                "要素数一覧"=$elementCounts,
                "Adaptive閾値ns"=$adaptiveThreshold,
                "論理CPU一覧"=$logicalCpus,
                "更新日時"=$updatedAt
            WHERE "実行ID"=$runId;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        command.Parameters.AddWithValue("$runRoot",runRoot);
        command.Parameters.AddWithValue("$elementCounts",string.Join(",",elementCounts));
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
            SELECT "実行ID","実行ルート","要素数一覧","Adaptive閾値ns","論理CPU一覧"
            FROM "実行"
            WHERE "実行ID"=$runId;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new UiRunSettingsRecord(reader.GetInt64(0),reader.GetString(1),reader.GetString(2),reader.GetDouble(3),reader.GetString(4))
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
                    CreateDetectedSide(left),
                    CreateDetectedSide(right));
            })
            .ToArray();
        this.SaveBenchmarkSnapshotItems(runId,rows);
    }

    public void SaveBenchmarkSnapshotItems(long runId,IReadOnlyList<UiBenchmarkSnapshotItemWrite> items){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using(var delete = connection.CreateCommand()){
            delete.Transaction=transaction;
            delete.CommandText="DELETE FROM \"ベンチマーク項目\" WHERE \"実行ID\"=$runId;";
            delete.Parameters.AddWithValue("$runId",runId);
            delete.ExecuteNonQuery();
        }

        foreach(var item in items){
            using var command = connection.CreateCommand();
            command.Transaction=transaction;
            command.CommandText="""
                INSERT INTO "ベンチマーク項目"(
                    "実行ID","タブ名","型","ベンチマーク","CPU番号",
                    "左チェック","左状態","左失敗理由","左日時","左時間ns","左メモリバイト",
                    "右チェック","右状態","右失敗理由","右日時","右時間ns","右メモリバイト")
                VALUES(
                    $runId,$tabName,$type,$benchmark,$cpu,
                    $leftSelected,$leftStatus,$leftFailure,$leftMeasuredAt,$leftMean,$leftAllocated,
                    $rightSelected,$rightStatus,$rightFailure,$rightMeasuredAt,$rightMean,$rightAllocated);
                """;
            command.Parameters.AddWithValue("$runId",runId);
            command.Parameters.AddWithValue("$tabName",item.TabName);
            command.Parameters.AddWithValue("$type",item.TypeName);
            command.Parameters.AddWithValue("$benchmark",item.BenchmarkName);
            command.Parameters.AddWithValue("$cpu",ToDbValue(item.CpuNumber));
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
        command.CommandText="DELETE FROM \"ベンチマーク項目\";DELETE FROM \"実行\";";
        command.ExecuteNonQuery();
    }

    public void SaveMeasurement(long jobId,IReadOnlyList<UiMetricValueWrite> metrics,DateTimeOffset? measuredAt = null){
        this.EnsureSchema();
    }

    public void SaveComparisons(long runId,IReadOnlyList<UiComparisonWrite> comparisons){
    }

    public void SaveFallbackDiagnostics(long runId,IReadOnlyList<UiFallbackDiagnosticWrite> diagnostics,DateTimeOffset? recordedAt = null){
    }

    public IReadOnlyList<UiFallbackDiagnosticRecord> ListFallbackDiagnostics(long runId) => [];

    public void SaveUiSelection(long runId,string scope,string selectionKey,bool isSelected){
        this.EnsureSchema();
    }

    public void SaveUiViewState(long runId,string stateKey,string stateValue){
        this.EnsureSchema();
        var column = stateKey switch{
            "SelectedSourceTab" => "選択タブ",
            "SelectedResultElementCount" => "選択要素数",
            _ => "",
        };
        if(string.IsNullOrWhiteSpace(column)){
            return;
        }
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText=$"""
            UPDATE "実行"
            SET "{column}"=$value,"更新日時"=$updatedAt
            WHERE "実行ID"=$runId;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        command.Parameters.AddWithValue("$value",stateValue);
        command.Parameters.AddWithValue("$updatedAt",ToText(DateTimeOffset.UtcNow));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<UiRunRecord> ListRuns(UiRunAppKind? appKind = null){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        if(appKind is { } value){
            command.CommandText="""
                SELECT "実行ID","アプリ種別","表示名","状態","実行プロファイル","タブ名","作成日時","更新日時"
                FROM "実行"
                WHERE "アプリ種別"=$appKind
                ORDER BY "更新日時" DESC,"実行ID" DESC;
                """;
            command.Parameters.AddWithValue("$appKind",ToAppKindText(value));
        }else{
            command.CommandText="""
                SELECT "実行ID","アプリ種別","表示名","状態","実行プロファイル","タブ名","作成日時","更新日時"
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
                FromText(reader.GetString(6)),
                FromText(reader.GetString(7))));
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
                "左チェック","左状態","左失敗理由","左日時","左時間ns","左メモリバイト",
                "右チェック","右状態","右失敗理由","右日時","右時間ns","右メモリバイト"
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
                ReadSide(reader,5),
                ReadSide(reader,11)));
        }

        return values;
    }

    public IReadOnlyList<UiComparisonRecord> ListComparisons(long runId) => [];

    public IReadOnlyList<UiSelectionRecord> ListUiSelections(long runId){
        this.EnsureSchema();
        return [];
    }

    public static IReadOnlyList<string> AnalysisViewNames { get; } = [];

    public IReadOnlyList<IReadOnlyDictionary<string,object?>> QueryAnalysisView(string viewName,int limit = 1000) => [];

    public IReadOnlyList<UiViewStateRecord> ListUiViewStates(long runId){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT "選択タブ","選択要素数"
            FROM "実行"
            WHERE "実行ID"=$runId;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        using var reader = command.ExecuteReader();
        if(!reader.Read()){
            return [];
        }
        var values = new List<UiViewStateRecord>();
        AddViewState(values,runId,"SelectedSourceTab",reader.GetString(0));
        AddViewState(values,runId,"SelectedResultElementCount",reader.GetString(1));
        return values;
    }

    private SqliteConnection OpenConnection(){
        var builder = new SqliteConnectionStringBuilder{
            DataSource=this.databasePath,
            Mode=SqliteOpenMode.ReadWriteCreate,
        };
        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText="PRAGMA busy_timeout=5000;PRAGMA journal_mode=WAL;PRAGMA foreign_keys=ON;";
        command.ExecuteNonQuery();
        return connection;
    }

    private SqliteConnection OpenConnectionWithoutCreate(){
        var builder = new SqliteConnectionStringBuilder{
            DataSource=this.databasePath,
            Mode=SqliteOpenMode.ReadWrite,
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

    private static void AddViewState(List<UiViewStateRecord> values,long runId,string key,string value){
        if(string.IsNullOrWhiteSpace(value)){
            return;
        }
        values.Add(new(runId,key,value));
    }

    private static UiBenchmarkGridSideWrite CreateDetectedSide(UiBenchmarkItemWrite? item){
        if(item is null){
            return new(false,UiRunStatus.Queued,string.Empty,null,null,null);
        }

        return new(
            item.IsSelected,
            UiRunStatus.Queued,
            string.Empty,
            null,
            null,
            null);
    }

    private static void AddSideParameters(SqliteCommand command,string prefix,UiBenchmarkGridSideWrite side){
        command.Parameters.AddWithValue("$"+prefix+"Selected",side.IsSelected ? 1 : 0);
        command.Parameters.AddWithValue("$"+prefix+"Status",ToStatusText(side.Status));
        command.Parameters.AddWithValue("$"+prefix+"Failure",side.FailureReason);
        command.Parameters.AddWithValue("$"+prefix+"MeasuredAt",ToDbValue(side.MeasuredAt));
        command.Parameters.AddWithValue("$"+prefix+"Mean",ToDbValue(side.MeanNanoseconds));
        command.Parameters.AddWithValue("$"+prefix+"Allocated",ToDbValue(side.AllocatedBytes));
    }

    private static UiBenchmarkGridSideRecord ReadSide(SqliteDataReader reader,int offset){
        var measuredAt = reader.IsDBNull(offset+3)
            ? (DateTimeOffset?)null
            : FromText(reader.GetString(offset+3));
        return new(
            reader.GetInt32(offset)!=0,
            FromStatusText(reader.GetString(offset+1)),
            reader.GetString(offset+2),
            measuredAt,
            reader.IsDBNull(offset+4) ? null : reader.GetDouble(offset+4),
            reader.IsDBNull(offset+5) ? null : reader.GetInt64(offset+5));
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
