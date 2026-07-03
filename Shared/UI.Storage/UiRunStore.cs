using Microsoft.Data.Sqlite;

namespace Seamless.UI.Storage;

/// <summary>
/// ベンチマーク UI の SQLite 正本ストア。
/// DDL(スキーマ作成/更新と分析 View 再定義)は <see cref="EnsureSchema"/> に集約し、
/// ストアインスタンス生成後の最初の操作で一度だけ実行する。通常の読み書きは DML/SELECT のみ。
/// スキーマ版数不一致時は <see cref="UiRunSchemaMismatchPolicy"/> に従い、
/// 開発用はバックアップリネーム後に新規作成、通常運用は例外で停止する。
/// </summary>
public sealed class UiRunStore{
    private const int SchemaVersion = 3;
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

    /// <summary>
    /// スキーマ作成/更新フェーズ。テーブル・インデックス・分析 View をここでだけ定義する。
    /// View は定義変更を確実に反映するため、このフェーズ内でのみ DROP+CREATE する。
    /// </summary>
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
            CreateAnalysisViews(connection,transaction);
            ExecuteNonQuery(connection,transaction,$"PRAGMA user_version={SchemaVersion};");
            UpsertMigration(connection,transaction,SchemaVersion);
            foreach(var appKind in Enum.GetValues<UiRunAppKind>()){
                EnsureApp(connection,transaction,appKind);
            }
            transaction.Commit();
            this.schemaEnsured=true;
        }
    }

    private void ResolveSchemaMismatch(){
        if(!File.Exists(this.databasePath)){
            return;
        }
        var version = this.ReadStoredSchemaVersion();
        if(version==0||version==SchemaVersion){
            return;
        }
        if(this.mismatchPolicy==UiRunSchemaMismatchPolicy.BackupAndRecreate){
            SqliteConnection.ClearAllPools();
            var backupPath = $"{this.databasePath}.v{version}.{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
            File.Move(this.databasePath,backupPath);
            MoveIfExists(this.databasePath+"-wal",backupPath+"-wal");
            MoveIfExists(this.databasePath+"-shm",backupPath+"-shm");
        }else{
            throw new InvalidOperationException(
                $"UiRunStore schema mismatch: database=v{version} expected=v{SchemaVersion} path={this.databasePath}. "+
                "通常運用ではデータベースを自動破棄しません。移行するか、開発時は UiRunSchemaMismatchPolicy.BackupAndRecreate を指定してください。");
        }
    }

    private long ReadStoredSchemaVersion(){
        using var connection = this.OpenConnection();
        var userVersion = ExecuteScalarLong(connection,"PRAGMA user_version;");
        if(userVersion>0){
            return userVersion;
        }
        // v1/v2 の旧DBは user_version 未設定のため SchemaMigrations から読む。
        var hasMigrations = ExecuteScalarLong(connection,"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SchemaMigrations';")>0;
        if(!hasMigrations){
            return 0;
        }
        return ExecuteScalarLong(connection,"SELECT COALESCE(MAX(Version),0) FROM SchemaMigrations;");
    }

    private static void MoveIfExists(string source,string destination){
        if(File.Exists(source)){
            File.Move(source,destination);
        }
    }

    private static void CreateTables(SqliteConnection connection,SqliteTransaction transaction){
        ExecuteNonQuery(connection,transaction,"""
            CREATE TABLE IF NOT EXISTS SchemaMigrations(
                Version INTEGER PRIMARY KEY,
                AppliedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Apps(
                AppId INTEGER PRIMARY KEY AUTOINCREMENT,
                AppKind TEXT NOT NULL UNIQUE
            );
            CREATE TABLE IF NOT EXISTS Runs(
                RunId INTEGER PRIMARY KEY AUTOINCREMENT,
                AppId INTEGER NOT NULL,
                DisplayName TEXT NOT NULL,
                CurrentStatus TEXT NOT NULL,
                ExecutionProfile TEXT NOT NULL,
                SourceTab TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY(AppId) REFERENCES Apps(AppId)
            );
            CREATE TABLE IF NOT EXISTS RunStates(
                RunStateId INTEGER PRIMARY KEY AUTOINCREMENT,
                RunId INTEGER NOT NULL,
                Status TEXT NOT NULL,
                Message TEXT NOT NULL,
                ChangedAt TEXT NOT NULL,
                FOREIGN KEY(RunId) REFERENCES Runs(RunId) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS Cases(
                CaseId INTEGER PRIMARY KEY AUTOINCREMENT,
                RunId INTEGER NOT NULL,
                CaseKey TEXT NOT NULL,
                Name TEXT NOT NULL,
                Project TEXT NOT NULL,
                Method TEXT NOT NULL,
                TypeFullName TEXT NOT NULL,
                SourceTabId TEXT NOT NULL DEFAULT '',
                ArgumentKey TEXT NOT NULL DEFAULT '',
                Side TEXT NOT NULL DEFAULT '',
                PairDiscriminator TEXT NOT NULL DEFAULT '',
                UNIQUE(RunId,CaseKey),
                FOREIGN KEY(RunId) REFERENCES Runs(RunId) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS CaseArguments(
                CaseArgumentId INTEGER PRIMARY KEY AUTOINCREMENT,
                CaseId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                Value TEXT NOT NULL,
                UNIQUE(CaseId,Name),
                FOREIGN KEY(CaseId) REFERENCES Cases(CaseId) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS Jobs(
                JobId INTEGER PRIMARY KEY AUTOINCREMENT,
                RunId INTEGER NOT NULL,
                CaseId INTEGER NOT NULL,
                JobKey TEXT NOT NULL,
                Status TEXT NOT NULL,
                ElementCount INTEGER NULL,
                LoopCount INTEGER NULL,
                AssignedLogicalCpu INTEGER NULL,
                StartedAt TEXT NULL,
                FinishedAt TEXT NULL,
                FailureKind TEXT NOT NULL,
                FailureReason TEXT NOT NULL,
                UNIQUE(RunId,JobKey),
                FOREIGN KEY(RunId) REFERENCES Runs(RunId) ON DELETE CASCADE,
                FOREIGN KEY(CaseId) REFERENCES Cases(CaseId) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS Measurements(
                MeasurementId INTEGER PRIMARY KEY AUTOINCREMENT,
                JobId INTEGER NOT NULL,
                MeasuredAt TEXT NOT NULL,
                FOREIGN KEY(JobId) REFERENCES Jobs(JobId) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS MetricValues(
                MetricValueId INTEGER PRIMARY KEY AUTOINCREMENT,
                MeasurementId INTEGER NOT NULL,
                MetricName TEXT NOT NULL,
                NumericValue REAL NOT NULL,
                Unit TEXT NOT NULL,
                UNIQUE(MeasurementId,MetricName),
                FOREIGN KEY(MeasurementId) REFERENCES Measurements(MeasurementId) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS Comparisons(
                ComparisonId INTEGER PRIMARY KEY AUTOINCREMENT,
                RunId INTEGER NOT NULL,
                CaseId INTEGER NOT NULL,
                BaselineRunId INTEGER NULL,
                MetricName TEXT NOT NULL,
                Ratio REAL NULL,
                Delta REAL NULL,
                Judgement TEXT NOT NULL,
                UNIQUE(RunId,CaseId,BaselineRunId,MetricName),
                FOREIGN KEY(RunId) REFERENCES Runs(RunId) ON DELETE CASCADE,
                FOREIGN KEY(CaseId) REFERENCES Cases(CaseId) ON DELETE CASCADE,
                FOREIGN KEY(BaselineRunId) REFERENCES Runs(RunId)
            );
            CREATE TABLE IF NOT EXISTS FallbackDiagnostics(
                FallbackDiagnosticId INTEGER PRIMARY KEY AUTOINCREMENT,
                RunId INTEGER NOT NULL,
                Project TEXT NOT NULL,
                Method TEXT NOT NULL,
                CounterName TEXT NOT NULL,
                CounterValue REAL NOT NULL,
                RecordedAt TEXT NOT NULL,
                UNIQUE(RunId,Project,Method,CounterName),
                FOREIGN KEY(RunId) REFERENCES Runs(RunId) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS UiSelections(
                UiSelectionId INTEGER PRIMARY KEY AUTOINCREMENT,
                RunId INTEGER NOT NULL,
                Scope TEXT NOT NULL,
                SelectionKey TEXT NOT NULL,
                IsSelected INTEGER NOT NULL,
                UNIQUE(RunId,Scope,SelectionKey),
                FOREIGN KEY(RunId) REFERENCES Runs(RunId) ON DELETE CASCADE
            );
            CREATE TABLE IF NOT EXISTS UiViewStates(
                UiViewStateId INTEGER PRIMARY KEY AUTOINCREMENT,
                RunId INTEGER NOT NULL,
                StateKey TEXT NOT NULL,
                StateValue TEXT NOT NULL,
                UNIQUE(RunId,StateKey),
                FOREIGN KEY(RunId) REFERENCES Runs(RunId) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_Runs_App_Status_UpdatedAt ON Runs(AppId,CurrentStatus,UpdatedAt);
            CREATE INDEX IF NOT EXISTS IX_Jobs_Run_Status ON Jobs(RunId,Status);
            CREATE INDEX IF NOT EXISTS IX_MetricValues_MetricName ON MetricValues(MetricName);
            CREATE INDEX IF NOT EXISTS IX_Cases_Run_Pair ON Cases(RunId,Method,ArgumentKey,Side);
            """);
    }

    // 分析 View。定義変更を反映するため EnsureSchema の中でのみ DROP+CREATE する。
    // 通常実行時には一切 DDL を発行しない。
    private static void CreateAnalysisViews(SqliteConnection connection,SqliteTransaction transaction){
        ExecuteNonQuery(connection,transaction,"""
            DROP VIEW IF EXISTS BenchmarkSelectedDetail;
            DROP VIEW IF EXISTS BenchmarkFallbackDiagnostics;
            DROP VIEW IF EXISTS BenchmarkMeasurementQuality;
            DROP VIEW IF EXISTS BenchmarkMissingFailureMatrix;
            DROP VIEW IF EXISTS BenchmarkScaleTrend;
            DROP VIEW IF EXISTS BenchmarkHistoryTrend;
            DROP VIEW IF EXISTS BenchmarkCategorySummary;
            DROP VIEW IF EXISTS BenchmarkAllocationRatioWorst;
            DROP VIEW IF EXISTS BenchmarkTimeRatioWorst;
            DROP VIEW IF EXISTS BenchmarkImprovementCandidates;
            DROP VIEW IF EXISTS BenchmarkTrendResults;
            DROP VIEW IF EXISTS BenchmarkPairComparisons;
            DROP VIEW IF EXISTS BenchmarkFlatResults;
            """);
        ExecuteNonQuery(connection,transaction,"""
            CREATE VIEW BenchmarkFlatResults AS
            SELECT
                Runs.RunId,
                Apps.AppKind,
                Runs.DisplayName,
                Runs.ExecutionProfile,
                Runs.SourceTab,
                Runs.CurrentStatus AS RunStatus,
                Cases.CaseId,
                Cases.CaseKey,
                Cases.Name,
                Cases.Project,
                Cases.Method,
                Cases.TypeFullName,
                Cases.SourceTabId,
                Cases.ArgumentKey,
                Cases.Side,
                Cases.PairDiscriminator,
                Jobs.JobId,
                Jobs.JobKey,
                Jobs.Status AS JobStatus,
                Jobs.ElementCount,
                Jobs.LoopCount,
                Jobs.AssignedLogicalCpu,
                Jobs.StartedAt,
                Jobs.FinishedAt,
                Jobs.FailureKind,
                Jobs.FailureReason,
                Measurements.MeasuredAt,
                MAX(CASE WHEN MetricValues.MetricName='MeanNanoseconds' THEN MetricValues.NumericValue END) AS MeanNanoseconds,
                MAX(CASE WHEN MetricValues.MetricName='ErrorNanoseconds' THEN MetricValues.NumericValue END) AS ErrorNanoseconds,
                MAX(CASE WHEN MetricValues.MetricName='StdDevNanoseconds' THEN MetricValues.NumericValue END) AS StdDevNanoseconds,
                MAX(CASE WHEN MetricValues.MetricName='AllocatedBytes' THEN MetricValues.NumericValue END) AS AllocatedBytes
            FROM Jobs
            INNER JOIN Runs ON Runs.RunId=Jobs.RunId
            INNER JOIN Apps ON Apps.AppId=Runs.AppId
            INNER JOIN Cases ON Cases.CaseId=Jobs.CaseId
            LEFT JOIN Measurements ON Measurements.JobId=Jobs.JobId
            LEFT JOIN MetricValues ON MetricValues.MeasurementId=Measurements.MeasurementId
            GROUP BY Jobs.JobId;
            """);
        // Left/Right ペア比較。UI の比較ロジックに合わせ method+argumentKey(+discriminator)+N/Loop で対応付ける。
        // TypeFullName は FLinq/ZLinq で異なるため結合キーに使わない。
        ExecuteNonQuery(connection,transaction,"""
            CREATE VIEW BenchmarkPairComparisons AS
            SELECT
                leftResult.RunId,
                leftResult.AppKind,
                leftResult.SourceTab,
                leftResult.SourceTabId,
                leftResult.RunStatus,
                leftResult.Method,
                leftResult.ArgumentKey,
                leftResult.PairDiscriminator,
                leftResult.ElementCount,
                leftResult.LoopCount,
                leftResult.Project AS LeftProject,
                rightResult.Project AS RightProject,
                leftResult.TypeFullName AS LeftTypeFullName,
                rightResult.TypeFullName AS RightTypeFullName,
                leftResult.MeanNanoseconds AS LeftMeanNanoseconds,
                rightResult.MeanNanoseconds AS RightMeanNanoseconds,
                CASE
                    WHEN rightResult.MeanNanoseconds IS NULL OR rightResult.MeanNanoseconds=0 THEN NULL
                    ELSE leftResult.MeanNanoseconds/rightResult.MeanNanoseconds
                END AS MeanRatio,
                leftResult.AllocatedBytes AS LeftAllocatedBytes,
                rightResult.AllocatedBytes AS RightAllocatedBytes,
                CASE
                    WHEN rightResult.AllocatedBytes IS NULL OR rightResult.AllocatedBytes=0 THEN NULL
                    ELSE leftResult.AllocatedBytes/rightResult.AllocatedBytes
                END AS AllocationRatio,
                leftResult.StdDevNanoseconds AS LeftStdDevNanoseconds,
                rightResult.StdDevNanoseconds AS RightStdDevNanoseconds,
                leftResult.FinishedAt AS LeftFinishedAt,
                rightResult.FinishedAt AS RightFinishedAt
            FROM BenchmarkFlatResults leftResult
            INNER JOIN BenchmarkFlatResults rightResult
                ON rightResult.RunId=leftResult.RunId
                AND leftResult.Side='Left'
                AND rightResult.Side='Right'
                AND rightResult.Method=leftResult.Method
                AND rightResult.ArgumentKey=leftResult.ArgumentKey
                AND rightResult.PairDiscriminator=leftResult.PairDiscriminator
                AND COALESCE(rightResult.ElementCount,-1)=COALESCE(leftResult.ElementCount,-1)
                AND COALESCE(rightResult.LoopCount,-1)=COALESCE(leftResult.LoopCount,-1)
            WHERE leftResult.JobStatus='Completed'
                AND rightResult.JobStatus='Completed'
                AND leftResult.MeanNanoseconds IS NOT NULL
                AND rightResult.MeanNanoseconds IS NOT NULL;
            """);
        ExecuteNonQuery(connection,transaction,"""
            CREATE VIEW BenchmarkTrendResults AS
            SELECT
                RunId,
                AppKind,
                SourceTab,
                Project,
                Method,
                TypeFullName,
                ArgumentKey,
                Side,
                ElementCount,
                LoopCount,
                Name,
                MeanNanoseconds,
                AllocatedBytes,
                FinishedAt,
                MeasuredAt
            FROM BenchmarkFlatResults
            WHERE JobStatus='Completed'
            ORDER BY SourceTab,Project,Method,TypeFullName,ElementCount,LoopCount,FinishedAt;
            """);
        // 改善候補: Completed Run の履歴集計。Left が Right に負けるほどスコアが高い。
        ExecuteNonQuery(connection,transaction,"""
            CREATE VIEW BenchmarkImprovementCandidates AS
            SELECT
                SourceTab,
                Method,
                ArgumentKey,
                PairDiscriminator,
                ElementCount,
                LoopCount,
                LeftProject,
                RightProject,
                COUNT(*) AS SampleCount,
                AVG(MeanRatio) AS AvgMeanRatio,
                MAX(MeanRatio) AS WorstMeanRatio,
                AVG(AllocationRatio) AS AvgAllocationRatio,
                MAX(LeftFinishedAt) AS LastMeasuredAt,
                AVG(MeanRatio)+COALESCE(AVG(AllocationRatio),0)*0.25 AS ImprovementScore
            FROM BenchmarkPairComparisons
            WHERE RunStatus='Completed'
            GROUP BY SourceTab,Method,ArgumentKey,PairDiscriminator,ElementCount,LoopCount,LeftProject,RightProject
            ORDER BY ImprovementScore DESC;
            """);
        ExecuteNonQuery(connection,transaction,"""
            CREATE VIEW BenchmarkTimeRatioWorst AS
            SELECT
                SourceTab,
                Method,
                ArgumentKey,
                PairDiscriminator,
                ElementCount,
                LoopCount,
                AVG(MeanRatio) AS AvgMeanRatio,
                MAX(MeanRatio) AS WorstMeanRatio,
                COUNT(*) AS SampleCount,
                MAX(LeftFinishedAt) AS LastMeasuredAt
            FROM BenchmarkPairComparisons
            WHERE RunStatus='Completed'
            GROUP BY SourceTab,Method,ArgumentKey,PairDiscriminator,ElementCount,LoopCount
            ORDER BY AvgMeanRatio DESC;
            """);
        ExecuteNonQuery(connection,transaction,"""
            CREATE VIEW BenchmarkAllocationRatioWorst AS
            SELECT
                SourceTab,
                Method,
                ArgumentKey,
                PairDiscriminator,
                ElementCount,
                LoopCount,
                AVG(AllocationRatio) AS AvgAllocationRatio,
                MAX(AllocationRatio) AS WorstAllocationRatio,
                AVG(LeftAllocatedBytes) AS AvgLeftAllocatedBytes,
                AVG(RightAllocatedBytes) AS AvgRightAllocatedBytes,
                COUNT(*) AS SampleCount
            FROM BenchmarkPairComparisons
            WHERE RunStatus='Completed' AND AllocationRatio IS NOT NULL
            GROUP BY SourceTab,Method,ArgumentKey,PairDiscriminator,ElementCount,LoopCount
            ORDER BY AvgAllocationRatio DESC;
            """);
        // メソッド名からのカテゴリ分類。分類できないものは Other。
        ExecuteNonQuery(connection,transaction,"""
            CREATE VIEW BenchmarkCategorySummary AS
            SELECT
                SourceTab,
                CASE
                    WHEN Method IN('Sum','Average','Min','Max','MinBy','MaxBy','Aggregate','AggregateBy','Count','LongCount','CountBy','Any','All','Contains','SequenceEqual') THEN 'Aggregate'
                    WHEN Method IN('Distinct','DistinctBy','Union','UnionBy','Intersect','IntersectBy','Except','ExceptBy') THEN 'Set'
                    WHEN Method IN('Select','SelectMany','Cast','OfType','Chunk','Index','Zip') THEN 'Projection'
                    WHEN Method IN('Join','GroupJoin','LeftJoin','RightJoin') THEN 'Join'
                    WHEN Method IN('OrderBy','OrderByDescending','ThenBy','ThenByDescending','Order','OrderDescending','Reverse','Shuffle') THEN 'Order'
                    WHEN Method IN('GroupBy','ToLookup') THEN 'Grouping'
                    WHEN Method IN('Where','Skip','SkipWhile','SkipLast','Take','TakeWhile','TakeLast','First','FirstOrDefault','Last','LastOrDefault','Single','SingleOrDefault','ElementAt','ElementAtOrDefault','DefaultIfEmpty') THEN 'Filter'
                    WHEN Method LIKE 'To%' THEN 'Materialize'
                    ELSE 'Other'
                END AS Category,
                COUNT(*) AS SampleCount,
                AVG(MeanRatio) AS AvgMeanRatio,
                MAX(MeanRatio) AS WorstMeanRatio,
                AVG(AllocationRatio) AS AvgAllocationRatio
            FROM BenchmarkPairComparisons
            WHERE RunStatus='Completed'
            GROUP BY SourceTab,Category
            ORDER BY AvgMeanRatio DESC;
            """);
        // Run 単位の改善/悪化トレンド。
        ExecuteNonQuery(connection,transaction,"""
            CREATE VIEW BenchmarkHistoryTrend AS
            SELECT
                RunId,
                SourceTab,
                Method,
                ArgumentKey,
                PairDiscriminator,
                ElementCount,
                LoopCount,
                MeanRatio,
                AllocationRatio,
                LeftMeanNanoseconds,
                RightMeanNanoseconds,
                LeftFinishedAt
            FROM BenchmarkPairComparisons
            WHERE RunStatus='Completed'
            ORDER BY SourceTab,Method,ArgumentKey,LeftFinishedAt;
            """);
        // ElementCount/LoopCount に対するスケール特性。
        ExecuteNonQuery(connection,transaction,"""
            CREATE VIEW BenchmarkScaleTrend AS
            SELECT
                SourceTab,
                Method,
                ArgumentKey,
                PairDiscriminator,
                ElementCount,
                LoopCount,
                COUNT(*) AS SampleCount,
                AVG(MeanRatio) AS AvgMeanRatio,
                AVG(LeftMeanNanoseconds) AS AvgLeftMeanNanoseconds,
                AVG(RightMeanNanoseconds) AS AvgRightMeanNanoseconds
            FROM BenchmarkPairComparisons
            WHERE RunStatus='Completed'
            GROUP BY SourceTab,Method,ArgumentKey,PairDiscriminator,ElementCount,LoopCount
            ORDER BY SourceTab,Method,ArgumentKey,ElementCount,LoopCount;
            """);
        // 測定失敗・片側欠測・比較不能の行列。
        ExecuteNonQuery(connection,transaction,"""
            CREATE VIEW BenchmarkMissingFailureMatrix AS
            SELECT
                leftResult.RunId,
                leftResult.SourceTab,
                leftResult.Method,
                leftResult.ArgumentKey,
                leftResult.PairDiscriminator,
                leftResult.ElementCount,
                leftResult.LoopCount,
                leftResult.Project,
                leftResult.Side,
                leftResult.JobStatus,
                leftResult.FailureKind,
                leftResult.FailureReason,
                CASE WHEN opposite.JobId IS NULL THEN 1 ELSE 0 END AS OppositeMissing
            FROM BenchmarkFlatResults leftResult
            LEFT JOIN BenchmarkFlatResults opposite
                ON opposite.RunId=leftResult.RunId
                AND opposite.Method=leftResult.Method
                AND opposite.ArgumentKey=leftResult.ArgumentKey
                AND opposite.PairDiscriminator=leftResult.PairDiscriminator
                AND COALESCE(opposite.ElementCount,-1)=COALESCE(leftResult.ElementCount,-1)
                AND COALESCE(opposite.LoopCount,-1)=COALESCE(leftResult.LoopCount,-1)
                AND opposite.Side<>leftResult.Side
                AND opposite.Side IN('Left','Right')
            WHERE leftResult.Side IN('Left','Right')
                AND (leftResult.JobStatus='Failed' OR opposite.JobId IS NULL);
            """);
        // 計測品質。StdDev/Error 比率と外れ値候補。CPU は補助情報に留める。
        ExecuteNonQuery(connection,transaction,"""
            CREATE VIEW BenchmarkMeasurementQuality AS
            SELECT
                RunId,
                SourceTab,
                Project,
                Method,
                ArgumentKey,
                ElementCount,
                LoopCount,
                MeanNanoseconds,
                StdDevNanoseconds,
                ErrorNanoseconds,
                AssignedLogicalCpu,
                CASE WHEN MeanNanoseconds>0 THEN StdDevNanoseconds/MeanNanoseconds END AS StdDevRatio,
                CASE WHEN MeanNanoseconds>0 THEN ErrorNanoseconds/MeanNanoseconds END AS ErrorRatio
            FROM BenchmarkFlatResults
            WHERE JobStatus='Completed' AND MeanNanoseconds IS NOT NULL
            ORDER BY StdDevRatio DESC;
            """);
        // FLinq fallback 実行時診断の集計。
        ExecuteNonQuery(connection,transaction,"""
            CREATE VIEW BenchmarkFallbackDiagnostics AS
            SELECT
                FallbackDiagnostics.RunId,
                Runs.SourceTab,
                FallbackDiagnostics.Project,
                FallbackDiagnostics.Method,
                FallbackDiagnostics.CounterName,
                FallbackDiagnostics.CounterValue,
                FallbackDiagnostics.RecordedAt
            FROM FallbackDiagnostics
            INNER JOIN Runs ON Runs.RunId=FallbackDiagnostics.RunId
            ORDER BY FallbackDiagnostics.CounterValue DESC;
            """);
        // 選択中セル/行の生測定行。SelectionKey は Cases.Name(行キー) または CaseKey に一致する。
        ExecuteNonQuery(connection,transaction,"""
            CREATE VIEW BenchmarkSelectedDetail AS
            SELECT
                UiSelections.RunId,
                UiSelections.Scope,
                UiSelections.SelectionKey,
                BenchmarkFlatResults.CaseKey,
                BenchmarkFlatResults.Project,
                BenchmarkFlatResults.Method,
                BenchmarkFlatResults.ArgumentKey,
                BenchmarkFlatResults.Side,
                BenchmarkFlatResults.ElementCount,
                BenchmarkFlatResults.LoopCount,
                BenchmarkFlatResults.JobStatus,
                BenchmarkFlatResults.MeanNanoseconds,
                BenchmarkFlatResults.AllocatedBytes,
                BenchmarkFlatResults.FinishedAt
            FROM UiSelections
            LEFT JOIN BenchmarkFlatResults
                ON BenchmarkFlatResults.RunId=UiSelections.RunId
                AND (BenchmarkFlatResults.Name=UiSelections.SelectionKey OR BenchmarkFlatResults.CaseKey=UiSelections.SelectionKey)
            WHERE UiSelections.IsSelected=1;
            """);
    }

    public UiRunRecord CreateRun(UiRunCreateRequest request){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var appId = EnsureApp(connection,transaction,request.AppKind);
        var now = request.CreatedAt;
        using(var command = connection.CreateCommand()){
            command.Transaction=transaction;
            command.CommandText="""
                INSERT INTO Runs(AppId,DisplayName,CurrentStatus,ExecutionProfile,SourceTab,CreatedAt,UpdatedAt)
                VALUES($appId,$displayName,$status,$executionProfile,$sourceTab,$createdAt,$updatedAt);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("$appId",appId);
            command.Parameters.AddWithValue("$displayName",request.DisplayName);
            command.Parameters.AddWithValue("$status",UiRunStatus.Queued.ToString());
            command.Parameters.AddWithValue("$executionProfile",request.ExecutionProfile);
            command.Parameters.AddWithValue("$sourceTab",request.SourceTab);
            command.Parameters.AddWithValue("$createdAt",ToText(now));
            command.Parameters.AddWithValue("$updatedAt",ToText(now));
            var runId = (long)(command.ExecuteScalar()??0L);
            InsertRunState(connection,transaction,runId,UiRunStatus.Queued,string.Empty,now);
            transaction.Commit();
            return new(runId,request.AppKind,request.DisplayName,UiRunStatus.Queued,request.ExecutionProfile,request.SourceTab,now,now);
        }
    }

    public void SetRunStatus(long runId,UiRunStatus status,string message = ""){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var now = DateTimeOffset.UtcNow;
        using(var command = connection.CreateCommand()){
            command.Transaction=transaction;
            command.CommandText="""
                UPDATE Runs
                SET CurrentStatus=$status,UpdatedAt=$updatedAt
                WHERE RunId=$runId;
                """;
            command.Parameters.AddWithValue("$status",status.ToString());
            command.Parameters.AddWithValue("$updatedAt",ToText(now));
            command.Parameters.AddWithValue("$runId",runId);
            command.ExecuteNonQuery();
        }

        InsertRunState(connection,transaction,runId,status,message,now);
        transaction.Commit();
    }

    public UiCaseRecord UpsertCase(
        long runId,
        string caseKey,
        string name,
        string project,
        string method,
        string typeFullName,
        IReadOnlyDictionary<string,string>? arguments = null,
        string sourceTabId = "",
        string argumentKey = "",
        string side = "",
        string pairDiscriminator = ""){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using(var command = connection.CreateCommand()){
            command.Transaction=transaction;
            command.CommandText="""
                INSERT INTO Cases(RunId,CaseKey,Name,Project,Method,TypeFullName,SourceTabId,ArgumentKey,Side,PairDiscriminator)
                VALUES($runId,$caseKey,$name,$project,$method,$typeFullName,$sourceTabId,$argumentKey,$side,$pairDiscriminator)
                ON CONFLICT(RunId,CaseKey) DO UPDATE SET
                    Name=excluded.Name,
                    Project=excluded.Project,
                    Method=excluded.Method,
                    TypeFullName=excluded.TypeFullName,
                    SourceTabId=excluded.SourceTabId,
                    ArgumentKey=excluded.ArgumentKey,
                    Side=excluded.Side,
                    PairDiscriminator=excluded.PairDiscriminator
                RETURNING CaseId;
                """;
            command.Parameters.AddWithValue("$runId",runId);
            command.Parameters.AddWithValue("$caseKey",caseKey);
            command.Parameters.AddWithValue("$name",name);
            command.Parameters.AddWithValue("$project",project);
            command.Parameters.AddWithValue("$method",method);
            command.Parameters.AddWithValue("$typeFullName",typeFullName);
            command.Parameters.AddWithValue("$sourceTabId",sourceTabId);
            command.Parameters.AddWithValue("$argumentKey",argumentKey);
            command.Parameters.AddWithValue("$side",side);
            command.Parameters.AddWithValue("$pairDiscriminator",pairDiscriminator);
            var caseId = (long)(command.ExecuteScalar()??0L);
            ReplaceCaseArguments(connection,transaction,caseId,arguments);
            transaction.Commit();
            return new(caseId,runId,caseKey,name,project,method,typeFullName,sourceTabId,argumentKey,side,pairDiscriminator);
        }
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
        string failureKind = "",
        string failureReason = ""){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            INSERT INTO Jobs(RunId,CaseId,JobKey,Status,ElementCount,LoopCount,AssignedLogicalCpu,StartedAt,FinishedAt,FailureKind,FailureReason)
            VALUES($runId,$caseId,$jobKey,$status,$elementCount,$loopCount,$assignedLogicalCpu,$startedAt,$finishedAt,$failureKind,$failureReason)
            ON CONFLICT(RunId,JobKey) DO UPDATE SET
                CaseId=excluded.CaseId,
                Status=excluded.Status,
                ElementCount=excluded.ElementCount,
                LoopCount=excluded.LoopCount,
                AssignedLogicalCpu=excluded.AssignedLogicalCpu,
                StartedAt=excluded.StartedAt,
                FinishedAt=excluded.FinishedAt,
                FailureKind=excluded.FailureKind,
                FailureReason=excluded.FailureReason
            RETURNING JobId;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        command.Parameters.AddWithValue("$caseId",caseId);
        command.Parameters.AddWithValue("$jobKey",jobKey);
        command.Parameters.AddWithValue("$status",status.ToString());
        command.Parameters.AddWithValue("$elementCount",ToDbValue(elementCount));
        command.Parameters.AddWithValue("$loopCount",ToDbValue(loopCount));
        command.Parameters.AddWithValue("$assignedLogicalCpu",ToDbValue(assignedLogicalCpu));
        command.Parameters.AddWithValue("$startedAt",ToDbValue(startedAt));
        command.Parameters.AddWithValue("$finishedAt",ToDbValue(finishedAt));
        command.Parameters.AddWithValue("$failureKind",failureKind);
        command.Parameters.AddWithValue("$failureReason",failureReason);
        var jobId = (long)(command.ExecuteScalar()??0L);
        return new(jobId,runId,caseId,jobKey,status,elementCount,loopCount,assignedLogicalCpu,startedAt,finishedAt,failureKind,failureReason);
    }

    /// <summary>
    /// UI ストア内の全テーブルのレコードを削除します。
    /// スキーマ（テーブル定義/ビュー）とインデックスは保持されます。
    /// </summary>
    public void DeleteAllRecords(){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var transaction = connection.BeginTransaction();
        ExecuteNonQuery(connection,transaction,"DELETE FROM MetricValues;");
        ExecuteNonQuery(connection,transaction,"DELETE FROM Measurements;");
        ExecuteNonQuery(connection,transaction,"DELETE FROM Comparisons;");
        ExecuteNonQuery(connection,transaction,"DELETE FROM FallbackDiagnostics;");
        ExecuteNonQuery(connection,transaction,"DELETE FROM UiSelections;");
        ExecuteNonQuery(connection,transaction,"DELETE FROM UiViewStates;");
        ExecuteNonQuery(connection,transaction,"DELETE FROM RunStates;");
        ExecuteNonQuery(connection,transaction,"DELETE FROM Jobs;");
        ExecuteNonQuery(connection,transaction,"DELETE FROM CaseArguments;");
        ExecuteNonQuery(connection,transaction,"DELETE FROM Cases;");
        ExecuteNonQuery(connection,transaction,"DELETE FROM Runs;");
        ExecuteNonQuery(connection,transaction,"DELETE FROM Apps;");
        ExecuteNonQuery(connection,transaction,"DELETE FROM SchemaMigrations;");
        ExecuteNonQuery(connection,transaction,"DELETE FROM sqlite_sequence;");
        transaction.Commit();
    }

    public void SaveMeasurement(long jobId,IReadOnlyList<UiMetricValueWrite> metrics,DateTimeOffset? measuredAt = null){
        ArgumentNullException.ThrowIfNull(metrics);
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var actualMeasuredAt = measuredAt??DateTimeOffset.UtcNow;
        using(var delete = connection.CreateCommand()){
            delete.Transaction=transaction;
            delete.CommandText="""
                DELETE FROM Measurements
                WHERE JobId=$jobId;
                """;
            delete.Parameters.AddWithValue("$jobId",jobId);
            delete.ExecuteNonQuery();
        }

        long measurementId;
        using(var command = connection.CreateCommand()){
            command.Transaction=transaction;
            command.CommandText="""
                INSERT INTO Measurements(JobId,MeasuredAt)
                VALUES($jobId,$measuredAt);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("$jobId",jobId);
            command.Parameters.AddWithValue("$measuredAt",ToText(actualMeasuredAt));
            measurementId=(long)(command.ExecuteScalar()??0L);
        }

        foreach(var metric in metrics){
            using var command = connection.CreateCommand();
            command.Transaction=transaction;
            command.CommandText="""
                INSERT INTO MetricValues(MeasurementId,MetricName,NumericValue,Unit)
                VALUES($measurementId,$metricName,$numericValue,$unit);
                """;
            command.Parameters.AddWithValue("$measurementId",measurementId);
            command.Parameters.AddWithValue("$metricName",metric.MetricName);
            command.Parameters.AddWithValue("$numericValue",metric.NumericValue);
            command.Parameters.AddWithValue("$unit",metric.Unit);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void SaveComparisons(long runId,IReadOnlyList<UiComparisonWrite> comparisons){
        ArgumentNullException.ThrowIfNull(comparisons);
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using(var delete = connection.CreateCommand()){
            delete.Transaction=transaction;
            delete.CommandText="DELETE FROM Comparisons WHERE RunId=$runId;";
            delete.Parameters.AddWithValue("$runId",runId);
            delete.ExecuteNonQuery();
        }

        foreach(var comparison in comparisons){
            using var command = connection.CreateCommand();
            command.Transaction=transaction;
            command.CommandText="""
                INSERT INTO Comparisons(RunId,CaseId,BaselineRunId,MetricName,Ratio,Delta,Judgement)
                VALUES($runId,$caseId,$baselineRunId,$metricName,$ratio,$delta,$judgement);
                """;
            command.Parameters.AddWithValue("$runId",runId);
            command.Parameters.AddWithValue("$caseId",comparison.CaseId);
            command.Parameters.AddWithValue("$baselineRunId",ToDbValue(comparison.BaselineRunId));
            command.Parameters.AddWithValue("$metricName",comparison.MetricName);
            command.Parameters.AddWithValue("$ratio",ToDbValue(comparison.Ratio));
            command.Parameters.AddWithValue("$delta",ToDbValue(comparison.Delta));
            command.Parameters.AddWithValue("$judgement",comparison.Judgement);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    /// <summary>FLinq fallback 実行時診断カウンタを Run 単位で upsert する。</summary>
    public void SaveFallbackDiagnostics(long runId,IReadOnlyList<UiFallbackDiagnosticWrite> diagnostics,DateTimeOffset? recordedAt = null){
        ArgumentNullException.ThrowIfNull(diagnostics);
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var actualRecordedAt = recordedAt??DateTimeOffset.UtcNow;
        foreach(var diagnostic in diagnostics){
            using var command = connection.CreateCommand();
            command.Transaction=transaction;
            command.CommandText="""
                INSERT INTO FallbackDiagnostics(RunId,Project,Method,CounterName,CounterValue,RecordedAt)
                VALUES($runId,$project,$method,$counterName,$counterValue,$recordedAt)
                ON CONFLICT(RunId,Project,Method,CounterName) DO UPDATE SET
                    CounterValue=excluded.CounterValue,
                    RecordedAt=excluded.RecordedAt;
                """;
            command.Parameters.AddWithValue("$runId",runId);
            command.Parameters.AddWithValue("$project",diagnostic.Project);
            command.Parameters.AddWithValue("$method",diagnostic.Method);
            command.Parameters.AddWithValue("$counterName",diagnostic.CounterName);
            command.Parameters.AddWithValue("$counterValue",diagnostic.CounterValue);
            command.Parameters.AddWithValue("$recordedAt",ToText(actualRecordedAt));
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public IReadOnlyList<UiFallbackDiagnosticRecord> ListFallbackDiagnostics(long runId){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT RunId,Project,Method,CounterName,CounterValue,RecordedAt
            FROM FallbackDiagnostics
            WHERE RunId=$runId
            ORDER BY Project,Method,CounterName;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        using var reader = command.ExecuteReader();
        var values = new List<UiFallbackDiagnosticRecord>();
        while(reader.Read()){
            values.Add(new(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetDouble(4),
                FromText(reader.GetString(5))));
        }

        return values;
    }

    public void SaveUiSelection(long runId,string scope,string selectionKey,bool isSelected){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            INSERT INTO UiSelections(RunId,Scope,SelectionKey,IsSelected)
            VALUES($runId,$scope,$selectionKey,$isSelected)
            ON CONFLICT(RunId,Scope,SelectionKey) DO UPDATE SET
                IsSelected=excluded.IsSelected;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        command.Parameters.AddWithValue("$scope",scope);
        command.Parameters.AddWithValue("$selectionKey",selectionKey);
        command.Parameters.AddWithValue("$isSelected",isSelected ? 1 : 0);
        command.ExecuteNonQuery();
    }

    public void SaveUiViewState(long runId,string stateKey,string stateValue){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            INSERT INTO UiViewStates(RunId,StateKey,StateValue)
            VALUES($runId,$stateKey,$stateValue)
            ON CONFLICT(RunId,StateKey) DO UPDATE SET
                StateValue=excluded.StateValue;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        command.Parameters.AddWithValue("$stateKey",stateKey);
        command.Parameters.AddWithValue("$stateValue",stateValue);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<UiRunRecord> ListRuns(UiRunAppKind? appKind = null){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText=appKind is null
            ? """
                SELECT Runs.RunId,Apps.AppKind,Runs.DisplayName,Runs.CurrentStatus,Runs.ExecutionProfile,Runs.SourceTab,Runs.CreatedAt,Runs.UpdatedAt
                FROM Runs
                INNER JOIN Apps ON Apps.AppId=Runs.AppId
                ORDER BY Runs.UpdatedAt DESC,Runs.RunId DESC;
                """
            : """
                SELECT Runs.RunId,Apps.AppKind,Runs.DisplayName,Runs.CurrentStatus,Runs.ExecutionProfile,Runs.SourceTab,Runs.CreatedAt,Runs.UpdatedAt
                FROM Runs
                INNER JOIN Apps ON Apps.AppId=Runs.AppId
                WHERE Apps.AppKind=$appKind
                ORDER BY Runs.UpdatedAt DESC,Runs.RunId DESC;
                """;
        if(appKind is { } value){
            command.Parameters.AddWithValue("$appKind",value.ToString());
        }

        using var reader = command.ExecuteReader();
        var runs = new List<UiRunRecord>();
        while(reader.Read()){
            runs.Add(new(
                reader.GetInt64(0),
                Enum.Parse<UiRunAppKind>(reader.GetString(1)),
                reader.GetString(2),
                Enum.Parse<UiRunStatus>(reader.GetString(3)),
                reader.GetString(4),
                reader.GetString(5),
                FromText(reader.GetString(6)),
                FromText(reader.GetString(7))));
        }

        return runs;
    }

    public IReadOnlyList<UiMetricValueRecord> ListMetrics(long runId){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT Jobs.JobId,MetricValues.MetricName,MetricValues.NumericValue,MetricValues.Unit,Measurements.MeasuredAt
            FROM MetricValues
            INNER JOIN Measurements ON Measurements.MeasurementId=MetricValues.MeasurementId
            INNER JOIN Jobs ON Jobs.JobId=Measurements.JobId
            WHERE Jobs.RunId=$runId
            ORDER BY Jobs.JobId,Measurements.MeasuredAt,MetricValues.MetricName;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        using var reader = command.ExecuteReader();
        var values = new List<UiMetricValueRecord>();
        while(reader.Read()){
            values.Add(new(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetDouble(2),
                reader.GetString(3),
                FromText(reader.GetString(4))));
        }

        return values;
    }

    public IReadOnlyList<UiRunStateRecord> ListRunStates(long runId){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT RunStateId,RunId,Status,Message,ChangedAt
            FROM RunStates
            WHERE RunId=$runId
            ORDER BY ChangedAt,RunStateId;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        using var reader = command.ExecuteReader();
        var values = new List<UiRunStateRecord>();
        while(reader.Read()){
            values.Add(new(
                reader.GetInt64(0),
                reader.GetInt64(1),
                Enum.Parse<UiRunStatus>(reader.GetString(2)),
                reader.GetString(3),
                FromText(reader.GetString(4))));
        }

        return values;
    }

    public IReadOnlyList<UiCaseRecord> ListCases(long runId){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT CaseId,RunId,CaseKey,Name,Project,Method,TypeFullName,SourceTabId,ArgumentKey,Side,PairDiscriminator
            FROM Cases
            WHERE RunId=$runId
            ORDER BY CaseId;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        using var reader = command.ExecuteReader();
        var values = new List<UiCaseRecord>();
        while(reader.Read()){
            values.Add(new(
                reader.GetInt64(0),
                reader.GetInt64(1),
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
            SELECT Name,Value
            FROM CaseArguments
            WHERE CaseId=$caseId
            ORDER BY Name;
            """;
        command.Parameters.AddWithValue("$caseId",caseId);
        using var reader = command.ExecuteReader();
        var values = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        while(reader.Read()){
            values[reader.GetString(0)]=reader.GetString(1);
        }

        return values;
    }

    public IReadOnlyList<UiJobRecord> ListJobs(long runId){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT JobId,RunId,CaseId,JobKey,Status,ElementCount,LoopCount,AssignedLogicalCpu,StartedAt,FinishedAt,FailureKind,FailureReason
            FROM Jobs
            WHERE RunId=$runId
            ORDER BY JobId;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        using var reader = command.ExecuteReader();
        var values = new List<UiJobRecord>();
        while(reader.Read()){
            values.Add(new(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetInt64(2),
                reader.GetString(3),
                Enum.Parse<UiRunStatus>(reader.GetString(4)),
                reader.IsDBNull(5) ? null : reader.GetInt64(5),
                reader.IsDBNull(6) ? null : reader.GetInt64(6),
                reader.IsDBNull(7) ? null : reader.GetInt32(7),
                reader.IsDBNull(8) ? null : FromText(reader.GetString(8)),
                reader.IsDBNull(9) ? null : FromText(reader.GetString(9)),
                reader.GetString(10),
                reader.GetString(11)));
        }

        return values;
    }

    public IReadOnlyList<UiComparisonRecord> ListComparisons(long runId){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT RunId,CaseId,BaselineRunId,MetricName,Ratio,Delta,Judgement
            FROM Comparisons
            WHERE RunId=$runId
            ORDER BY CaseId,MetricName;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        using var reader = command.ExecuteReader();
        var values = new List<UiComparisonRecord>();
        while(reader.Read()){
            values.Add(new(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.IsDBNull(2) ? null : reader.GetInt64(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetDouble(4),
                reader.IsDBNull(5) ? null : reader.GetDouble(5),
                reader.GetString(6)));
        }

        return values;
    }

    public IReadOnlyList<UiSelectionRecord> ListUiSelections(long runId){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT RunId,Scope,SelectionKey,IsSelected
            FROM UiSelections
            WHERE RunId=$runId
            ORDER BY Scope,SelectionKey;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        using var reader = command.ExecuteReader();
        var values = new List<UiSelectionRecord>();
        while(reader.Read()){
            values.Add(new(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3)!=0));
        }

        return values;
    }

    /// <summary>右ペイン分析タブが参照する分析 View の定義順一覧。</summary>
    public static IReadOnlyList<string> AnalysisViewNames { get; } = [
        "BenchmarkImprovementCandidates",
        "BenchmarkTimeRatioWorst",
        "BenchmarkAllocationRatioWorst",
        "BenchmarkCategorySummary",
        "BenchmarkHistoryTrend",
        "BenchmarkScaleTrend",
        "BenchmarkMissingFailureMatrix",
        "BenchmarkMeasurementQuality",
        "BenchmarkFallbackDiagnostics",
        "BenchmarkSelectedDetail",
        "BenchmarkPairComparisons",
        "BenchmarkTrendResults",
        "BenchmarkFlatResults",
    ];

    /// <summary>
    /// 分析 View を汎用行として読み出す。View 名はホワイトリスト検証するため SQL は固定形。
    /// UI の分析タブが表/チャートを構築するために使う(SELECT のみ)。
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string,object?>> QueryAnalysisView(string viewName,int limit = 1000){
        if(!AnalysisViewNames.Contains(viewName,StringComparer.Ordinal)){
            throw new ArgumentOutOfRangeException(nameof(viewName),viewName,"未知の分析 View です。");
        }
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText=$"SELECT * FROM {viewName} LIMIT $limit;";
        command.Parameters.AddWithValue("$limit",limit);
        using var reader = command.ExecuteReader();
        var rows = new List<IReadOnlyDictionary<string,object?>>();
        while(reader.Read()){
            var row = new Dictionary<string,object?>(reader.FieldCount,StringComparer.OrdinalIgnoreCase);
            for(var index = 0; index<reader.FieldCount; index++){
                if(reader.IsDBNull(index)){
                    row[reader.GetName(index)]=null;
                }else{
                    row[reader.GetName(index)]=reader.GetValue(index);
                }
            }
            rows.Add(row);
        }

        return rows;
    }

    public IReadOnlyList<UiViewStateRecord> ListUiViewStates(long runId){
        this.EnsureSchema();
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT RunId,StateKey,StateValue
            FROM UiViewStates
            WHERE RunId=$runId
            ORDER BY StateKey;
            """;
        command.Parameters.AddWithValue("$runId",runId);
        using var reader = command.ExecuteReader();
        var values = new List<UiViewStateRecord>();
        while(reader.Read()){
            values.Add(new(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2)));
        }

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
        command.CommandText="PRAGMA foreign_keys=ON;";
        command.ExecuteNonQuery();
        return connection;
    }

    private static long EnsureApp(SqliteConnection connection,SqliteTransaction transaction,UiRunAppKind appKind){
        using(var command = connection.CreateCommand()){
            command.Transaction=transaction;
            command.CommandText="""
                INSERT INTO Apps(AppKind)
                VALUES($appKind)
                ON CONFLICT(AppKind) DO NOTHING;
                """;
            command.Parameters.AddWithValue("$appKind",appKind.ToString());
            command.ExecuteNonQuery();
        }

        using(var command = connection.CreateCommand()){
            command.Transaction=transaction;
            command.CommandText="SELECT AppId FROM Apps WHERE AppKind=$appKind;";
            command.Parameters.AddWithValue("$appKind",appKind.ToString());
            return (long)(command.ExecuteScalar()??0L);
        }
    }

    private static void ReplaceCaseArguments(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long caseId,
        IReadOnlyDictionary<string,string>? arguments){
        using(var delete = connection.CreateCommand()){
            delete.Transaction=transaction;
            delete.CommandText="DELETE FROM CaseArguments WHERE CaseId=$caseId;";
            delete.Parameters.AddWithValue("$caseId",caseId);
            delete.ExecuteNonQuery();
        }

        if(arguments is null){
            return;
        }

        foreach(var argument in arguments.OrderBy(static item => item.Key,StringComparer.OrdinalIgnoreCase)){
            using var command = connection.CreateCommand();
            command.Transaction=transaction;
            command.CommandText="""
                INSERT INTO CaseArguments(CaseId,Name,Value)
                VALUES($caseId,$name,$value);
                """;
            command.Parameters.AddWithValue("$caseId",caseId);
            command.Parameters.AddWithValue("$name",argument.Key);
            command.Parameters.AddWithValue("$value",argument.Value);
            command.ExecuteNonQuery();
        }
    }

    private static void InsertRunState(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long runId,
        UiRunStatus status,
        string message,
        DateTimeOffset changedAt){
        using var command = connection.CreateCommand();
        command.Transaction=transaction;
        command.CommandText="""
            INSERT INTO RunStates(RunId,Status,Message,ChangedAt)
            VALUES($runId,$status,$message,$changedAt);
            """;
        command.Parameters.AddWithValue("$runId",runId);
        command.Parameters.AddWithValue("$status",status.ToString());
        command.Parameters.AddWithValue("$message",message);
        command.Parameters.AddWithValue("$changedAt",ToText(changedAt));
        command.ExecuteNonQuery();
    }

    private static void UpsertMigration(SqliteConnection connection,SqliteTransaction transaction,int version){
        using var command = connection.CreateCommand();
        command.Transaction=transaction;
        command.CommandText="""
            INSERT INTO SchemaMigrations(Version,AppliedAt)
            VALUES($version,$appliedAt)
            ON CONFLICT(Version) DO NOTHING;
            """;
        command.Parameters.AddWithValue("$version",version);
        command.Parameters.AddWithValue("$appliedAt",ToText(DateTimeOffset.UtcNow));
        command.ExecuteNonQuery();
    }

    private static void ExecuteNonQuery(SqliteConnection connection,SqliteTransaction transaction,string text){
        using var command = connection.CreateCommand();
        command.Transaction=transaction;
        command.CommandText=text;
        command.ExecuteNonQuery();
    }

    private static long ExecuteScalarLong(SqliteConnection connection,string text){
        using var command = connection.CreateCommand();
        command.CommandText=text;
        return (long)(command.ExecuteScalar()??0L);
    }

    private static object ToDbValue(int? value) => value is { } actual ? actual : DBNull.Value;

    private static object ToDbValue(long? value) => value is { } actual ? actual : DBNull.Value;

    private static object ToDbValue(double? value) => value is { } actual ? actual : DBNull.Value;

    private static object ToDbValue(DateTimeOffset? value) => value is { } actual ? ToText(actual) : DBNull.Value;

    private static string ToText(DateTimeOffset value) => value.ToString("O",System.Globalization.CultureInfo.InvariantCulture);

    private static DateTimeOffset FromText(string value) =>
        DateTimeOffset.Parse(value,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind);
}
