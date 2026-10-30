using Benchmark.Scalable.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Benchmark.Scalable.UI;

public sealed record 大規模外部実行設定{
    public required ScalableRequest 要求{get;init;}
    public static 大規模外部実行設定 読み取る(JsonElement 設定){
        var JSON=new JsonSerializerOptions{UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,Converters={new JsonStringEnumConverter()}};
        var 結果=設定.Deserialize<大規模外部実行設定>(JSON)??throw new InvalidDataException("要求が必要です。");
        if(結果.要求 is null){throw new InvalidDataException("要求が必要です。");}
        var 要求JSON=設定.GetProperty(nameof(要求));
        foreach(var キー in new[]{"Count","Seed","Backend","Kind","Workload","Profile","Growth","MemoryLimitBytes","DiskLimitBytes","Samples","Adaptive","ChunkSize","Implementation","IOMode","AccessPattern"}){
            if(!要求JSON.TryGetProperty(キー,out _)){throw new InvalidDataException("実行設定を明示してください: "+キー);}
        }
        if(結果.要求.TemporaryDirectory is not null||!Enum.IsDefined(結果.要求.Workload)||!Enum.IsDefined(結果.要求.Profile)||!Enum.IsDefined(結果.要求.Implementation)){
            throw new InvalidDataException("未対応の実行設定です。TemporaryDirectory の外部指定は受け付けません。");
        }
        結果.要求.Validate();
        return 結果;
    }
}
