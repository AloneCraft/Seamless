using Microsoft.Data.Sqlite;
using Seamless.Execution;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Benchmark.Infrastructure;

public sealed class BenchmarkRepository{
    public static JsonSerializerOptions JSON{get;}=new(){NumberHandling=JsonNumberHandling.AllowNamedFloatingPointLiterals};
    private readonly BenchmarkDatabase database;
    public BenchmarkRepository(BenchmarkDatabase database){this.database=database;this.database.InitializeIfMissing();}
    public void SaveRun(Run run){
        using var connection=this.database.OpenWrite();
        SaveRun(connection,null,run);
    }
    public static void SaveRun(SqliteConnection connection,SqliteTransaction? transaction,Run run){
        using var command=connection.CreateCommand();command.Transaction=transaction;
        command.CommandText="INSERT INTO BenchmarkRuns VALUES($id,$app,$status,$at,$payload) ON CONFLICT(id) DO UPDATE SET status=excluded.status,payload=excluded.payload;";
        Add(command,"$id",run.ID);Add(command,"$app",run.Application);Add(command,"$status",run.Status);Add(command,"$at",run.CreatedAt.ToString("O"));Add(command,"$payload",Serialize(run));command.ExecuteNonQuery();
    }
    public Run? GetRun(string id){
        using var connection=this.database.OpenRead();return GetRun(connection,null,id);
    }
    public static Run? GetRun(SqliteConnection connection,SqliteTransaction? transaction,string id){
        using var command=connection.CreateCommand();command.Transaction=transaction;
        command.CommandText="SELECT payload FROM BenchmarkRuns WHERE id=$id;";Add(command,"$id",id);
        return Read<Run>(command.ExecuteScalar());
    }
    public IReadOnlyList<Run> GetRuns(string? application=null,int offset=0,int limit=100){
        using var connection=this.database.OpenRead();using var command=connection.CreateCommand();
        command.CommandText="SELECT payload FROM BenchmarkRuns WHERE ($app IS NULL OR application=$app) ORDER BY created_at DESC LIMIT $limit OFFSET $offset;";
        Add(command,"$app",application);Add(command,"$limit",limit);Add(command,"$offset",offset);
        using var reader=command.ExecuteReader();var result=new List<Run>();
        while(reader.Read()){result.Add(Deserialize<Run>(reader.GetString(0)));}return result;
    }
    public void SaveMeasurement(Measurement measurement){
        using var connection=this.database.OpenWrite();using var transaction=connection.BeginTransaction();
        SaveMeasurement(connection,transaction,measurement);transaction.Commit();
    }
    public static void SaveMeasurement(SqliteConnection connection,SqliteTransaction transaction,Measurement measurement){
        using var command=connection.CreateCommand();command.Transaction=transaction;
        command.CommandText="INSERT INTO BenchmarkMeasurements VALUES($id,$run,$scope,$interval,$status,$payload) ON CONFLICT(id) DO UPDATE SET scope_key=excluded.scope_key,interval=excluded.interval,status=excluded.status,payload=excluded.payload;";
        Add(command,"$id",measurement.ID);Add(command,"$run",measurement.RunID);Add(command,"$scope",measurement.Conditions.ScopeKey);Add(command,"$interval",measurement.Interval);Add(command,"$status",measurement.Status);Add(command,"$payload",Serialize(measurement));command.ExecuteNonQuery();
        command.Parameters.Clear();command.CommandText="DELETE FROM BenchmarkSamples WHERE measurement_id=$id;";Add(command,"$id",measurement.ID);command.ExecuteNonQuery();
        for(var index=0;index<measurement.Samples.Length;index++){
            var sample=measurement.Samples[index];
            if(!double.IsFinite(sample.ElapsedNanoseconds)||sample.OperationCount<=0){continue;}
            command.Parameters.Clear();command.CommandText="INSERT INTO BenchmarkSamples VALUES($id,$index,$elapsed,$operations,$allocated,$payload);";
            Add(command,"$id",measurement.ID);Add(command,"$index",index);Add(command,"$elapsed",sample.ElapsedNanoseconds);Add(command,"$operations",sample.OperationCount);Add(command,"$allocated",sample.AllocatedBytes);Add(command,"$payload",Serialize(sample));command.ExecuteNonQuery();
        }
    }
    public Measurement? GetMeasurement(string id){
        using var connection=this.database.OpenRead();using var command=connection.CreateCommand();
        command.CommandText="SELECT payload FROM BenchmarkMeasurements WHERE id=$id;";Add(command,"$id",id);return Read<Measurement>(command.ExecuteScalar());
    }
    public IReadOnlyList<Measurement> GetMeasurements(string runID,int offset=0,int limit=200){
        using var connection=this.database.OpenRead();using var command=connection.CreateCommand();
        command.CommandText="SELECT payload FROM BenchmarkMeasurements WHERE run_id=$run ORDER BY interval,id LIMIT $limit OFFSET $offset;";
        Add(command,"$run",runID);Add(command,"$limit",limit);Add(command,"$offset",offset);
        using var reader=command.ExecuteReader();var result=new List<Measurement>();
        while(reader.Read()){result.Add(Deserialize<Measurement>(reader.GetString(0)));}return result;
    }
    public void SaveChange(ConfigurationChange change){
        using var connection=this.database.OpenWrite();SaveChange(connection,null,change);
    }
    public static void SaveChange(SqliteConnection connection,SqliteTransaction? transaction,ConfigurationChange change){
        using var command=connection.CreateCommand();command.Transaction=transaction;
        command.CommandText="INSERT INTO BenchmarkChanges VALUES($id,$run,$interval,$payload) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload;";
        Add(command,"$id",change.ID);Add(command,"$run",change.RunID);Add(command,"$interval",change.Interval);Add(command,"$payload",Serialize(change));command.ExecuteNonQuery();
    }
    public IReadOnlyList<ConfigurationChange> GetChanges(string runID){
        using var connection=this.database.OpenRead();using var command=connection.CreateCommand();command.CommandText="SELECT payload FROM BenchmarkChanges WHERE run_id=$run ORDER BY interval;";Add(command,"$run",runID);
        using var reader=command.ExecuteReader();var result=new List<ConfigurationChange>();while(reader.Read()){result.Add(Deserialize<ConfigurationChange>(reader.GetString(0)));}return result;
    }
    public void SetBaseline(string measurementID){
        var measurement=this.GetMeasurement(measurementID)??throw new ArgumentException("比較対象がありません。",nameof(measurementID));
        var run=this.GetRun(measurement.RunID);
        if(run?.Status!="Passed"||Comparison.Compare(measurement,measurement).Status!=ComparisonStatus.Passed){throw new InvalidOperationException("正常終了し、測定条件と正しさが確認できる結果だけを基準にできます。");}
        using var connection=this.database.OpenWrite();using var command=connection.CreateCommand();
        command.CommandText="INSERT INTO BenchmarkBaselines VALUES($scope,$id,$at) ON CONFLICT(scope_key) DO UPDATE SET measurement_id=excluded.measurement_id,selected_at=excluded.selected_at;";
        Add(command,"$scope",measurement.Conditions.ScopeKey);Add(command,"$id",measurementID);Add(command,"$at",DateTimeOffset.UtcNow.ToString("O"));command.ExecuteNonQuery();
    }
    public string? GetBaseline(Conditions conditions){
        using var connection=this.database.OpenRead();using var command=connection.CreateCommand();command.CommandText="SELECT measurement_id FROM BenchmarkBaselines WHERE scope_key=$scope;";Add(command,"$scope",conditions.ScopeKey);return command.ExecuteScalar() as string;
    }
    public ComparisonResult Compare(string baselineID,string candidateID,ComparisonMode mode=ComparisonMode.Regression,double tolerance=0.05){
        var baseline=this.GetMeasurement(baselineID)??throw new ArgumentException("基準の記録がありません。");
        var candidate=this.GetMeasurement(candidateID)??throw new ArgumentException("候補の記録がありません。");
        var result=Comparison.Compare(baseline,candidate,mode,tolerance);
        if(this.GetRun(baseline.RunID)?.Status!="Passed"||this.GetRun(candidate.RunID)?.Status!="Passed"){
            result=result with{Status=ComparisonStatus.Incomplete,TimeRatio=null,AllocationRatio=null,Reason="基準または候補のRunが正常完了していません。"};
        }
        using var connection=this.database.OpenWrite();using var command=connection.CreateCommand();command.CommandText="INSERT INTO BenchmarkComparisons VALUES($id,$before,$after,$payload);";
        Add(command,"$id",result.ID);Add(command,"$before",baselineID);Add(command,"$after",candidateID);Add(command,"$payload",Serialize(result));command.ExecuteNonQuery();return result;
    }
    public IReadOnlyList<ComparisonResult> GetComparisons(string runID){
        using var connection=this.database.OpenRead();using var command=connection.CreateCommand();
        command.CommandText="SELECT c.payload FROM BenchmarkComparisons c JOIN BenchmarkMeasurements m ON m.id=c.candidate_id WHERE m.run_id=$run ORDER BY c.rowid;";Add(command,"$run",runID);
        using var reader=command.ExecuteReader();var result=new List<ComparisonResult>();while(reader.Read()){result.Add(Deserialize<ComparisonResult>(reader.GetString(0)));}return result;
    }
    public MeasuredProfile PublishProfile(string id,string trainingID,string validationID){
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var training=this.GetMeasurement(trainingID)??throw new ArgumentException("選定用の測定がありません。");
        var validation=this.GetMeasurement(validationID)??throw new ArgumentException("検証用の測定がありません。");
        if(training.RunID==validation.RunID||training.Conditions.InputHash==validation.Conditions.InputHash){throw new InvalidOperationException("選定と独立した入力・実行で検証してください。");}
        if(training.Conditions.Adaptive||validation.Conditions.Adaptive){throw new InvalidOperationException("採用前に選定した設定を固定して検証してください。");}
        if(this.GetRun(training.RunID)?.Status!="Passed"||this.GetRun(validation.RunID)?.Status!="Passed"
            ||Comparison.Compare(training,training).Status!=ComparisonStatus.Passed||Comparison.Compare(validation,validation).Status!=ComparisonStatus.Passed){
            throw new InvalidOperationException("正常終了し7回以上測定した記録が必要です。");
        }
        var different=Comparison.Differences(training.Conditions,validation.Conditions);
        if(different.Any(static name=>name!=nameof(Conditions.InputHash))){throw new InvalidOperationException("検証は入力以外の条件をそろえてください。");}
        string RequireImprovement(Measurement measurement){
            var comparisons=this.GetComparisons(measurement.RunID);
            foreach(var comparison in comparisons.Where(result=>result.CandidateID==measurement.ID&&result.Status==ComparisonStatus.Passed&&result.TimeRatio is >0 and <0.95)){
                var baseline=this.GetMeasurement(comparison.BaselineID)!;
                var parameters=JsonSerializer.Deserialize<ProfileParameters>(baseline.Conditions.ParametersJSON,JSON);
                var baselineGrowth=JsonSerializer.Deserialize<Seamless.Execution.Growth.Descriptor>(baseline.Conditions.GrowthJSON,JSON);
                if(parameters?.ProfileID=="Minimum"&&parameters.Evidence==ProfileEvidence.Current&&Fingerprint.OfJSON(parameters.Initial??parameters.Effective)==Fingerprint.OfJSON(ProfileResolver.Minimum(baseline.Conditions.Backend))&&this.GetRun(baseline.RunID)?.Status=="Passed"
                    &&baselineGrowth?.ID=="doubling"&&baselineGrowth.Version==1
                    &&baseline.Conditions.Backend==measurement.Conditions.Backend&&baseline.Conditions.StoreKind==measurement.Conditions.StoreKind
                    &&baseline.Conditions.Implementation==measurement.Conditions.Implementation&&baseline.Conditions.Optimizations==measurement.Conditions.Optimizations
                    &&baseline.Conditions.IO==measurement.Conditions.IO
                    &&Comparison.Compare(baseline,measurement,ComparisonMode.Experiment).Status==ComparisonStatus.Passed){return baseline.RunID;}
            }
            throw new InvalidOperationException("同じ実装・入力の既定Minimum＋Doublingに対して5%以上改善した比較を、選定用と独立検証用の両方で保存してください。");
        }
        var trainingBaseline=RequireImprovement(training);
        var validationBaseline=RequireImprovement(validation);
        var parameters=JsonSerializer.Deserialize<ProfileParameters>(training.Conditions.ParametersJSON,JSON)
            ??throw new InvalidOperationException("実際のストレージ設定が記録されていません。");
        var settings=parameters.Initial??parameters.Effective;
        if(parameters.Evidence!=ProfileEvidence.Current||training.Conditions.MeasurementVersion!=ProfileEvidence.Current.MeasurementVersion
            ||training.Conditions.CodecVersion!=ProfileEvidence.Current.CodecVersion){
            throw new InvalidOperationException("ライブラリ・Codec・測定方式の現行版を確認できない旧証拠は採用できません。");
        }
        if(!Enum.TryParse<StoreKind>(training.Conditions.StoreKind,out var kind)){throw new InvalidOperationException("未対応の保存方式です。");}
        var conditions=training.Conditions;
        var environment=JsonSerializer.Deserialize<EnvironmentSnapshot>(this.GetRun(training.RunID)!.EnvironmentJSON,JSON)??throw new InvalidDataException("測定環境がありません。");
        var growth=JsonSerializer.Deserialize<Seamless.Execution.Growth.Descriptor>(conditions.GrowthJSON,JSON)??throw new InvalidDataException("拡張方式がありません。");
        var workload=conditions.WorkloadID;
        if(this.GetRun(training.RunID)!.Application.StartsWith("Scalable",StringComparison.Ordinal)&&conditions.Implementation!="Native"){workload+="."+conditions.Implementation;}
        var profile=new MeasuredProfile(id,1,new(conditions.Backend,kind,conditions.ElementType,conditions.Count,workload,environment.ProfileEnvironment,conditions.IO),conditions.Count,settings,[training.RunID,validation.RunID,trainingBaseline,validationBaseline],true,growth,parameters.Evidence);
        _=new ProfileCatalog([profile]);
        using var connection=this.database.OpenWrite();using var command=connection.CreateCommand();command.CommandText="INSERT INTO BenchmarkProfiles VALUES($id,$at,$payload);";
        Add(command,"$id",id);Add(command,"$at",DateTimeOffset.UtcNow.ToString("O"));Add(command,"$payload",Serialize(profile));command.ExecuteNonQuery();return profile;
    }
    public ProfileCatalog GetProfiles(){
        using var connection=this.database.OpenRead();using var command=connection.CreateCommand();command.CommandText="SELECT payload FROM BenchmarkProfiles ORDER BY created_at DESC,id;";
        using var reader=command.ExecuteReader();var result=new List<MeasuredProfile>();while(reader.Read()){result.Add(Deserialize<MeasuredProfile>(reader.GetString(0)));}return new(result);
    }
    private static string Serialize<T>(T value)=>JsonSerializer.Serialize(value,JSON);
    private static T Deserialize<T>(string value)=>JsonSerializer.Deserialize<T>(value,JSON)??throw new InvalidDataException("測定記録を読み取れません。");
    private static T? Read<T>(object? value) where T:class=>value is string json?Deserialize<T>(json):null;
    private static void Add(SqliteCommand command,string name,object? value)=>command.Parameters.AddWithValue(name,value??DBNull.Value);
}

public sealed record ProfileParameters(ProfileSettings? Requested,ProfileSettings Effective,string ProfileID="Minimum",string? Reason=null,int? ProfileVersion=null,string[]? EvidenceRunIDs=null,string? RequestedProfile=null,ProfileSettings? Initial=null,ProfileEvidence? Evidence=null);
