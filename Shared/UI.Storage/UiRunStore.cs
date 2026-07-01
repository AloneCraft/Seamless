using Microsoft.Data.Sqlite;

namespace Seamless.UI.Storage;

public sealed class UiRunStore{
    private const int SchemaVersion = 2;
    private readonly string databasePath;

    public UiRunStore(string databasePath){
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        this.databasePath=databasePath;
    }

    public string DatabasePath => this.databasePath;

    public void Initialize(){
        Directory.CreateDirectory(Path.GetDirectoryName(this.databasePath)??".");
        using var connection = this.OpenConnection();
        using var transaction = connection.BeginTransaction();
        ExecuteNonQuery(connection,transaction,"""
            CREATE TABLE IF NOT EXISTS SchemaMigrations(
                Version INTEGER PRIMARY KEY,
                AppliedAt TEXT NOT NULL
            );
            """);
        ExecuteNonQuery(connection,transaction,"""
            CREATE TABLE IF NOT EXISTS Apps(
                AppId INTEGER PRIMARY KEY AUTOINCREMENT,
                AppKind TEXT NOT NULL UNIQUE
            );
            """);
        ExecuteNonQuery(connection,transaction,"""
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
            """);
        ExecuteNonQuery(connection,transaction,"""
            CREATE TABLE IF NOT EXISTS RunStates(
                RunStateId INTEGER PRIMARY KEY AUTOINCREMENT,
                RunId INTEGER NOT NULL,
                Status TEXT NOT NULL,
                Message TEXT NOT NULL,
                ChangedAt TEXT NOT NULL,
                FOREIGN KEY(RunId) REFERENCES Runs(RunId) ON DELETE CASCADE
            );
            """);
        ExecuteNonQuery(connection,transaction,"""
            CREATE TABLE IF NOT EXISTS Cases(
                CaseId INTEGER PRIMARY KEY AUTOINCREMENT,
                RunId INTEGER NOT NULL,
                CaseKey TEXT NOT NULL,
                Name TEXT NOT NULL,
                Project TEXT NOT NULL,
                Method TEXT NOT NULL,
                TypeFullName TEXT NOT NULL,
                UNIQUE(RunId,CaseKey),
                FOREIGN KEY(RunId) REFERENCES Runs(RunId) ON DELETE CASCADE
            );
            """);
        ExecuteNonQuery(connection,transaction,"""
            CREATE TABLE IF NOT EXISTS CaseArguments(
                CaseArgumentId INTEGER PRIMARY KEY AUTOINCREMENT,
                CaseId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                Value TEXT NOT NULL,
                UNIQUE(CaseId,Name),
                FOREIGN KEY(CaseId) REFERENCES Cases(CaseId) ON DELETE CASCADE
            );
            """);
        ExecuteNonQuery(connection,transaction,"""
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
            """);
        ExecuteNonQuery(connection,transaction,"""
            CREATE TABLE IF NOT EXISTS Measurements(
                MeasurementId INTEGER PRIMARY KEY AUTOINCREMENT,
                JobId INTEGER NOT NULL,
                MeasuredAt TEXT NOT NULL,
                FOREIGN KEY(JobId) REFERENCES Jobs(JobId) ON DELETE CASCADE
            );
            """);
        ExecuteNonQuery(connection,transaction,"""
            CREATE TABLE IF NOT EXISTS MetricValues(
                MetricValueId INTEGER PRIMARY KEY AUTOINCREMENT,
                MeasurementId INTEGER NOT NULL,
                MetricName TEXT NOT NULL,
                NumericValue REAL NOT NULL,
                Unit TEXT NOT NULL,
                UNIQUE(MeasurementId,MetricName),
                FOREIGN KEY(MeasurementId) REFERENCES Measurements(MeasurementId) ON DELETE CASCADE
            );
            """);
        ExecuteNonQuery(connection,transaction,"""
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
            """);
        ExecuteNonQuery(connection,transaction,"""
            CREATE TABLE IF NOT EXISTS UiSelections(
                UiSelectionId INTEGER PRIMARY KEY AUTOINCREMENT,
                RunId INTEGER NOT NULL,
                Scope TEXT NOT NULL,
                SelectionKey TEXT NOT NULL,
                IsSelected INTEGER NOT NULL,
                UNIQUE(RunId,Scope,SelectionKey),
                FOREIGN KEY(RunId) REFERENCES Runs(RunId) ON DELETE CASCADE
            );
            """);
        ExecuteNonQuery(connection,transaction,"""
            CREATE TABLE IF NOT EXISTS UiViewStates(
                UiViewStateId INTEGER PRIMARY KEY AUTOINCREMENT,
                RunId INTEGER NOT NULL,
                StateKey TEXT NOT NULL,
                StateValue TEXT NOT NULL,
                UNIQUE(RunId,StateKey),
                FOREIGN KEY(RunId) REFERENCES Runs(RunId) ON DELETE CASCADE
            );
            """);
        ExecuteNonQuery(connection,transaction,"""
            CREATE INDEX IF NOT EXISTS IX_Runs_App_Status_UpdatedAt ON Runs(AppId,CurrentStatus,UpdatedAt);
            CREATE INDEX IF NOT EXISTS IX_Jobs_Run_Status ON Jobs(RunId,Status);
            CREATE INDEX IF NOT EXISTS IX_MetricValues_MetricName ON MetricValues(MetricName);
            """);
        EnsureColumn(connection,transaction,"Jobs","LoopCount","INTEGER NULL");
        CreateAnalysisViews(connection,transaction);
        UpsertMigration(connection,transaction,SchemaVersion);
        foreach(var appKind in Enum.GetValues<UiRunAppKind>()){
            EnsureApp(connection,transaction,appKind);
        }

        transaction.Commit();
    }

    public UiRunRecord CreateRun(UiRunCreateRequest request){
        this.Initialize();
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
        IReadOnlyDictionary<string,string>? arguments = null){
        using var connection = this.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using(var command = connection.CreateCommand()){
            command.Transaction=transaction;
            command.CommandText="""
                INSERT INTO Cases(RunId,CaseKey,Name,Project,Method,TypeFullName)
                VALUES($runId,$caseKey,$name,$project,$method,$typeFullName)
                ON CONFLICT(RunId,CaseKey) DO UPDATE SET
                    Name=excluded.Name,
                    Project=excluded.Project,
                    Method=excluded.Method,
                    TypeFullName=excluded.TypeFullName
                RETURNING CaseId;
                """;
            command.Parameters.AddWithValue("$runId",runId);
            command.Parameters.AddWithValue("$caseKey",caseKey);
            command.Parameters.AddWithValue("$name",name);
            command.Parameters.AddWithValue("$project",project);
            command.Parameters.AddWithValue("$method",method);
            command.Parameters.AddWithValue("$typeFullName",typeFullName);
            var caseId = (long)(command.ExecuteScalar()??0L);
            ReplaceCaseArguments(connection,transaction,caseId,arguments);
            transaction.Commit();
            return new(caseId,runId,caseKey,name,project,method,typeFullName);
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

    public void SaveMeasurement(long jobId,IReadOnlyList<UiMetricValueWrite> metrics,DateTimeOffset? measuredAt = null){
        ArgumentNullException.ThrowIfNull(metrics);
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

    public void SaveUiSelection(long runId,string scope,string selectionKey,bool isSelected){
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
        using var connection = this.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText="""
            SELECT CaseId,RunId,CaseKey,Name,Project,Method,TypeFullName
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
                reader.GetString(6)));
        }

        return values;
    }

    public IReadOnlyDictionary<string,string> ListCaseArguments(long caseId){
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

    public IReadOnlyList<UiViewStateRecord> ListUiViewStates(long runId){
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

    private static void EnsureColumn(SqliteConnection connection,SqliteTransaction transaction,string table,string column,string definition){
        using(var command = connection.CreateCommand()){
            command.Transaction=transaction;
            command.CommandText="SELECT COUNT(*) FROM pragma_table_info($table) WHERE name=$column;";
            command.Parameters.AddWithValue("$table",table);
            command.Parameters.AddWithValue("$column",column);
            if((long)(command.ExecuteScalar()??0L)>0){
                return;
            }
        }

        using(var command = connection.CreateCommand()){
            command.Transaction=transaction;
            command.CommandText="ALTER TABLE "+table+" ADD COLUMN "+column+" "+definition+";";
            command.ExecuteNonQuery();
        }
    }

    private static void CreateAnalysisViews(SqliteConnection connection,SqliteTransaction transaction){
        ExecuteNonQuery(connection,transaction,"""
            CREATE VIEW IF NOT EXISTS BenchmarkFlatResults AS
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
        ExecuteNonQuery(connection,transaction,"""
            CREATE VIEW IF NOT EXISTS BenchmarkPairComparisons AS
            SELECT
                leftResult.RunId,
                leftResult.SourceTab,
                leftResult.Method,
                leftResult.TypeFullName,
                leftResult.ElementCount,
                leftResult.LoopCount,
                leftResult.Project AS LeftProject,
                rightResult.Project AS RightProject,
                leftResult.MeanNanoseconds AS LeftMeanNanoseconds,
                rightResult.MeanNanoseconds AS RightMeanNanoseconds,
                CASE
                    WHEN rightResult.MeanNanoseconds IS NULL OR rightResult.MeanNanoseconds=0 THEN NULL
                    ELSE leftResult.MeanNanoseconds/rightResult.MeanNanoseconds
                END AS MeanRatio,
                leftResult.AllocatedBytes AS LeftAllocatedBytes,
                rightResult.AllocatedBytes AS RightAllocatedBytes
            FROM BenchmarkFlatResults leftResult
            INNER JOIN BenchmarkFlatResults rightResult
                ON rightResult.RunId=leftResult.RunId
                AND rightResult.Method=leftResult.Method
                AND rightResult.TypeFullName=leftResult.TypeFullName
                AND COALESCE(rightResult.ElementCount,-1)=COALESCE(leftResult.ElementCount,-1)
                AND COALESCE(rightResult.LoopCount,-1)=COALESCE(leftResult.LoopCount,-1)
                AND rightResult.Project>leftResult.Project
            WHERE leftResult.JobStatus='Completed'
                AND rightResult.JobStatus='Completed'
                AND leftResult.MeanNanoseconds IS NOT NULL
                AND rightResult.MeanNanoseconds IS NOT NULL;
            """);
        ExecuteNonQuery(connection,transaction,"""
            CREATE VIEW IF NOT EXISTS BenchmarkTrendResults AS
            SELECT
                RunId,
                AppKind,
                SourceTab,
                Project,
                Method,
                TypeFullName,
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
    }

    private static void ExecuteNonQuery(SqliteConnection connection,SqliteTransaction transaction,string text){
        using var command = connection.CreateCommand();
        command.Transaction=transaction;
        command.CommandText=text;
        command.ExecuteNonQuery();
    }

    private static object ToDbValue(int? value) => value is { } actual ? actual : DBNull.Value;

    private static object ToDbValue(long? value) => value is { } actual ? actual : DBNull.Value;

    private static object ToDbValue(double? value) => value is { } actual ? actual : DBNull.Value;

    private static object ToDbValue(DateTimeOffset? value) => value is { } actual ? ToText(actual) : DBNull.Value;

    private static string ToText(DateTimeOffset value) => value.ToString("O",System.Globalization.CultureInfo.InvariantCulture);

    private static DateTimeOffset FromText(string value) =>
        DateTimeOffset.Parse(value,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind);
}
