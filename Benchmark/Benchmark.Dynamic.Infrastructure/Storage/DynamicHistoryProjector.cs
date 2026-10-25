using Benchmark.Dynamic.Core;
using Benchmark.Infrastructure;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace Benchmark.Dynamic.Infrastructure;

/// <summary>通常実行とライブ実行の詳細記録を、同じ共通履歴へ投影する。</summary>
internal sealed class DynamicHistoryProjector{
    public void SaveChanges(SqliteConnection connection,SqliteTransaction transaction,DynamicRunRecord run,string metadataJSON){
        var metadata=JsonSerializer.Deserialize<DynamicMeasurementConditions>(metadataJSON)!;
        foreach(var change in JsonSerializer.Deserialize<ConfigurationChange[]>(metadata.StorageChangesJSON??"[]")??[]){
            if(change.RunID!=run.RunID){throw new InvalidDataException("The configuration change belongs to another run.");}
            BenchmarkRepository.SaveChange(connection,transaction,change);
        }
    }
    public void SaveRun(SqliteConnection connection,SqliteTransaction transaction,DynamicRunRecord run){
        var status=run.Status;
        if(status=="Completed"){
            using var command=connection.CreateCommand();command.Transaction=transaction;
            command.CommandText="SELECT COUNT(*) FROM DynamicSourceExecutions e JOIN DynamicQueries q ON q.query_id=e.query_id WHERE q.run_id=$run AND e.status<>'Succeeded';";
            command.Parameters.AddWithValue("$run",run.RunID);
            status=Convert.ToInt64(command.ExecuteScalar())==0?"Passed":"Failed";
        }
        var previous=BenchmarkRepository.GetRun(connection,transaction,run.RunID);
        BenchmarkRepository.SaveRun(connection,transaction,new(run.RunID,"Dynamic",status,run.SettingsJSON,previous?.EnvironmentJSON??"{}",previous?.CodeFingerprint??(run.AppVersion+"/"+run.WorkerVersion),run.CreatedAt,run.CompletedAt));
    }
    public void SaveMeasurements(SqliteConnection connection,SqliteTransaction transaction,DynamicRunRecord run,IReadOnlyList<DynamicMeasurementBundle>? measurements){
        this.SaveRun(connection,transaction,run);
        foreach(var bundle in measurements??[]){
            using var command=connection.CreateCommand();command.Transaction=transaction;
            command.CommandText="""
                SELECT q.query_hash,q.element_type,q.ordinal,e.source_key,e.result_digest,e.status,
                    (SELECT d.details_json FROM DynamicDiagnostics d WHERE d.execution_id=e.execution_id AND d.name='MeasurementConditions' LIMIT 1),q.canonical_ir_json
                FROM DynamicSourceExecutions e JOIN DynamicQueries q ON q.query_id=e.query_id WHERE e.execution_id=$id;
                """;
            command.Parameters.AddWithValue("$id",bundle.Measurement.ExecutionID);
            using var reader=command.ExecuteReader();if(!reader.Read()){throw new InvalidDataException("The dynamic measurement has no execution.");}
            var metadata=reader.IsDBNull(6)?null:JsonSerializer.Deserialize<DynamicMeasurementConditions>(reader.GetString(6));
            var known=metadata is not null&&!string.IsNullOrEmpty(metadata.InputHash)&&!string.IsNullOrEmpty(metadata.EnvironmentJSON);
            var source=Enum.Parse<SourceTarget>(reader.GetString(3));
            var type=ElementTypeCatalog.Get(Enum.Parse<QueryElementType>(reader.GetString(1))).ClrType.FullName??reader.GetString(1);
            var kind=bundle.Measurement.ImplementationKind;
            var count=0L;
            using(var settings=JsonDocument.Parse(run.SettingsJSON)){
                if(settings.RootElement.TryGetProperty("inputElementCount",out var value)||settings.RootElement.TryGetProperty("InputElementCount",out value)){count=value.GetInt64();}
            }
            var environment="unknown";
            if(known){environment=JsonSerializer.Deserialize<EnvironmentSnapshot>(metadata!.EnvironmentJSON)!.Key;}
            var workload="unknown:"+reader.GetString(0);
            if(known){workload=QueryWorkloadIdentity.Compute(QueryIrJson.Deserialize(reader.GetString(7)));}
            var conditions=new Conditions(workload,metadata?.InputHash??"unknown",count,type,source.GetSourceKind().ToString(),metadata?.StoreKind??"Unknown",
                kind,kind=="FLinq"?(metadata?.Optimizations??"All"):kind=="FLinqUnoptimized"?"None":"Native",metadata?.ParametersJSON??"{}",metadata?.GrowthJSON??"{}",environment,
                MeasurementVersion:run.Profile,Known:known,IO:metadata?.IO??"BackendDefault");
            var samples=bundle.RawSamples.Where(static sample=>sample.Phase=="Measurement").Select(static sample=>new Sample(sample.ElapsedNanoseconds,sample.OperationCount,sample.AllocatedBytes,Gen0:checked((int)sample.Gen0Collections),Gen1:checked((int)sample.Gen1Collections),Gen2:checked((int)sample.Gen2Collections))).ToArray();
            if(samples.Length==0){samples=bundle.RawSamples.Select(static sample=>new Sample(sample.ElapsedNanoseconds,sample.OperationCount,sample.AllocatedBytes)).ToArray();}
            var measurement=Measurement.Create(bundle.Measurement.MeasurementID,run.RunID,conditions,reader.GetString(4),samples) with{Status=reader.GetString(5)=="Succeeded"?"Passed":reader.GetString(5),Interval=reader.GetInt32(2)};
            reader.Close();
            BenchmarkRepository.SaveMeasurement(connection,transaction,measurement);
            if(known){
                var root=BenchmarkRepository.GetRun(connection,transaction,run.RunID)!;
                BenchmarkRepository.SaveRun(connection,transaction,root with{EnvironmentJSON=metadata!.EnvironmentJSON,CodeFingerprint=metadata.CodeFingerprint});
            }
        }
    }
}

public sealed record DynamicMeasurementConditions(string InputHash,string EnvironmentJSON,string ParametersJSON,string GrowthJSON,string StoreKind,string Optimizations,string CodeFingerprint,string? StorageChangesJSON=null,string IO="BackendDefault");
