using Seamless.Execution;
using Seamless.Execution.Codecs;
using System.Runtime.InteropServices;

namespace Test.Seamless.Database.Records;

public sealed class OwnedRecordGraphTests{
    [Fact]
    public void SerializeOwned_RetainsTheOutputUntilDisposeAndReleasesFailures(){
        var budget=new ResourceBudget(1<<20,0);object[] values=[-0.0,null!];values[1]=values;var input=new Graph(values,values);
        var owner=RecordGraphSerializer<Graph>.SerializeOwned(input,budget);var length=owner.Value.Length;
        Assert.Equal(owner.MemoryBytes,budget.MemoryBytes);Assert.True(owner.MemoryBytes>length);
        var restored=RecordGraphSerializer<Graph>.Deserialize(owner.Value,budget);Assert.Same(restored.First,restored.Second);Assert.Same(restored.First,restored.First[1]);
        Assert.Equal(owner.MemoryBytes,budget.MemoryBytes);owner.Dispose();owner.Dispose();Assert.Equal(0,budget.MemoryBytes);
        Assert.Throws<ObjectDisposedException>(()=>_ = owner.Value);
        Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<Graph>.SerializeOwned(input,budget,maximumBytes:length-1));Assert.Equal(0,budget.MemoryBytes);
    }

    [Fact]
    public void DecodeOwned_RetainsTheGraphBudgetUntilDispose(){
        var budget=new ResourceBudget(1<<20,0);
        object[] values=[-0.0,null!];values[1]=values;
        var input=new Graph(values,values);
        var bytes=RecordGraphSerializer<Graph>.Serialize(input);
        var expected=RecordGraphSerializer<Graph>.GetRetainedSize(input,budget);
        var owner=RecordGraphSerializer<Graph>.DeserializeOwned(bytes,budget);
        Assert.Equal(expected,owner.MemoryBytes);Assert.Equal(expected,budget.MemoryBytes);
        Assert.Same(owner.Value.First,owner.Value.Second);Assert.Same(owner.Value.First,owner.Value.First[1]);
        Assert.NotSame(values,owner.Value.First);Assert.Equal(0,BitConverter.DoubleToInt64Bits((double)owner.Value.First[0]));
        owner.Dispose();owner.Dispose();Assert.Equal(0,budget.MemoryBytes);
        Assert.Throws<ObjectDisposedException>(()=>_ = owner.Value);
    }

    [Fact]
    public void OwnedDecode_FailuresReleaseBothReservations(){
        var bytes=RecordGraphSerializer<LargeObject>.Serialize(new());
        var budget=new ResourceBudget(2048,0);
        Assert.Throws<ResourceLimitException>(()=>RecordGraphSerializer<LargeObject>.DeserializeOwned(bytes,budget));
        Assert.Equal(0,budget.MemoryBytes);
        var largeBudget=new ResourceBudget(1<<20,0);
        Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<LargeObject>.DeserializeOwned(bytes[..^1],largeBudget));
        Assert.Equal(0,largeBudget.MemoryBytes);
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(()=>RecordGraphSerializer<LargeObject>.DeserializeOwned(bytes,budget,cancellation.Token));
        Assert.Equal(0,budget.MemoryBytes);
    }

    [Fact]
    public void ExecutionScope_OwnsDecodedValues(){
        var parent=new ResourceBudget(1<<20,0);var scope=new ExecutionScope(new(){ParentBudget=parent,MemoryLimitBytes=1<<20});
        scope.Codecs.UseRecordGraphSerialization(scope.Budget);
        var owner=scope.DecodeOwned<LargeObject>(RecordGraphSerializer<LargeObject>.Serialize(new()));
        Assert.True(parent.MemoryBytes>=4096);scope.Dispose();
        Assert.Equal(0,parent.MemoryBytes);Assert.Throws<ObjectDisposedException>(()=>_ = owner.Value);
    }

    [Theory,InlineData(false),InlineData(true)]
    public void ResultBuffers_AccountForRetainedSizeAndPreserveReferences(bool dictionary){
        using var scope=new ExecutionScope(new(){MemoryLimitBytes=1<<20});
        scope.Codecs.UseRecordGraphSerialization(scope.Budget);
        var input=new LargeObject();
        if(dictionary){
            using var buffer=new QueryDictionaryBuffer<int,LargeObject>(scope);buffer.Add(1,input);
            Assert.True(scope.Budget.MemoryBytes>=4096);var result=buffer.ToDictionary();
            Assert.Same(input,result[1]);Assert.Equal(0,scope.Budget.MemoryBytes);
        }else{
            using var buffer=new QueryResultBuffer<LargeObject>(scope);buffer.Add(input);buffer.Add(input);
            Assert.True(scope.Budget.MemoryBytes>=4096);var result=buffer.ToArray();
            Assert.Same(input,result[0]);Assert.Same(result[0],result[1]);Assert.Equal(0,scope.Budget.MemoryBytes);
        }
    }

    [Fact]
    public void BoxedScalars_AreIncludedAndOwnersAreNotRecordData(){
        object input=Int128.MaxValue;var budget=new ResourceBudget(1<<20,0);
        using var owner=RecordGraphSerializer<object>.DeserializeOwned(RecordGraphSerializer<object>.Serialize(input),budget);
        Assert.Equal(RecordGraphSerializer<object>.GetRetainedSize(input,budget),owner.MemoryBytes);
        Assert.True(owner.MemoryBytes>=128+IntPtr.Size+16+24);
        Assert.Throws<NotSupportedException>(()=>RecordGraphSerializer<object>.Serialize(owner));
        Assert.Equal(owner.MemoryBytes,budget.MemoryBytes);
    }

    [Theory,InlineData(false),InlineData(true)]
    public void RejectedResult_DoesNotChangeTheBuffer(bool dictionary){
        using var scope=new ExecutionScope(new(){MemoryLimitBytes=4096});scope.Codecs.UseRecordGraphSerialization(scope.Budget);
        if(dictionary){
            using var buffer=new QueryDictionaryBuffer<int,LargeObject>(scope);var baseline=scope.Budget.MemoryBytes;
            Assert.Throws<ResourceLimitException>(()=>buffer.Add(1,new()));Assert.Equal(0,buffer.Count);Assert.Equal(baseline,scope.Budget.MemoryBytes);
        }else{
            using var buffer=new QueryResultBuffer<LargeObject>(scope);var baseline=scope.Budget.MemoryBytes;
            Assert.Throws<ResourceLimitException>(()=>buffer.Add(new()));Assert.Equal(0,buffer.Count);Assert.Equal(baseline,scope.Budget.MemoryBytes);
        }
    }

    private readonly record struct Graph(object[] First,object[] Second);
    [StructLayout(LayoutKind.Sequential,Size=4096)]
    private sealed class LargeObject{public byte Value=1;}
}
