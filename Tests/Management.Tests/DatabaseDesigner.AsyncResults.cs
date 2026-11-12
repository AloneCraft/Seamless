using Seamless.Management;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed partial class DatabaseDesigner{
    [Theory]
    [InlineData("global::System.Threading.Tasks.Task<int>","return global::System.Threading.Tasks.Task.FromResult(id);")]
    [InlineData("global::System.Threading.Tasks.ValueTask<int>","return new global::System.Threading.Tasks.ValueTask<int>(id);")]
    [InlineData("global::System.Collections.Generic.IAsyncEnumerable<int>","return global::UserCode.AsyncRows.Read(id);")]
    [InlineData("global::System.Collections.Generic.IAsyncEnumerator<int>","return global::UserCode.AsyncRows.Read(id).GetAsyncEnumerator();")]
    [InlineData("global::UserCode.DerivedTask","return new global::UserCode.DerivedTask(id);")]
    [InlineData("global::System.Threading.Tasks.Task<int>","return default!;")]
    public async Task AsyncResultsHaveAnExplicitUnsupportedNotice(string type,string body){
        var definition=FunctionBoundaryDefinition(type,body,"""
            namespace UserCode{
             public sealed class DerivedTask(int id):global::System.Threading.Tasks.Task<int>(()=>id);
             public static class AsyncRows{
              public static async global::System.Collections.Generic.IAsyncEnumerable<int> Read(int id){await global::System.Threading.Tasks.Task.Yield();yield return id;}
             }
            }
            """);
        var target=Designer.Objects(definition).Single(item=>item.Name=="Amount");
        Assert.Equal("AsyncResultUnsupported",Designer.ReadObjectDefinition(definition,target).ResultTypeInferenceReason);
        var html=await RenderFunctionResultEditor(definition,target);
        Assert.Contains("型推論通知",html);Assert.Contains("非同期の戻り値を待機する実行は未対応です。",html);
    }

    [Theory]
    [InlineData("return global::System.Threading.Tasks.Task.FromResult(id);")]
    [InlineData("return new[]{global::System.Threading.Tasks.Task.FromResult(id)};")]
    public void InferringAnAsyncResultPreservesTheInputAndReportsItsUnsupportedContract(string body){
        var definition=FunctionResultFixture(false);var target=Designer.Objects(definition).Single(item=>item.Name=="Amount");
        var baseline=Designer.ReadObjectDefinition(definition,target);var part=baseline.Parts.Single(item=>item.Role=="Member");
        var replacement=part.Text.Replace("return id;",body,StringComparison.Ordinal);
        var edit=Designer.ApplyObjectSource(definition,baseline,part.Id,replacement);
        Assert.False(edit.Succeeded);Assert.Null(edit.Definition);
        Assert.Contains(edit.Diagnostics,item=>item.Code=="FunctionResult"&&item.Message.Contains("非同期",StringComparison.Ordinal));
        Assert.Equal(part.Text,Designer.ReadObjectDefinition(definition,target).Parts.Single(item=>item.Id==part.Id).Text);
    }

    [Fact]
    public void AUserTypeNamedTaskKeepsItsSynchronousResultContract(){
        var definition=FunctionBoundaryDefinition("global::UserCode.Task","return new global::UserCode.Task(id);","namespace UserCode{public readonly record struct Task(int Value);}");
        var target=Designer.Objects(definition).Single(item=>item.Name=="Amount");
        Assert.Null(Designer.ReadObjectDefinition(definition,target).ResultTypeInferenceReason);
    }
}
