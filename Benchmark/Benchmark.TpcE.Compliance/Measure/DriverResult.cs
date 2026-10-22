using System.Text.Json;
using System.Text.Json.Serialization;
namespace Benchmark.TpcE.Compliance;
/// <summary>運転側(EGenDriver.exe)が出す結果 JSON。</summary>
internal sealed class DriverResult{
    [JsonPropertyName("customers")]public long Customers{get;init;}
    [JsonPropertyName("scale_factor")]public int ScaleFactor{get;init;}
    [JsonPropertyName("days")]public int Days{get;init;}
    [JsonPropertyName("users")]public int Users{get;init;}
    [JsonPropertyName("target_tpse")]public double TargetTpse{get;init;}
    [JsonPropertyName("measure_seconds")]public double MeasureSeconds{get;init;}
    [JsonPropertyName("tpsE")]public double TpsE{get;init;}
    [JsonPropertyName("nominal_tpsE")]public double NominalTpsE{get;init;}
    [JsonPropertyName("fatal")]public bool Fatal{get;init;}
    /// <summary>応答を受けた中で最大の、SUT が確定済みと返した書き込みの番号。強制停止後に残っているべき下限。</summary>
    [JsonPropertyName("last_acked_wseq")]public long LastAckedWriteSequence{get;init;}
    [JsonPropertyName("transactions")]public List<DriverTransaction> Transactions{get;init;}=[];
    [JsonPropertyName("trade_result_per_second")]public List<long> TradeResultPerSecond{get;init;}=[];
    [JsonPropertyName("errors")]public List<DriverError> Errors{get;init;}=[];
    public static DriverResult Parse(string json)=>JsonSerializer.Deserialize<DriverResult>(json) ?? throw new InvalidDataException("The driver result is empty.");
    public DriverTransaction? Find(string name){
        foreach(var transaction in this.Transactions){
            if(transaction.Name==name){
                return transaction;
            }
        }
        return null;
    }
}
internal sealed class DriverTransaction{
    [JsonPropertyName("name")]public string Name{get;init;}="";
    [JsonPropertyName("count")]public long Count{get;init;}
    [JsonPropertyName("failed")]public long Failed{get;init;}
    [JsonPropertyName("mix_percent")]public double MixPercent{get;init;}
    [JsonPropertyName("avg_s")]public double AverageSeconds{get;init;}
    [JsonPropertyName("p90_s")]public double P90Seconds{get;init;}
    [JsonPropertyName("min_s")]public double MinimumSeconds{get;init;}
    [JsonPropertyName("max_s")]public double MaximumSeconds{get;init;}
}
internal sealed class DriverError{
    [JsonPropertyName("message")]public string Message{get;init;}="";
    [JsonPropertyName("count")]public long Count{get;init;}
}
