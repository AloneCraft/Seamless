using Microsoft.Data.Sqlite;

namespace Seamless.UI.Storage;

public sealed class UiRunStore{
    private const int SchemaVersion = 5;
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
                    +(SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT IN('実行','ベンチマーク項目','sqlite_sequence'));
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
                "項目ID" INTEGER PRIMARY KEY AUTOINCREMENT,
                "実行ID" INTEGER NOT NULL,
                "タブ名" TEXT NOT NULL,
                "プロジェクト名" TEXT NOT NULL,
                "Assembly" TEXT NOT NULL DEFAULT '',
                "Type" TEXT NOT NULL,
                "型フル名" TEXT NOT NULL,
                "ベンチマーク名" TEXT NOT NULL,
                "選択済み" INTEGER NOT NULL DEFAULT 0,
                "計測状態" TEXT NOT NULL,
                "要素数" INTEGER NULL,
                "ループ回数" INTEGER NULL,
                "CPU番号" INTEGER NULL,
                "引数キー" TEXT NOT NULL DEFAULT '',
                "引数表示" TEXT NOT NULL DEFAULT '',
                "平均表示" TEXT NOT NULL DEFAULT '',
                "平均ns" REAL NULL,
                "誤差表示" TEXT NOT NULL DEFAULT '',
                "誤差ns" REAL NULL,
                "標準偏差表示" TEXT NOT NULL DEFAULT '',
                "標準偏差ns" REAL NULL,
                "割当バイト" INTEGER NULL,
                "計測日時" TEXT NULL,
                "エラー内容" TEXT NOT NULL DEFAULT '',
                "行キー" TEXT NOT NULL DEFAULT '',
                "側" TEXT NOT NULL DEFAULT '',
                "ペア識別子" TEXT NOT NULL DEFAULT '',
                FOREIGN KEY("実行ID") REFERENCES "実行"("実行ID") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_実行_アプリ種別_更新日時" ON "実行"("アプリ種別","更新日時");
            CREATE INDEX IF NOT EXISTS "IX_ベンチマーク項目_実行ID" ON "ベンチマーク項目"("実行ID");
            CREATE UNIQUE INDEX IF NOT EXISTS "UX_ベンチマーク項目_セル" ON "ベンチマーク項目"(
                "実行ID",
                "タブ名",
                "行キー",
                "側",
                "プロジェクト名",
                COALESCE("要素数",-1),
                COALESCE("ループ回数",-1)
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
        var tabName = string.IsNullOrWhiteSpace(sourceTabId) ? "" : sourceTabId;
        var type = ToGenericTypeName(typeFullName);
        using var connection = this.OpenConnection();
        using(var select = connection.CreateCommand()){
            select.CommandText="""
                SELECT "項目ID","行キー","側","ペア識別子","Assembly"
                FROM "ベンチマーク項目"
                WHERE "実行ID"=$runId
                    AND "タブ名"=$tabName
                    AND "プロジェクト名"=$project
                    AND ("Type"=$type OR "型フル名"=$typeFullName)
                    AND "ベンチマーク名"=$method
                    AND "引数キー"=$argumentKey
                    AND ($elementCount IS NULL OR "要素数"=$elementCount OR "要素数" IS NULL)
                    AND ($loopCount IS NULL OR "ループ回数"=$loopCount OR "ループ回数" IS NULL)
                ORDER BY
                    CASE WHEN "要素数"=$elementCount AND "ループ回数"=$loopCount THEN 0 ELSE 1 END,
                    "選択済み" DESC,
                    "項目ID"
                LIMIT 1;
                """;
            select.Parameters.AddWithValue("$runId",runId);
            select.Parameters.AddWithValue("$tabName",tabName);
            select.Parameters.AddWithValue("$project",project);
            select.Parameters.AddWithValue("$type",type);
            select.Parameters.AddWithValue("$typeFullName",typeFullName);
            select.Parameters.AddWithValue("$method",method);
            select.Parameters.AddWithValue("$argumentKey",argumentKey);
            select.Parameters.AddWithValue("$elementCount",ToDbValue(elementCount));
            select.Parameters.AddWithValue("$loopCount",ToDbValue(loopCount));
            using var reader = select.ExecuteReader();
            if(reader.Read()){
                var existingId = reader.GetInt64(0);
                return new(
                    existingId,
                    runId,
                    caseKey,
                    reader.GetString(1),
                    project,
                    reader.GetString(4),
                    method,
                    typeFullName,
                    sourceTabId,
                    argumentKey,
                    reader.GetString(2),
                    reader.GetString(3));
            }
        }

        using var command = connection.CreateCommand();
        command.CommandText="""
            INSERT INTO "ベンチマーク項目"(
                "実行ID","タブ名","プロジェクト名","Assembly","Type","型フル名","ベンチマーク名",
                "選択済み","計測状態","要素数","ループ回数","引数キー","引数表示","行キー","側","ペア識別子")
            VALUES($runId,$tabName,$project,$assembly,$type,$typeFullName,$method,1,$status,$elementCount,$loopCount,$argumentKey,$argumentDisplay,$rowKey,$side,$pairDiscriminator)
            ON CONFLICT("実行ID","タブ名","行キー","側","プロジェクト名",COALESCE("要素数",-1),COALESCE("ループ回数",-1))
            DO UPDATE SET
                "Assembly"=excluded."Assembly",
                "型フル名"=excluded."型フル名",
                "選択済み"=excluded."選択済み",
                "引数表示"=excluded."引数表示",
                "行キー"=excluded."行キー",
                "側"=excluded."側",
                "ペア識別子"=excluded."ペア識別子";
            SELECT "項目ID"
            FROM "ベンチマーク項目"
            WHERE "実行ID"=$runId
                AND "タブ名"=$tabName
                AND "プロジェクト名"=$project
                AND "Type"=$type
                AND "ベンチマーク名"=$method
                AND "引数キー"=$argumentKey
            ORDER BY "項目ID" DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        command.Parameters.AddWithValue("$tabName",tabName);
        command.Parameters.AddWithValue("$project",project);
        command.Parameters.AddWithValue("$assembly",assemblyName);
        command.Parameters.AddWithValue("$type",type);
        command.Parameters.AddWithValue("$typeFullName",typeFullName);
        command.Parameters.AddWithValue("$method",method);
        command.Parameters.AddWithValue("$status",ToStatusText(UiRunStatus.Queued));
        command.Parameters.AddWithValue("$elementCount",ToDbValue(elementCount));
        command.Parameters.AddWithValue("$loopCount",ToDbValue(loopCount));
        command.Parameters.AddWithValue("$argumentKey",argumentKey);
        command.Parameters.AddWithValue("$argumentDisplay",ToArgumentDisplay(arguments));
        command.Parameters.AddWithValue("$rowKey",name);
        command.Parameters.AddWithValue("$side",side);
        command.Parameters.AddWithValue("$pairDiscriminator",pairDiscriminator);
        var caseId = (long)(command.ExecuteScalar()??0L);
        return new(caseId,runId,caseKey,name,project,assemblyName,method,typeFullName,sourceTabId,argumentKey,side,pairDiscriminator);
    }

    public void SaveDetectedBenchmarkItems(long runId,IReadOnlyList<UiBenchmarkItemWrite> items){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach(var item in items){
            using var command = connection.CreateCommand();
            command.Transaction=transaction;
            command.CommandText="""
                INSERT INTO "ベンチマーク項目"(
                    "実行ID","タブ名","プロジェクト名","Assembly","Type","型フル名","ベンチマーク名",
                    "選択済み","計測状態","要素数","ループ回数","CPU番号",
                    "引数キー","引数表示","行キー","側","ペア識別子")
                VALUES(
                    $runId,$tabName,$project,$assembly,$type,$typeFullName,$method,
                    $selected,$status,$elementCount,$loopCount,$cpu,
                    $argumentKey,$argumentDisplay,$rowKey,$side,$pairDiscriminator);
                """;
            command.Parameters.AddWithValue("$runId",runId);
            command.Parameters.AddWithValue("$tabName",item.TabName);
            command.Parameters.AddWithValue("$project",item.ProjectName);
            command.Parameters.AddWithValue("$assembly",item.AssemblyName);
            command.Parameters.AddWithValue("$type",item.TypeName);
            command.Parameters.AddWithValue("$typeFullName",item.TypeFullName);
            command.Parameters.AddWithValue("$method",item.BenchmarkName);
            command.Parameters.AddWithValue("$selected",item.IsSelected ? 1 : 0);
            command.Parameters.AddWithValue("$status","未計測");
            command.Parameters.AddWithValue("$elementCount",ToDbValue(item.ElementCount));
            command.Parameters.AddWithValue("$loopCount",ToDbValue(item.LoopCount));
            command.Parameters.AddWithValue("$cpu",ToDbValue(item.CpuNumber));
            command.Parameters.AddWithValue("$argumentKey",item.ArgumentKey);
            command.Parameters.AddWithValue("$argumentDisplay",item.ArgumentDisplay);
            command.Parameters.AddWithValue("$rowKey",item.RowKey);
            command.Parameters.AddWithValue("$side",item.Side);
            command.Parameters.AddWithValue("$pairDiscriminator",item.PairDiscriminator);
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
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            UPDATE "ベンチマーク項目"
            SET "計測状態"=$status,
                "要素数"=$elementCount,
                "ループ回数"=$loopCount,
                "CPU番号"=$cpu,
                "計測日時"=$measuredAt,
                "エラー内容"=$failureReason
            WHERE "項目ID"=$caseId;
            """;
        command.Parameters.AddWithValue("$caseId",caseId);
        command.Parameters.AddWithValue("$status",ToStatusText(status));
        command.Parameters.AddWithValue("$elementCount",ToDbValue(elementCount));
        command.Parameters.AddWithValue("$loopCount",ToDbValue(loopCount));
        command.Parameters.AddWithValue("$cpu",ToDbValue(assignedLogicalCpu));
        command.Parameters.AddWithValue("$measuredAt",ToDbValue(finishedAt??startedAt));
        command.Parameters.AddWithValue("$failureReason",failureReason);
        command.ExecuteNonQuery();
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
        var mean = GetMetric(metrics,"MeanNanoseconds");
        var error = GetMetric(metrics,"ErrorNanoseconds");
        var stdDev = GetMetric(metrics,"StdDevNanoseconds");
        var allocated = GetMetric(metrics,"AllocatedBytes");
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            UPDATE "ベンチマーク項目"
            SET "平均表示"=$meanText,
                "平均ns"=$mean,
                "誤差表示"=$errorText,
                "誤差ns"=$error,
                "標準偏差表示"=$stdDevText,
                "標準偏差ns"=$stdDev,
                "割当バイト"=$allocated,
                "計測日時"=$measuredAt
            WHERE "項目ID"=$jobId;
            """;
        command.Parameters.AddWithValue("$jobId",jobId);
        command.Parameters.AddWithValue("$meanText",mean is null ? "" : mean.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$mean",ToDbValue(mean));
        command.Parameters.AddWithValue("$errorText",error is null ? "" : error.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$error",ToDbValue(error));
        command.Parameters.AddWithValue("$stdDevText",stdDev is null ? "" : stdDev.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$stdDev",ToDbValue(stdDev));
        command.Parameters.AddWithValue("$allocated",ToDbValue(allocated is null ? null : (long)allocated.Value));
        command.Parameters.AddWithValue("$measuredAt",ToDbValue(measuredAt));
        command.ExecuteNonQuery();
    }

    public void SaveComparisons(long runId,IReadOnlyList<UiComparisonWrite> comparisons){
    }

    public void SaveFallbackDiagnostics(long runId,IReadOnlyList<UiFallbackDiagnosticWrite> diagnostics,DateTimeOffset? recordedAt = null){
    }

    public IReadOnlyList<UiFallbackDiagnosticRecord> ListFallbackDiagnostics(long runId) => [];

    public void SaveUiSelection(long runId,string scope,string selectionKey,bool isSelected){
        this.EnsureSchema();
        if(!string.Equals(scope,"BenchmarkCell",StringComparison.OrdinalIgnoreCase)){
            return;
        }
        var parts = selectionKey.Split('|');
        if(parts.Length<3){
            return;
        }
        var tabName = parts[0];
        var project = parts[^1];
        var rowKey = string.Join("|",parts.Skip(1).Take(parts.Length-2));
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            UPDATE "ベンチマーク項目"
            SET "選択済み"=$selected
            WHERE "実行ID"=$runId
                AND "タブ名"=$tabName
                AND "行キー"=$rowKey
                AND "プロジェクト名"=$project;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        command.Parameters.AddWithValue("$tabName",tabName);
        command.Parameters.AddWithValue("$rowKey",rowKey);
        command.Parameters.AddWithValue("$project",project);
        command.Parameters.AddWithValue("$selected",isSelected ? 1 : 0);
        command.ExecuteNonQuery();
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
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT "項目ID","平均ns","誤差ns","標準偏差ns","割当バイト","計測日時"
            FROM "ベンチマーク項目"
            WHERE "実行ID"=$runId
            ORDER BY "項目ID";
            """;
        command.Parameters.AddWithValue("$runId",runId);
        using var reader = command.ExecuteReader();
        var values = new List<UiMetricValueRecord>();
        while(reader.Read()){
            var jobId = reader.GetInt64(0);
            var measuredAt = reader.IsDBNull(5) ? DateTimeOffset.MinValue : FromText(reader.GetString(5));
            AddMetric(values,jobId,"MeanNanoseconds",reader,1,"ns",measuredAt);
            AddMetric(values,jobId,"ErrorNanoseconds",reader,2,"ns",measuredAt);
            AddMetric(values,jobId,"StdDevNanoseconds",reader,3,"ns",measuredAt);
            AddMetric(values,jobId,"AllocatedBytes",reader,4,"bytes",measuredAt);
        }

        return values;
    }

    public IReadOnlyList<UiRunStateRecord> ListRunStates(long runId) => [];

    public IReadOnlyList<UiCaseRecord> ListCases(long runId){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT "項目ID","実行ID","行キー","プロジェクト名","Assembly","ベンチマーク名","型フル名","タブ名","引数キー","側","ペア識別子"
            FROM "ベンチマーク項目"
            WHERE "実行ID"=$runId
            ORDER BY "項目ID";
            """;
        command.Parameters.AddWithValue("$runId",runId);
        using var reader = command.ExecuteReader();
        var values = new List<UiCaseRecord>();
        while(reader.Read()){
            values.Add(new(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetString(0),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetString(10)));
        }

        return values;
    }

    public IReadOnlyDictionary<string,string> ListCaseArguments(long caseId){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT "引数表示","要素数","ループ回数","タブ名"
            FROM "ベンチマーク項目"
            WHERE "項目ID"=$caseId;
            """;
        command.Parameters.AddWithValue("$caseId",caseId);
        using var reader = command.ExecuteReader();
        if(!reader.Read()){
            return new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        }

        var values = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        if(!reader.IsDBNull(0)&&!string.IsNullOrWhiteSpace(reader.GetString(0))){
            foreach(var item in reader.GetString(0).Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)){
                var separator = item.IndexOf('=');
                if(separator>0&&separator<item.Length-1){
                    values[item[..separator]]=item[(separator+1)..];
                }
            }
        }
        if(!reader.IsDBNull(1)){
            values["ElementCount"]=reader.GetInt64(1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if(!reader.IsDBNull(2)){
            values["LoopCount"]=reader.GetInt64(2).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        values["SourceTab"]=reader.GetString(3);
        return values;
    }

    public IReadOnlyList<UiJobRecord> ListJobs(long runId){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT "項目ID","実行ID","計測状態","要素数","ループ回数","CPU番号","計測日時","エラー内容"
            FROM "ベンチマーク項目"
            WHERE "実行ID"=$runId
            ORDER BY "項目ID";
            """;
        command.Parameters.AddWithValue("$runId",runId);
        using var reader = command.ExecuteReader();
        var values = new List<UiJobRecord>();
        while(reader.Read()){
            var itemId = reader.GetInt64(0);
            var measuredAt = reader.IsDBNull(6) ? (DateTimeOffset?)null : FromText(reader.GetString(6));
            values.Add(new(
                itemId,
                reader.GetInt64(1),
                itemId,
                itemId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                FromStatusText(reader.GetString(2)),
                reader.IsDBNull(3) ? null : reader.GetInt64(3),
                reader.IsDBNull(4) ? null : reader.GetInt64(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                null,
                measuredAt,
                string.Empty,
                reader.GetString(7)));
        }

        return values;
    }

    public IReadOnlyList<UiComparisonRecord> ListComparisons(long runId) => [];

    public IReadOnlyList<UiSelectionRecord> ListUiSelections(long runId){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT "タブ名","行キー","プロジェクト名","選択済み"
            FROM "ベンチマーク項目"
            WHERE "実行ID"=$runId
            ORDER BY "タブ名","行キー","プロジェクト名";
            """;
        command.Parameters.AddWithValue("$runId",runId);
        using var reader = command.ExecuteReader();
        var values = new List<UiSelectionRecord>();
        while(reader.Read()){
            values.Add(new(
                runId,
                "BenchmarkCell",
                reader.GetString(0)+"|"+reader.GetString(1)+"|"+reader.GetString(2),
                reader.GetInt32(3)!=0));
        }
        return values;
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

    private static void AddViewState(List<UiViewStateRecord> values,long runId,string key,string value){
        if(string.IsNullOrWhiteSpace(value)){
            return;
        }
        values.Add(new(runId,key,value));
    }

    private static double? GetMetric(IReadOnlyList<UiMetricValueWrite> metrics,string name) =>
        metrics.LastOrDefault(item => string.Equals(item.MetricName,name,StringComparison.OrdinalIgnoreCase)) is { } value
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
