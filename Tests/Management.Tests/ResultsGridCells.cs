using System.Reflection;
using System.Text.Json;
using Seamless.Management;
using Grid=Seamless.Management.Components.結果グリッド;

namespace Management.Tests;

public sealed class ResultsGridCells{
    [Fact]public void EditingOneCellPreservesStructuredNullAndLargeNumberCells(){
        using var parsed=JsonDocument.Parse("{\"value\":{\"amount\":7},\"items\":[1,2],\"optional\":null,\"text\":\"before\",\"large\":1234567890123456789012345678901234567890}");
        var merged=Merge(parsed.RootElement,new(){{"text","after"}});
        Assert.Equal(7m,merged.GetProperty("value").GetProperty("amount").GetDecimal());Assert.Equal(2,merged.GetProperty("items").GetArrayLength());Assert.Equal(JsonValueKind.Null,merged.GetProperty("optional").ValueKind);
        Assert.Equal("after",merged.GetProperty("text").GetString());Assert.Equal(parsed.RootElement.GetProperty("large").GetRawText(),merged.GetProperty("large").GetRawText());
    }
    [Fact]public void EditedObjectAndArrayAreSentAsJsonValues(){
        var source=ManagementJson.Element(new{value=new{amount=7m},price=new{amount=13m},items=new[]{1},optional=(string?)null});
        var merged=Merge(source,new(){{"price","{\"amount\":14}"},{"items","[2,3]"}});
        Assert.Equal(7m,merged.GetProperty("value").GetProperty("amount").GetDecimal());Assert.Equal(14m,merged.GetProperty("price").GetProperty("amount").GetDecimal());
        Assert.Equal(2,merged.GetProperty("items").GetArrayLength());Assert.Equal(JsonValueKind.Null,merged.GetProperty("optional").ValueKind);
    }
    [Theory]
    [InlineData("true","System.String",JsonValueKind.String)]
    [InlineData("001","string",JsonValueKind.String)]
    [InlineData("True","System.Boolean",JsonValueKind.True)]
    [InlineData("null","System.Nullable`1[System.Int32]",JsonValueKind.Null)]
    [InlineData("42","System.Int32",JsonValueKind.Number)]
    [InlineData("{\"amount\":14}","UserCode.Payload",JsonValueKind.Object)]
    [InlineData("[1,2]","System.Int32[]",JsonValueKind.Array)]
    public void NewCellsUseDeclaredStringTypeAndJsonForStructuredValues(string input,string type,JsonValueKind expected){
        var value=(JsonElement)typeof(Grid).GetMethod("セル値",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[input,type,JsonValueKind.Undefined])!;
        Assert.Equal(expected,value.ValueKind);if(expected==JsonValueKind.String){Assert.Equal(input,value.GetString());}
    }
    private static JsonElement Merge(JsonElement values,Dictionary<string,string> edits){
        var type=typeof(Grid).GetNestedType("行モデル",BindingFlags.NonPublic)!;
        var row=Activator.CreateInstance(type,[new TableRowSnapshot(ManagementJson.Element(new{Id=1}),values,"stamp")])!;
        var changes=(Dictionary<string,string>)type.GetProperty("Values")!.GetValue(row)!;
        foreach(var pair in edits){changes.Add(pair.Key,pair.Value);}
        return(JsonElement)type.GetMethod("CreateMergedValues")!.Invoke(row,[null])!;
    }
}
