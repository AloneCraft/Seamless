using Seamless.Execution;
using Seamless.Execution.Codecs;
using Seamless.Transactions.Collections;
using System.Buffers.Binary;

namespace Transactions.Tests;

public sealed class OwnedChangeSetTests{
    [Theory,InlineData(1048576),InlineData(1)]
    public void EncodedChanges_IsolateInputsReadsAndSnapshots(long limit){
        using var scope=Scope();using var changes=new TransactionChangeSet<int,Row>(scope,limit);
        object[] values=[-0.0,null!];values[1]=values;changes.Set(1,new(false,new(values,values)));
        values[0]=99.0;
        Assert.True(changes.TryGetValue(1,out var first));Assert.Equal(0,(double)first.Record.First[0]);
        Assert.Same(first.Record.First,first.Record.Second);Assert.Same(first.Record.First,first.Record.First[1]);
        first.Record.First[0]=77.0;Assert.True(changes.TryGetValue(1,out var second));Assert.Equal(0,(double)second.Record.First[0]);
        using var snapshot=changes.CreateSnapshot();changes.Set(1,new(false,CreateRow(2)));
        Assert.True(snapshot.TryGetValue(1,out var original));Assert.Equal(0,(double)original.Record.First[0]);
        Assert.True(changes.TryGetValue(1,out var updated));Assert.Equal(2,(double)updated.Record.First[0]);
    }

    [Theory,InlineData(1048576),InlineData(1)]
    public void Enumeration_OwnsCurrentValuesAndReleasesThemOnDispose(long limit){
        using var scope=Scope();using var changes=new TransactionChangeSet<int,Row>(scope,limit);
        changes.Set(1,new(false,CreateRow(1)));var baseline=scope.Budget.MemoryBytes;
        using(var iterator=changes.Enumerate().GetEnumerator()){
            Assert.True(iterator.MoveNext());
            Assert.True(scope.Budget.MemoryBytes>=baseline+RecordGraphSerializer<Row>.GetRetainedSize(CreateRow(1),scope.Budget));
            Assert.Equal(1,(double)iterator.Current.Value.Record.First[0]);
        }
        Assert.Equal(baseline,scope.Budget.MemoryBytes);
        changes.Set(1,new(true,default));Assert.True(changes.TryGetValue(1,out var deleted));Assert.True(deleted.Deleted);
    }

    [Fact]
    public void FailedSpill_PreservesTheOwnedImage(){
        using var scope=Scope(0);using var changes=new TransactionChangeSet<int,Row>(scope,1<<20);
        changes.Set(1,new(false,CreateRow(1)));var baseline=scope.Budget.MemoryBytes;
        Assert.Throws<ResourceLimitException>(()=>changes.CreateHistoryPoint());
        Assert.Equal(baseline,scope.Budget.MemoryBytes);Assert.True(changes.TryGetValue(1,out var row));
        Assert.Equal(1,(double)row.Record.First[0]);changes.Set(1,new(false,CreateRow(2)));
        Assert.True(changes.TryGetValue(1,out var updated));Assert.Equal(2,(double)updated.Record.First[0]);
    }

    [Fact]
    public void ScopeDisposal_ReleasesUnclosedChangeSets(){
        var parent=new ResourceBudget(2<<20,4<<20);var scope=new ExecutionScope(new(){ParentBudget=parent,MemoryLimitBytes=2<<20,TemporaryStorageLimitBytes=4<<20});
        scope.Codecs.UseRecordGraphSerialization(scope.Budget);var changes=new TransactionChangeSet<int,Row>(scope,1<<20);
        changes.Set(1,new(false,CreateRow(1)));Assert.True(parent.MemoryBytes>0);scope.Dispose();
        Assert.Equal(0,parent.MemoryBytes);Assert.Equal(0,parent.TemporaryStorageBytes);
        Assert.Throws<ObjectDisposedException>(()=>changes.TryGetValue(1,out _));changes.Dispose();
    }

    [Fact]
    public void EncodeFailure_ReleasesTheUnpublishedBuffer(){
        using var scope=new ExecutionScope(new(){MemoryLimitBytes=1<<20});var codec=new FailingCodec();scope.Codecs.Register<int>(codec);
        using var changes=new TransactionChangeSet<string,int>(scope,1<<16);changes.Set("a",new(false,1));var baseline=scope.Budget.MemoryBytes;
        codec.Fail=true;Assert.Throws<InvalidOperationException>(()=>changes.Set("a",new(false,2)));
        Assert.Equal(baseline,scope.Budget.MemoryBytes);Assert.True(changes.TryGetValue("a",out var row));Assert.Equal(1,row.Record);
    }

    [Theory,InlineData(false,0),InlineData(false,1),InlineData(false,2),InlineData(true,0),InlineData(true,1),InlineData(true,2)]
    public void SpillWriteFailure_RestoresFileLengthsAndTheSavepoint(bool history,int stage){
        using var scope=Scope();using var changes=new TransactionChangeSet<int,Row>(scope,1);
        changes.Set(1,new(false,CreateRow(1)));
        TransactionChangeSet<int,Row>.HistoryPoint? point=null;if(history){point=changes.CreateHistoryPoint();}
        using(point){
            var beforeIndex=checked(4+scope.Codecs.Get<int>().GetEncodedLength(2)+4+1+scope.Codecs.Get<Row>().GetEncodedLength(CreateRow(2)));
            var available=beforeIndex+10;
            if(stage==0){available=0;}
            else if(stage==1){available=4+scope.Codecs.Get<int>().GetEncodedLength(2)+2;}
            if(history){available+=scope.Codecs.Get<int>().GetEncodedLength(2)+16;}
            using(var pressure=scope.Budget.ReserveTemporaryStorage(scope.Budget.TemporaryStorageLimitBytes-scope.Budget.TemporaryStorageBytes-available)){
                var occupied=scope.Budget.TemporaryStorageBytes;
                Assert.Throws<ResourceLimitException>(()=>changes.Set(2,new(false,CreateRow(2))));
                Assert.Equal(occupied,scope.Budget.TemporaryStorageBytes);
                Assert.Single(changes.Enumerate());Assert.False(changes.TryGetValue(2,out _));
            }
            changes.Set(2,new(false,CreateRow(2)));Assert.Equal(2,changes.Enumerate().Count());
            // A failed index insertion must also remain valid when the index grows.
            for(var key=3;key<=140;key++){changes.Set(key,new(false,CreateRow(key)));}
            Assert.Equal(140,changes.Enumerate().Count());
            if(point is not null){changes.RollbackToHistoryPoint(point);Assert.Single(changes.Enumerate());Assert.False(changes.TryGetValue(2,out _));}
        }
    }

    private static ExecutionScope Scope(long storage=4<<20){
        var scope=new ExecutionScope(new(){MemoryLimitBytes=2<<20,TemporaryStorageLimitBytes=storage});scope.Codecs.UseRecordGraphSerialization(scope.Budget);return scope;
    }
    private static Row CreateRow(double value){object[] values=[value,null!];values[1]=values;return new(values,values);}
    private readonly record struct Row(object[] First,object[] Second);
    private sealed class FailingCodec:IAdapter<int>{
        internal bool Fail;
        public string FormatId=>"retained-failure-v1";
        public int GetEncodedLength(int value)=>4;
        public void Encode(int value,Span<byte> destination){if(this.Fail){throw new InvalidOperationException("injected");}BinaryPrimitives.WriteInt32LittleEndian(destination,value);}
        public int Decode(ReadOnlySpan<byte> source)=>BinaryPrimitives.ReadInt32LittleEndian(source);
    }
}
