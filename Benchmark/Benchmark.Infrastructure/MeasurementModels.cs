using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Benchmark.Infrastructure;

public sealed record Run(string ID,string Application,string Status,string SettingsJSON,string EnvironmentJSON,string CodeFingerprint,DateTimeOffset CreatedAt,DateTimeOffset? CompletedAt=null);

public sealed record Conditions(
    string WorkloadID,string InputHash,long Count,string ElementType,string Backend,string StoreKind,
    string Implementation,string Optimizations,string ParametersJSON,string GrowthJSON,string EnvironmentKey,
    string MeasurementVersion="balanced-v1",string SemanticsVersion="1",string CodecVersion="1",bool Adaptive=false,bool Known=true,string IO="BackendDefault",string StorageKey="Unknown"){
    [System.Text.Json.Serialization.JsonIgnore]public string ScopeKey{
        get{
            var json=(JsonObject)JsonSerializer.SerializeToNode(this)!;
            if(this.IO=="BackendDefault"){json.Remove(nameof(this.IO));}
            if(this.StorageKey=="Unknown"){json.Remove(nameof(this.StorageKey));}
            return Fingerprint.Of(Fingerprint.CanonicalJSON(json.ToJsonString()));
        }
    }
}

public sealed record Sample(double ElapsedNanoseconds,long OperationCount,long AllocatedBytes,
    long? ProcessBytes=null,long? DiskBytes=null,int Gen0=0,int Gen1=0,int Gen2=0,
    long? ObservedIntervalWorkingSetBytes=null,long? ReservedPeakMemoryBytes=null,long? ReservedPeakTemporaryStorageBytes=null,double? VerificationNanoseconds=null){
    /// <summary>OSが報告するプロセス生存期間全体の高水位。区間ピークではない。</summary>
    [System.Text.Json.Serialization.JsonIgnore]public long? ProcessLifetimePeakWorkingSetBytes=>this.ProcessBytes;
    [System.Text.Json.Serialization.JsonIgnore]public double NanosecondsPerOperation=>this.ElapsedNanoseconds/this.OperationCount;
    [System.Text.Json.Serialization.JsonIgnore]public double AllocatedBytesPerOperation=>(double)this.AllocatedBytes/this.OperationCount;
}

public sealed record Measurement(string ID,string RunID,Conditions Conditions,string Digest,string Status,Sample[] Samples,
    DateTimeOffset CreatedAt,long Interval=0,double TransitionNanoseconds=0,string DiagnosticsJSON="{}",CapacityOutcome? Capacity=null){
    public static Measurement Create(string id,string runID,Conditions conditions,string digest,Sample[] samples)=>new(id,runID,conditions,digest,"Passed",samples,DateTimeOffset.UtcNow);
    [System.Text.Json.Serialization.JsonIgnore]public double MedianNanoseconds=>Statistics.Median(this.Samples.Select(static sample=>sample.NanosecondsPerOperation));
    [System.Text.Json.Serialization.JsonIgnore]public double MedianAllocatedBytes=>Statistics.Median(this.Samples.Select(static sample=>sample.AllocatedBytesPerOperation));
    [System.Text.Json.Serialization.JsonIgnore]public double StandardDeviation=>Statistics.StandardDeviation(this.Samples.Select(static sample=>sample.NanosecondsPerOperation));
}
public sealed record CapacityOutcome(long MaximumCount,string FailureKind);

public sealed record ConfigurationChange(string ID,string RunID,long Interval,string BeforeJSON,string AfterJSON,string Reason,
    bool Applied,double? TransitionNanoseconds,string DigestBefore,string DigestAfter,DateTimeOffset CreatedAt);
public enum ComparisonMode{Regression,Experiment}
public enum ComparisonStatus{Passed,Regressed,Incompatible,Incomplete,CorrectnessFailure,Inconclusive}
public sealed record ComparisonResult(string ID,string BaselineID,string CandidateID,ComparisonMode Mode,ComparisonStatus Status,
    double Tolerance,double? TimeRatio,double? AllocationRatio,string Reason,string[] ChangedConditions,DateTimeOffset CreatedAt,string? PolicyVersion=null);

public static class Statistics{
    public static double Median(IEnumerable<double> values){
        var sorted=values.Order().ToArray();
        if(sorted.Length==0){return double.NaN;}
        var middle=sorted.Length/2;
        if(sorted.Length%2==1){return sorted[middle];}
        return sorted[middle-1]/2+sorted[middle]/2;
    }
    public static double StandardDeviation(IEnumerable<double> values){
        var samples=values.ToArray();
        if(samples.Length==0){return double.NaN;}
        var mean=samples.Average();
        return Math.Sqrt(samples.Select(value=>(value-mean)*(value-mean)).Average());
    }
}

public static class Fingerprint{
    public static string Of(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static string OfJSON<T>(T value)=>Of(CanonicalJSON(JsonSerializer.Serialize(value)));
    public static string CanonicalJSON(string json)=>Sort(JsonNode.Parse(json))?.ToJsonString()??"null";
    private static JsonNode? Sort(JsonNode? node){
        if(node is JsonObject obj){
            var result=new JsonObject();
            foreach(var pair in obj.OrderBy(static pair=>pair.Key,StringComparer.Ordinal)){result.Add(pair.Key,Sort(pair.Value));}
            return result;
        }
        if(node is JsonArray array){return new JsonArray(array.Select(Sort).ToArray());}
        return node?.DeepClone();
    }
}
