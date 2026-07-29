using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Benchmark.Dynamic.Infrastructure;

public sealed class DynamicRepository{
    private readonly DynamicDatabase database;
    private readonly DynamicSingleWriterQueue writer;

    public DynamicRepository(DynamicDatabase database,DynamicSingleWriterQueue writer){
        this.database=database??throw new ArgumentNullException(nameof(database));
        this.writer=writer??throw new ArgumentNullException(nameof(writer));
    }

    public async Task SaveRunPlanAsync(
        DynamicRunRecord run,
        IReadOnlyList<DynamicRunSourceRecord> sources,
        IReadOnlyList<DynamicQueryRecord> queries,
        CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(queries);
        await this.writer.ExecuteAsync((connection,transaction)=>{
            UpsertRun(connection,transaction,run);
            foreach(var source in sources){
                UpsertRunSource(connection,transaction,source);
            }
            foreach(var query in queries){
                UpsertQuery(connection,transaction,query);
            }
            return true;
        },cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveRunAsync(
        DynamicRunRecord run,
        CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(run);
        await this.writer.ExecuteAsync((connection,transaction)=>{
            UpsertRun(connection,transaction,run);
            return true;
        },cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveRunSourceAsync(
        DynamicRunSourceRecord source,
        CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(source);
        await this.writer.ExecuteAsync((connection,transaction)=>{
            UpsertRunSource(connection,transaction,source);
            return true;
        },cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveQueryAsync(
        DynamicQueryRecord query,
        CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(query);
        await this.writer.ExecuteAsync((connection,transaction)=>{
            UpsertQuery(connection,transaction,query);
            return true;
        },cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveSourceExecutionAsync(
        DynamicSourceExecutionRecord execution,
        CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(execution);
        await this.writer.ExecuteAsync((connection,transaction)=>{
            UpsertSourceExecution(connection,transaction,execution);
            return true;
        },cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveValidationAsync(
        DynamicValidationRecord validation,
        CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(validation);
        await this.writer.ExecuteAsync((connection,transaction)=>{
            UpsertValidation(connection,transaction,validation);
            return true;
        },cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveMeasurementAsync(
        DynamicMeasurementBundle bundle,
        CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(bundle);
        await this.writer.ExecuteAsync((connection,transaction)=>{
            ReplaceMeasurementBundle(connection,transaction,bundle);
            return true;
        },cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveDiagnosticAsync(
        DynamicDiagnosticRecord diagnostic,
        CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(diagnostic);
        await this.writer.ExecuteAsync((connection,transaction)=>{
            UpsertDiagnostic(connection,transaction,diagnostic);
            return true;
        },cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveOptimizationEventAsync(
        DynamicOptimizationEventRecord optimizationEvent,
        CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(optimizationEvent);
        await this.writer.ExecuteAsync((connection,transaction)=>{
            UpsertOptimizationEvent(connection,transaction,optimizationEvent);
            return true;
        },cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveRunEventAsync(
        DynamicRunEventRecord runEvent,
        CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(runEvent);
        await this.writer.ExecuteAsync((connection,transaction)=>{
            UpsertRunEvent(connection,transaction,runEvent);
            return true;
        },cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveCheckpointAsync(
        DynamicRunCheckpoint checkpoint,
        CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(checkpoint);
        await this.writer.ExecuteAsync((connection,transaction)=>{
            UpsertRun(connection,transaction,checkpoint.Run);
            foreach(var source in checkpoint.Sources??[]){
                UpsertRunSource(connection,transaction,source);
            }
            foreach(var query in checkpoint.Queries??[]){
                UpsertQuery(connection,transaction,query);
            }
            foreach(var execution in checkpoint.SourceExecutions??[]){
                UpsertSourceExecution(connection,transaction,execution);
            }
            foreach(var validation in checkpoint.Validations??[]){
                UpsertValidation(connection,transaction,validation);
            }
            foreach(var measurement in checkpoint.Measurements??[]){
                ReplaceMeasurementBundle(connection,transaction,measurement);
            }
            foreach(var diagnostic in checkpoint.Diagnostics??[]){
                UpsertDiagnostic(connection,transaction,diagnostic);
            }
            foreach(var optimizationEvent in checkpoint.OptimizationEvents??[]){
                UpsertOptimizationEvent(connection,transaction,optimizationEvent);
            }
            foreach(var runEvent in checkpoint.RunEvents??[]){
                UpsertRunEvent(connection,transaction,runEvent);
            }
            return true;
        },cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> SoftDeleteRunAsync(
        string runID,
        DateTimeOffset deletedAt,
        string reason="",
        CancellationToken cancellationToken=default){
        ArgumentException.ThrowIfNullOrWhiteSpace(runID);
        return this.writer.ExecuteAsync((connection,transaction)=>{
            using var command=CreateCommand(connection,transaction,
                """
                UPDATE "DynamicRuns"
                SET "deleted_at"=$deleted_at,
                    "deleted_reason"=$deleted_reason,
                    "updated_at"=$updated_at
                WHERE "run_id"=$run_id AND "deleted_at" IS NULL;
                """);
            Add(command,"$deleted_at",Format(deletedAt));
            Add(command,"$deleted_reason",reason);
            Add(command,"$updated_at",Format(deletedAt));
            Add(command,"$run_id",runID);
            return command.ExecuteNonQuery()==1;
        },cancellationToken);
    }

    public Task<bool> RestoreRunAsync(
        string runID,
        DateTimeOffset restoredAt,
        CancellationToken cancellationToken=default){
        ArgumentException.ThrowIfNullOrWhiteSpace(runID);
        return this.writer.ExecuteAsync((connection,transaction)=>{
            using var command=CreateCommand(connection,transaction,
                """
                UPDATE "DynamicRuns"
                SET "deleted_at"=NULL,
                    "deleted_reason"='',
                    "updated_at"=$updated_at
                WHERE "run_id"=$run_id AND "deleted_at" IS NOT NULL;
                """);
            Add(command,"$updated_at",Format(restoredAt));
            Add(command,"$run_id",runID);
            return command.ExecuteNonQuery()==1;
        },cancellationToken);
    }

    public Task<bool> PurgeRunAsync(
        string runID,
        CancellationToken cancellationToken=default){
        ArgumentException.ThrowIfNullOrWhiteSpace(runID);
        return this.writer.ExecuteAsync((connection,transaction)=>{
            using var command=CreateCommand(connection,transaction,
                """
                DELETE FROM "DynamicRuns"
                WHERE "run_id"=$run_id AND "deleted_at" IS NOT NULL;
                """);
            Add(command,"$run_id",runID);
            return command.ExecuteNonQuery()==1;
        },cancellationToken);
    }

    public IReadOnlyList<DynamicRunRecord> GetRuns(
        bool includeDeleted=false,
        bool trashOnly=false){
        using var connection=this.database.OpenReadConnection();
        using var command=connection.CreateCommand();
        command.CommandText=trashOnly
            ?RunSelect+" WHERE \"deleted_at\" IS NOT NULL ORDER BY \"updated_at\" DESC;"
            :includeDeleted
                ?RunSelect+" ORDER BY \"updated_at\" DESC;"
                :RunSelect+" WHERE \"deleted_at\" IS NULL ORDER BY \"updated_at\" DESC;";
        using var reader=command.ExecuteReader();
        var records=new List<DynamicRunRecord>();
        while(reader.Read()){
            records.Add(ReadRun(reader));
        }
        return records;
    }

    public DynamicRunSnapshot? GetRunSnapshot(string runID){
        ArgumentException.ThrowIfNullOrWhiteSpace(runID);
        using var connection=this.database.OpenReadConnection();
        var run=ReadSingleRun(connection,runID);
        if(run is null){
            return null;
        }

        return new DynamicRunSnapshot(
            run,
            ReadRunSources(connection,runID),
            ReadQueries(connection,runID),
            ReadSourceExecutions(connection,runID),
            ReadValidations(connection,runID),
            ReadMeasurements(connection,runID),
            ReadRawSamples(connection,runID),
            ReadDiagnostics(connection,runID),
            ReadOptimizationEvents(connection,runID),
            ReadRunEvents(connection,runID));
    }

    public DynamicRunSnapshot? GetRunSnapshotByQueryID(string queryID){
        ArgumentException.ThrowIfNullOrWhiteSpace(queryID);
        using var connection=this.database.OpenReadConnection();
        using var command=connection.CreateCommand();
        command.CommandText="SELECT \"run_id\" FROM \"DynamicQueries\" WHERE \"query_id\"=$query_id;";
        Add(command,"$query_id",queryID);
        var runID=command.ExecuteScalar() as string;
        return string.IsNullOrWhiteSpace(runID)?null:this.GetRunSnapshot(runID);
    }

    private const string RunSelect=
        """
        SELECT "run_id","status","seed","profile","settings_json","app_version","worker_version",
               "created_at","updated_at","started_at","completed_at","cancelled_at",
               "replayed_from_run_id","deleted_at","deleted_reason"
        FROM "DynamicRuns"
        """;

    private static void UpsertRun(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DynamicRunRecord run){
        using var command=CreateCommand(connection,transaction,
            """
            INSERT INTO "DynamicRuns"(
                "run_id","status","seed","profile","settings_json","app_version","worker_version",
                "created_at","updated_at","started_at","completed_at","cancelled_at",
                "replayed_from_run_id","deleted_at","deleted_reason")
            VALUES(
                $run_id,$status,$seed,$profile,$settings_json,$app_version,$worker_version,
                $created_at,$updated_at,$started_at,$completed_at,$cancelled_at,
                $replayed_from_run_id,$deleted_at,$deleted_reason)
            ON CONFLICT("run_id") DO UPDATE SET
                "status"=excluded."status",
                "seed"=excluded."seed",
                "profile"=excluded."profile",
                "settings_json"=excluded."settings_json",
                "app_version"=excluded."app_version",
                "worker_version"=excluded."worker_version",
                "created_at"=excluded."created_at",
                "updated_at"=excluded."updated_at",
                "started_at"=excluded."started_at",
                "completed_at"=excluded."completed_at",
                "cancelled_at"=excluded."cancelled_at",
                "replayed_from_run_id"=excluded."replayed_from_run_id";
            """);
        Add(command,"$run_id",run.RunID);
        Add(command,"$status",run.Status);
        Add(command,"$seed",run.Seed);
        Add(command,"$profile",run.Profile);
        Add(command,"$settings_json",run.SettingsJSON);
        Add(command,"$app_version",run.AppVersion);
        Add(command,"$worker_version",run.WorkerVersion);
        Add(command,"$created_at",Format(run.CreatedAt));
        Add(command,"$updated_at",Format(run.UpdatedAt));
        Add(command,"$started_at",Format(run.StartedAt));
        Add(command,"$completed_at",Format(run.CompletedAt));
        Add(command,"$cancelled_at",Format(run.CancelledAt));
        Add(command,"$replayed_from_run_id",run.ReplayedFromRunID);
        Add(command,"$deleted_at",Format(run.DeletedAt));
        Add(command,"$deleted_reason",run.DeletedReason);
        command.ExecuteNonQuery();
    }

    private static void UpsertRunSource(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DynamicRunSourceRecord source){
        using var command=CreateCommand(connection,transaction,
            """
            INSERT INTO "DynamicRunSources"("run_id","source_key","selected","display_order")
            VALUES($run_id,$source_key,$selected,$display_order)
            ON CONFLICT("run_id","source_key") DO UPDATE SET
                "selected"=excluded."selected",
                "display_order"=excluded."display_order";
            """);
        Add(command,"$run_id",source.RunID);
        Add(command,"$source_key",source.SourceKey);
        Add(command,"$selected",source.Selected?1:0);
        Add(command,"$display_order",source.DisplayOrder);
        command.ExecuteNonQuery();
    }

    private static void UpsertQuery(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DynamicQueryRecord query){
        using var command=CreateCommand(connection,transaction,
            """
            INSERT INTO "DynamicQueries"(
                "query_id","run_id","ordinal","path_key","generation_seed","canonical_ir_json",
                "source_text","query_hash","status","replayed_from_query_id")
            VALUES(
                $query_id,$run_id,$ordinal,$path_key,$generation_seed,$canonical_ir_json,
                $source_text,$query_hash,$status,$replayed_from_query_id)
            ON CONFLICT("query_id") DO UPDATE SET
                "run_id"=excluded."run_id",
                "ordinal"=excluded."ordinal",
                "path_key"=excluded."path_key",
                "generation_seed"=excluded."generation_seed",
                "canonical_ir_json"=excluded."canonical_ir_json",
                "source_text"=excluded."source_text",
                "query_hash"=excluded."query_hash",
                "status"=excluded."status",
                "replayed_from_query_id"=excluded."replayed_from_query_id";
            """);
        Add(command,"$query_id",query.QueryID);
        Add(command,"$run_id",query.RunID);
        Add(command,"$ordinal",query.Ordinal);
        Add(command,"$path_key",query.PathKey);
        Add(command,"$generation_seed",query.GenerationSeed);
        Add(command,"$canonical_ir_json",query.CanonicalIRJSON);
        Add(command,"$source_text",query.SourceText);
        Add(command,"$query_hash",query.QueryHash);
        Add(command,"$status",query.Status);
        Add(command,"$replayed_from_query_id",query.ReplayedFromQueryID);
        command.ExecuteNonQuery();
    }

    private static void UpsertSourceExecution(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DynamicSourceExecutionRecord execution){
        using var command=CreateCommand(connection,transaction,
            """
            INSERT INTO "DynamicSourceExecutions"(
                "execution_id","query_id","source_key","attempt","execution_order","status",
                "loop_count","warmup_count","started_at","completed_at","result_digest","error","partial")
            VALUES(
                $execution_id,$query_id,$source_key,$attempt,$execution_order,$status,
                $loop_count,$warmup_count,$started_at,$completed_at,$result_digest,$error,$partial)
            ON CONFLICT("execution_id") DO UPDATE SET
                "query_id"=excluded."query_id",
                "source_key"=excluded."source_key",
                "attempt"=excluded."attempt",
                "execution_order"=excluded."execution_order",
                "status"=excluded."status",
                "loop_count"=excluded."loop_count",
                "warmup_count"=excluded."warmup_count",
                "started_at"=excluded."started_at",
                "completed_at"=excluded."completed_at",
                "result_digest"=excluded."result_digest",
                "error"=excluded."error",
                "partial"=excluded."partial";
            """);
        Add(command,"$execution_id",execution.ExecutionID);
        Add(command,"$query_id",execution.QueryID);
        Add(command,"$source_key",execution.SourceKey);
        Add(command,"$attempt",execution.Attempt);
        Add(command,"$execution_order",execution.ExecutionOrder);
        Add(command,"$status",execution.Status);
        Add(command,"$loop_count",execution.LoopCount);
        Add(command,"$warmup_count",execution.WarmupCount);
        Add(command,"$started_at",Format(execution.StartedAt));
        Add(command,"$completed_at",Format(execution.CompletedAt));
        Add(command,"$result_digest",execution.ResultDigest);
        Add(command,"$error",execution.Error);
        Add(command,"$partial",execution.Partial?1:0);
        command.ExecuteNonQuery();
    }

    private static void UpsertValidation(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DynamicValidationRecord validation){
        using var command=CreateCommand(connection,transaction,
            """
            INSERT INTO "DynamicValidationResults"(
                "validation_id","query_id","source_key","status","expected_digest",
                "actual_digest","message","validated_at")
            VALUES(
                $validation_id,$query_id,$source_key,$status,$expected_digest,
                $actual_digest,$message,$validated_at)
            ON CONFLICT("validation_id") DO UPDATE SET
                "query_id"=excluded."query_id",
                "source_key"=excluded."source_key",
                "status"=excluded."status",
                "expected_digest"=excluded."expected_digest",
                "actual_digest"=excluded."actual_digest",
                "message"=excluded."message",
                "validated_at"=excluded."validated_at";
            """);
        Add(command,"$validation_id",validation.ValidationID);
        Add(command,"$query_id",validation.QueryID);
        Add(command,"$source_key",validation.SourceKey);
        Add(command,"$status",validation.Status);
        Add(command,"$expected_digest",validation.ExpectedDigest);
        Add(command,"$actual_digest",validation.ActualDigest);
        Add(command,"$message",validation.Message);
        Add(command,"$validated_at",Format(validation.ValidatedAt));
        command.ExecuteNonQuery();
    }

    private static void ReplaceMeasurementBundle(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DynamicMeasurementBundle bundle){
        foreach(var sample in bundle.RawSamples){
            if(!string.Equals(
                sample.MeasurementID,
                bundle.Measurement.MeasurementID,
                StringComparison.Ordinal)){
                throw new InvalidDataException(
                    $"Raw sample measurement ID does not match its aggregate. Expected={bundle.Measurement.MeasurementID} Actual={sample.MeasurementID}");
            }
        }
        UpsertMeasurement(connection,transaction,bundle.Measurement);
        using(var deleteCommand=CreateCommand(connection,transaction,
            "DELETE FROM \"DynamicMeasurementSamples\" WHERE \"measurement_id\"=$measurement_id;")){
            Add(deleteCommand,"$measurement_id",bundle.Measurement.MeasurementID);
            deleteCommand.ExecuteNonQuery();
        }
        foreach(var sample in bundle.RawSamples){
            InsertRawSample(connection,transaction,sample);
        }
    }

    private static void UpsertMeasurement(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DynamicMeasurementRecord measurement){
        using var command=CreateCommand(connection,transaction,
            """
            INSERT INTO "DynamicMeasurements"(
                "measurement_id","execution_id","implementation_kind","operation_count","sample_count",
                "median_ns","mean_ns","min_ns","max_ns","standard_deviation_ns","standard_error_ns",
                "allocated_bytes_per_operation","gen0_collections","gen1_collections","gen2_collections",
                "started_at","completed_at")
            VALUES(
                $measurement_id,$execution_id,$implementation_kind,$operation_count,$sample_count,
                $median_ns,$mean_ns,$min_ns,$max_ns,$standard_deviation_ns,$standard_error_ns,
                $allocated_bytes_per_operation,$gen0_collections,$gen1_collections,$gen2_collections,
                $started_at,$completed_at)
            ON CONFLICT("measurement_id") DO UPDATE SET
                "execution_id"=excluded."execution_id",
                "implementation_kind"=excluded."implementation_kind",
                "operation_count"=excluded."operation_count",
                "sample_count"=excluded."sample_count",
                "median_ns"=excluded."median_ns",
                "mean_ns"=excluded."mean_ns",
                "min_ns"=excluded."min_ns",
                "max_ns"=excluded."max_ns",
                "standard_deviation_ns"=excluded."standard_deviation_ns",
                "standard_error_ns"=excluded."standard_error_ns",
                "allocated_bytes_per_operation"=excluded."allocated_bytes_per_operation",
                "gen0_collections"=excluded."gen0_collections",
                "gen1_collections"=excluded."gen1_collections",
                "gen2_collections"=excluded."gen2_collections",
                "started_at"=excluded."started_at",
                "completed_at"=excluded."completed_at";
            """);
        Add(command,"$measurement_id",measurement.MeasurementID);
        Add(command,"$execution_id",measurement.ExecutionID);
        Add(command,"$implementation_kind",measurement.ImplementationKind);
        Add(command,"$operation_count",measurement.OperationCount);
        Add(command,"$sample_count",measurement.SampleCount);
        Add(command,"$median_ns",measurement.MedianNanoseconds);
        Add(command,"$mean_ns",measurement.MeanNanoseconds);
        Add(command,"$min_ns",measurement.MinimumNanoseconds);
        Add(command,"$max_ns",measurement.MaximumNanoseconds);
        Add(command,"$standard_deviation_ns",measurement.StandardDeviationNanoseconds);
        Add(command,"$standard_error_ns",measurement.StandardErrorNanoseconds);
        Add(command,"$allocated_bytes_per_operation",measurement.AllocatedBytesPerOperation);
        Add(command,"$gen0_collections",measurement.Gen0Collections);
        Add(command,"$gen1_collections",measurement.Gen1Collections);
        Add(command,"$gen2_collections",measurement.Gen2Collections);
        Add(command,"$started_at",Format(measurement.StartedAt));
        Add(command,"$completed_at",Format(measurement.CompletedAt));
        command.ExecuteNonQuery();
    }

    private static void InsertRawSample(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DynamicRawSampleRecord sample){
        using var command=CreateCommand(connection,transaction,
            """
            INSERT INTO "DynamicMeasurementSamples"(
                "measurement_id","phase","sample_index","elapsed_ns","operation_count","allocated_bytes",
                "gen0_collections","gen1_collections","gen2_collections","recorded_at")
            VALUES(
                $measurement_id,$phase,$sample_index,$elapsed_ns,$operation_count,$allocated_bytes,
                $gen0_collections,$gen1_collections,$gen2_collections,$recorded_at);
            """);
        Add(command,"$measurement_id",sample.MeasurementID);
        Add(command,"$phase",sample.Phase);
        Add(command,"$sample_index",sample.SampleIndex);
        Add(command,"$elapsed_ns",sample.ElapsedNanoseconds);
        Add(command,"$operation_count",sample.OperationCount);
        Add(command,"$allocated_bytes",sample.AllocatedBytes);
        Add(command,"$gen0_collections",sample.Gen0Collections);
        Add(command,"$gen1_collections",sample.Gen1Collections);
        Add(command,"$gen2_collections",sample.Gen2Collections);
        Add(command,"$recorded_at",Format(sample.RecordedAt));
        command.ExecuteNonQuery();
    }

    private static void UpsertDiagnostic(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DynamicDiagnosticRecord diagnostic){
        using var command=CreateCommand(connection,transaction,
            """
            INSERT INTO "DynamicDiagnostics"(
                "diagnostic_id","execution_id","name","numeric_value","text_value",
                "unit","details_json","recorded_at")
            VALUES(
                $diagnostic_id,$execution_id,$name,$numeric_value,$text_value,
                $unit,$details_json,$recorded_at)
            ON CONFLICT("diagnostic_id") DO UPDATE SET
                "execution_id"=excluded."execution_id",
                "name"=excluded."name",
                "numeric_value"=excluded."numeric_value",
                "text_value"=excluded."text_value",
                "unit"=excluded."unit",
                "details_json"=excluded."details_json",
                "recorded_at"=excluded."recorded_at";
            """);
        Add(command,"$diagnostic_id",diagnostic.DiagnosticID);
        Add(command,"$execution_id",diagnostic.ExecutionID);
        Add(command,"$name",diagnostic.Name);
        Add(command,"$numeric_value",diagnostic.NumericValue);
        Add(command,"$text_value",diagnostic.TextValue);
        Add(command,"$unit",diagnostic.Unit);
        Add(command,"$details_json",diagnostic.DetailsJSON);
        Add(command,"$recorded_at",Format(diagnostic.RecordedAt));
        command.ExecuteNonQuery();
    }

    private static void UpsertOptimizationEvent(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DynamicOptimizationEventRecord optimizationEvent){
        using var command=CreateCommand(connection,transaction,
            """
            INSERT INTO "DynamicOptimizationEvents"(
                "optimization_event_id","query_id","source_key","sequence","kind",
                "details_json","recorded_at")
            VALUES(
                $optimization_event_id,$query_id,$source_key,$sequence,$kind,
                $details_json,$recorded_at)
            ON CONFLICT("optimization_event_id") DO UPDATE SET
                "query_id"=excluded."query_id",
                "source_key"=excluded."source_key",
                "sequence"=excluded."sequence",
                "kind"=excluded."kind",
                "details_json"=excluded."details_json",
                "recorded_at"=excluded."recorded_at";
            """);
        Add(command,"$optimization_event_id",optimizationEvent.OptimizationEventID);
        Add(command,"$query_id",optimizationEvent.QueryID);
        Add(command,"$source_key",optimizationEvent.SourceKey);
        Add(command,"$sequence",optimizationEvent.Sequence);
        Add(command,"$kind",optimizationEvent.Kind);
        Add(command,"$details_json",optimizationEvent.DetailsJSON);
        Add(command,"$recorded_at",Format(optimizationEvent.RecordedAt));
        command.ExecuteNonQuery();
    }

    private static void UpsertRunEvent(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DynamicRunEventRecord runEvent){
        using var command=CreateCommand(connection,transaction,
            """
            INSERT INTO "DynamicRunEvents"(
                "run_event_id","run_id","query_id","execution_id","sequence",
                "level","kind","message","details_json","recorded_at")
            VALUES(
                $run_event_id,$run_id,$query_id,$execution_id,$sequence,
                $level,$kind,$message,$details_json,$recorded_at)
            ON CONFLICT("run_event_id") DO UPDATE SET
                "run_id"=excluded."run_id",
                "query_id"=excluded."query_id",
                "execution_id"=excluded."execution_id",
                "sequence"=excluded."sequence",
                "level"=excluded."level",
                "kind"=excluded."kind",
                "message"=excluded."message",
                "details_json"=excluded."details_json",
                "recorded_at"=excluded."recorded_at";
            """);
        Add(command,"$run_event_id",runEvent.RunEventID);
        Add(command,"$run_id",runEvent.RunID);
        Add(command,"$query_id",runEvent.QueryID);
        Add(command,"$execution_id",runEvent.ExecutionID);
        Add(command,"$sequence",runEvent.Sequence);
        Add(command,"$level",runEvent.Level);
        Add(command,"$kind",runEvent.Kind);
        Add(command,"$message",runEvent.Message);
        Add(command,"$details_json",runEvent.DetailsJSON);
        Add(command,"$recorded_at",Format(runEvent.RecordedAt));
        command.ExecuteNonQuery();
    }

    private static DynamicRunRecord? ReadSingleRun(SqliteConnection connection,string runID){
        using var command=connection.CreateCommand();
        command.CommandText=RunSelect+" WHERE \"run_id\"=$run_id;";
        Add(command,"$run_id",runID);
        using var reader=command.ExecuteReader();
        return reader.Read()?ReadRun(reader):null;
    }

    private static DynamicRunRecord ReadRun(SqliteDataReader reader){
        return new DynamicRunRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt64(2),
            reader.GetString(3),
            reader.GetString(4),
            ParseRequiredDate(reader,7),
            ParseRequiredDate(reader,8),
            reader.GetString(5),
            reader.GetString(6),
            ParseOptionalDate(reader,9),
            ParseOptionalDate(reader,10),
            ParseOptionalDate(reader,11),
            GetOptionalString(reader,12),
            ParseOptionalDate(reader,13),
            reader.GetString(14));
    }

    private static IReadOnlyList<DynamicRunSourceRecord> ReadRunSources(
        SqliteConnection connection,
        string runID){
        using var command=connection.CreateCommand();
        command.CommandText=
            """
            SELECT "run_id","source_key","selected","display_order"
            FROM "DynamicRunSources"
            WHERE "run_id"=$run_id
            ORDER BY "display_order","source_key";
            """;
        Add(command,"$run_id",runID);
        using var reader=command.ExecuteReader();
        var records=new List<DynamicRunSourceRecord>();
        while(reader.Read()){
            records.Add(new DynamicRunSourceRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2)!=0,
                reader.GetInt32(3)));
        }
        return records;
    }

    private static IReadOnlyList<DynamicQueryRecord> ReadQueries(
        SqliteConnection connection,
        string runID){
        using var command=connection.CreateCommand();
        command.CommandText=
            """
            SELECT "query_id","run_id","ordinal","path_key","generation_seed",
                   "canonical_ir_json","source_text","query_hash","status","replayed_from_query_id"
            FROM "DynamicQueries"
            WHERE "run_id"=$run_id
            ORDER BY "ordinal";
            """;
        Add(command,"$run_id",runID);
        using var reader=command.ExecuteReader();
        var records=new List<DynamicQueryRecord>();
        while(reader.Read()){
            records.Add(new DynamicQueryRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetInt64(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                GetOptionalString(reader,9)));
        }
        return records;
    }

    private static IReadOnlyList<DynamicSourceExecutionRecord> ReadSourceExecutions(
        SqliteConnection connection,
        string runID){
        using var command=connection.CreateCommand();
        command.CommandText=
            """
            SELECT e."execution_id",e."query_id",e."source_key",e."attempt",e."execution_order",
                   e."status",e."loop_count",e."warmup_count",e."started_at",e."completed_at",
                   e."result_digest",e."error",e."partial"
            FROM "DynamicSourceExecutions" e
            JOIN "DynamicQueries" q ON q."query_id"=e."query_id"
            WHERE q."run_id"=$run_id
            ORDER BY q."ordinal",e."execution_order",e."attempt";
            """;
        Add(command,"$run_id",runID);
        using var reader=command.ExecuteReader();
        var records=new List<DynamicSourceExecutionRecord>();
        while(reader.Read()){
            records.Add(new DynamicSourceExecutionRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetString(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                ParseOptionalDate(reader,8),
                ParseOptionalDate(reader,9),
                reader.GetString(10),
                reader.GetString(11),
                reader.GetInt64(12)!=0));
        }
        return records;
    }

    private static IReadOnlyList<DynamicValidationRecord> ReadValidations(
        SqliteConnection connection,
        string runID){
        using var command=connection.CreateCommand();
        command.CommandText=
            """
            SELECT v."validation_id",v."query_id",v."source_key",v."status",
                   v."validated_at",v."expected_digest",v."actual_digest",v."message"
            FROM "DynamicValidationResults" v
            JOIN "DynamicQueries" q ON q."query_id"=v."query_id"
            WHERE q."run_id"=$run_id
            ORDER BY q."ordinal",v."source_key",v."validated_at";
            """;
        Add(command,"$run_id",runID);
        using var reader=command.ExecuteReader();
        var records=new List<DynamicValidationRecord>();
        while(reader.Read()){
            records.Add(new DynamicValidationRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                ParseRequiredDate(reader,4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7)));
        }
        return records;
    }

    private static IReadOnlyList<DynamicMeasurementRecord> ReadMeasurements(
        SqliteConnection connection,
        string runID){
        using var command=connection.CreateCommand();
        command.CommandText=
            """
            SELECT m."measurement_id",m."execution_id",m."implementation_kind",m."operation_count",
                   m."sample_count",m."median_ns",m."mean_ns",m."min_ns",m."max_ns",
                   m."standard_deviation_ns",m."standard_error_ns",
                   m."allocated_bytes_per_operation",m."gen0_collections",
                   m."gen1_collections",m."gen2_collections",m."started_at",m."completed_at"
            FROM "DynamicMeasurements" m
            JOIN "DynamicSourceExecutions" e ON e."execution_id"=m."execution_id"
            JOIN "DynamicQueries" q ON q."query_id"=e."query_id"
            WHERE q."run_id"=$run_id
            ORDER BY q."ordinal",e."execution_order";
            """;
        Add(command,"$run_id",runID);
        using var reader=command.ExecuteReader();
        var records=new List<DynamicMeasurementRecord>();
        while(reader.Read()){
            records.Add(new DynamicMeasurementRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3),
                reader.GetInt32(4),
                GetOptionalDouble(reader,5),
                GetOptionalDouble(reader,6),
                GetOptionalDouble(reader,7),
                GetOptionalDouble(reader,8),
                GetOptionalDouble(reader,9),
                GetOptionalDouble(reader,10),
                GetOptionalDouble(reader,11),
                GetOptionalDouble(reader,12),
                GetOptionalDouble(reader,13),
                GetOptionalDouble(reader,14),
                ParseOptionalDate(reader,15),
                ParseOptionalDate(reader,16)));
        }
        return records;
    }

    private static IReadOnlyList<DynamicRawSampleRecord> ReadRawSamples(
        SqliteConnection connection,
        string runID){
        using var command=connection.CreateCommand();
        command.CommandText=
            """
            SELECT s."measurement_id",s."phase",s."sample_index",s."elapsed_ns",
                   s."operation_count",s."allocated_bytes",s."gen0_collections",
                   s."gen1_collections",s."gen2_collections",s."recorded_at"
            FROM "DynamicMeasurementSamples" s
            JOIN "DynamicMeasurements" m ON m."measurement_id"=s."measurement_id"
            JOIN "DynamicSourceExecutions" e ON e."execution_id"=m."execution_id"
            JOIN "DynamicQueries" q ON q."query_id"=e."query_id"
            WHERE q."run_id"=$run_id
            ORDER BY q."ordinal",e."execution_order",s."phase",s."sample_index";
            """;
        Add(command,"$run_id",runID);
        using var reader=command.ExecuteReader();
        var records=new List<DynamicRawSampleRecord>();
        while(reader.Read()){
            records.Add(new DynamicRawSampleRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetDouble(3),
                reader.GetInt64(4),
                reader.GetInt64(5),
                reader.GetInt64(6),
                reader.GetInt64(7),
                reader.GetInt64(8),
                ParseRequiredDate(reader,9)));
        }
        return records;
    }

    private static IReadOnlyList<DynamicDiagnosticRecord> ReadDiagnostics(
        SqliteConnection connection,
        string runID){
        using var command=connection.CreateCommand();
        command.CommandText=
            """
            SELECT d."diagnostic_id",d."execution_id",d."name",d."details_json",
                   d."recorded_at",d."numeric_value",d."text_value",d."unit"
            FROM "DynamicDiagnostics" d
            JOIN "DynamicSourceExecutions" e ON e."execution_id"=d."execution_id"
            JOIN "DynamicQueries" q ON q."query_id"=e."query_id"
            WHERE q."run_id"=$run_id
            ORDER BY q."ordinal",e."execution_order",d."name";
            """;
        Add(command,"$run_id",runID);
        using var reader=command.ExecuteReader();
        var records=new List<DynamicDiagnosticRecord>();
        while(reader.Read()){
            records.Add(new DynamicDiagnosticRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                ParseRequiredDate(reader,4),
                GetOptionalDouble(reader,5),
                GetOptionalString(reader,6),
                reader.GetString(7)));
        }
        return records;
    }

    private static IReadOnlyList<DynamicOptimizationEventRecord> ReadOptimizationEvents(
        SqliteConnection connection,
        string runID){
        using var command=connection.CreateCommand();
        command.CommandText=
            """
            SELECT o."optimization_event_id",o."query_id",o."source_key",o."sequence",
                   o."kind",o."details_json",o."recorded_at"
            FROM "DynamicOptimizationEvents" o
            JOIN "DynamicQueries" q ON q."query_id"=o."query_id"
            WHERE q."run_id"=$run_id
            ORDER BY q."ordinal",o."source_key",o."sequence";
            """;
        Add(command,"$run_id",runID);
        using var reader=command.ExecuteReader();
        var records=new List<DynamicOptimizationEventRecord>();
        while(reader.Read()){
            records.Add(new DynamicOptimizationEventRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5),
                ParseRequiredDate(reader,6)));
        }
        return records;
    }

    private static IReadOnlyList<DynamicRunEventRecord> ReadRunEvents(
        SqliteConnection connection,
        string runID){
        using var command=connection.CreateCommand();
        command.CommandText=
            """
            SELECT "run_event_id","run_id","sequence","level","kind","message",
                   "details_json","recorded_at","query_id","execution_id"
            FROM "DynamicRunEvents"
            WHERE "run_id"=$run_id
            ORDER BY "sequence";
            """;
        Add(command,"$run_id",runID);
        using var reader=command.ExecuteReader();
        var records=new List<DynamicRunEventRecord>();
        while(reader.Read()){
            records.Add(new DynamicRunEventRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                ParseRequiredDate(reader,7),
                GetOptionalString(reader,8),
                GetOptionalString(reader,9)));
        }
        return records;
    }

    private static SqliteCommand CreateCommand(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText){
        var command=connection.CreateCommand();
        command.Transaction=transaction;
        command.CommandText=commandText;
        return command;
    }

    private static void Add(SqliteCommand command,string name,object? value){
        command.Parameters.AddWithValue(name,value??DBNull.Value);
    }

    private static string Format(DateTimeOffset value){
        return value.ToString("O",CultureInfo.InvariantCulture);
    }

    private static string? Format(DateTimeOffset? value){
        return value?.ToString("O",CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset ParseRequiredDate(SqliteDataReader reader,int ordinal){
        return DateTimeOffset.Parse(
            reader.GetString(ordinal),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
    }

    private static DateTimeOffset? ParseOptionalDate(SqliteDataReader reader,int ordinal){
        return reader.IsDBNull(ordinal)?null:ParseRequiredDate(reader,ordinal);
    }

    private static string? GetOptionalString(SqliteDataReader reader,int ordinal){
        return reader.IsDBNull(ordinal)?null:reader.GetString(ordinal);
    }

    private static double? GetOptionalDouble(SqliteDataReader reader,int ordinal){
        return reader.IsDBNull(ordinal)?null:reader.GetDouble(ordinal);
    }
}
