using Benchmark.Scalable.Core;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Benchmark.Scalable.Tests;

public sealed class 大規模外部実行設定{
    [Fact]
    public void 読み取る(){
        var JSON=new JsonSerializerOptions{Converters={new JsonStringEnumConverter()}};
        var 設定=JsonSerializer.SerializeToElement(new{要求=new ScalableRequest()},JSON);
        Assert.Equal(4096,UI.大規模外部実行設定.読み取る(設定).要求.Count);
        Assert.Throws<JsonException>(()=>UI.大規模外部実行設定.読み取る(JsonSerializer.SerializeToElement(new{})));
        Assert.Throws<InvalidDataException>(()=>UI.大規模外部実行設定.読み取る(JsonSerializer.SerializeToElement(new{要求=new{Count=1}})));
        var 任意パス=JsonSerializer.SerializeToElement(new{要求=new ScalableRequest(TemporaryDirectory:Path.GetTempPath())},JSON);
        Assert.Throws<InvalidDataException>(()=>UI.大規模外部実行設定.読み取る(任意パス));
        var 未対応=JsonSerializer.SerializeToNode(new{要求=new ScalableRequest()},JSON)!;
        未対応["eval"]="anything";
        Assert.Throws<JsonException>(()=>UI.大規模外部実行設定.読み取る(JsonSerializer.SerializeToElement(未対応)));
    }
}
