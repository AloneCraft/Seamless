using Scope=Seamless.Execution.ExecutionScope;
using Budget=Seamless.Execution.ResourceBudget;
namespace Execution.Tests;

public sealed class QueryGrouping{
    [Fact]
    public void ManyGroupsShareConstructionAndKeepIndependentOwnedResults(){
        var parent=new Budget(2<<20,0);IGrouping<int,int>[] results;
        using(var scope=new Scope(new(){MemoryLimitBytes=2<<20,TemporaryStorageLimitBytes=0,ParentBudget=parent})){
            using var materializer=new global::Seamless.Linq.QueryGrouping.Materializer<int,int>(scope);
            results=Enumerable.Range(0,2048).GroupBy(static value=>value%1024).Select(materializer.Materialize).ToArray();
            Assert.True(parent.MemoryBytes>0);Assert.Equal(1024,results.Length);
        }
        Assert.Equal(0,parent.MemoryBytes);
        for(var key=0;key<1024;key++){Assert.Equal(key,results[key].Key);Assert.Equal(new[]{key,key+1024},results[key]);}
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedGroupRollsBackItsReservationAndKeepsEarlierGroups(bool first){
        using var scope=new Scope(new(){MemoryLimitBytes=32768,TemporaryStorageLimitBytes=0});
        using var materializer=new global::Seamless.Linq.QueryGrouping.Materializer<int,int>(scope);
        IGrouping<int,int>? prior=null;
        if(!first){prior=materializer.Materialize(new[]{7}.GroupBy(static _=>0).Single());}
        var before=scope.Budget.MemoryBytes;
        Assert.Throws<InvalidDataException>(()=>materializer.Materialize(new InterruptedGroup(static()=>throw new InvalidDataException("source failed"))));
        Assert.Equal(before,scope.Budget.MemoryBytes);
        Assert.Throws<global::Seamless.Execution.ResourceLimitException>(()=>materializer.Materialize(Enumerable.Range(0,4096).GroupBy(static _=>0).Single()));
        Assert.Equal(before,scope.Budget.MemoryBytes);
        if(prior is not null){Assert.Equal(new[]{7},prior);}
        Assert.Equal(new[]{9},materializer.Materialize(new[]{9}.GroupBy(static _=>0).Single()));
    }
    [Fact]
    public void SharedConstructionKeepsReferenceIdentityAndRejectsUseAfterScopeDisposal(){
        var scope=new Scope(new(){MemoryLimitBytes=65536,TemporaryStorageLimitBytes=0});
        var materializer=new global::Seamless.Linq.QueryGrouping.Materializer<string,Item>(scope);var key=new string('k',3);var value=new Item();
        var source=new[]{value}.GroupBy(_=>key).Single();var result=materializer.Materialize(source);
        scope.Dispose();Assert.Equal(0,scope.Budget.MemoryBytes);Assert.Same(key,result.Key);Assert.Same(value,Assert.Single(result));
        Assert.Throws<ObjectDisposedException>(()=>materializer.Materialize(source));
    }
    [Fact]
    public void SharedConstructionKeepsCustomCodecCallsOnTheProducerInElementOrder(){
        using var scope=new Scope(new(){MemoryLimitBytes=65536,TemporaryStorageLimitBytes=0});var codec=new ObservedCodec();scope.Codecs.Register<int>(codec);
        using var materializer=new global::Seamless.Linq.QueryGrouping.Materializer<string,int>(scope);
        var groups=new[]{1,2,3,4}.GroupBy(static value=>{if(value<3){return "a";}return "b";}).Select(materializer.Materialize).ToArray();
        Assert.Equal(new[]{1,2,3,4},codec.Values);Assert.All(codec.Threads,id=>Assert.Equal(Environment.CurrentManagedThreadId,id));
        Assert.Equal(new[]{1,2},groups[0]);Assert.Equal(new[]{3,4},groups[1]);
    }
    [Fact]
    public void CancellationRollsBackOnlyTheGroupUnderConstruction(){
        using var stop=new CancellationTokenSource();using var scope=new Scope(new(){MemoryLimitBytes=65536,TemporaryStorageLimitBytes=0,CancellationToken=stop.Token});
        using var materializer=new global::Seamless.Linq.QueryGrouping.Materializer<int,int>(scope);
        var prior=materializer.Materialize(new[]{7}.GroupBy(static _=>0).Single());var before=scope.Budget.MemoryBytes;
        Assert.Throws<OperationCanceledException>(()=>materializer.Materialize(new InterruptedGroup(stop.Cancel)));
        Assert.Equal(before,scope.Budget.MemoryBytes);Assert.Equal(new[]{7},prior);
    }
    [Fact]
    public void OwnedGroupsExposeTheSameReadonlyListOperationsAsLinqGroups(){
        var expected=new[]{3,1,5}.GroupBy(static _=>0).Single();IGrouping<int,int> result;
        using(var scope=new Scope(new(){MemoryLimitBytes=65536,TemporaryStorageLimitBytes=0})){
            using var materializer=new global::Seamless.Linq.QueryGrouping.Materializer<int,int>(scope);result=materializer.Materialize(expected);
        }
        var reference=Assert.IsAssignableFrom<IList<int>>(expected);var list=Assert.IsAssignableFrom<IList<int>>(result);
        Assert.True(list.IsReadOnly);Assert.Equal(reference.Count,list.Count);Assert.Equal(reference[1],list[1]);Assert.Equal(reference.IndexOf(5),list.IndexOf(5));Assert.True(list.Contains(1));
        var array=new int[5];list.CopyTo(array,1);Assert.Equal(new[]{0,3,1,5,0},array);
        Assert.Throws<NotSupportedException>(()=>list.Add(7));Assert.Throws<NotSupportedException>(()=>list.Clear());Assert.Throws<NotSupportedException>(()=>list.Remove(3));Assert.Throws<NotSupportedException>(()=>list.Insert(0,7));Assert.Throws<NotSupportedException>(()=>list.RemoveAt(0));Assert.Throws<NotSupportedException>(()=>list[0]=7);
        Assert.Throws<ArgumentOutOfRangeException>(()=>list[-1]);Assert.Throws<ArgumentOutOfRangeException>(()=>list[list.Count]);
    }
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void KnownInternalGroupCountsMaterializeBothMemoryAndSpilledGroups(int workers){
        foreach(var limit in new[]{128<<10,2<<20}){
            var parent=new Budget(2<<20,4<<20);IGrouping<int,int>[] results;
            using(var scope=new Scope(new(){MemoryLimitBytes=2<<20,TemporaryStorageLimitBytes=4<<20,MaximumQueryWorkers=workers,TemporaryDirectory=Environment.GetEnvironmentVariable("SEAMLESS_TEST_DATA_ROOT")!,ParentBudget=parent})){
                using var lookupScope=scope.CreateChild(memoryLimitBytes:limit);
                using var lookup=new global::Seamless.Linq.PartitionedLookupBuilder<int,int>(lookupScope,allowParallel:true);
                lookup.Build(static output=>{for(var value=0;value<1024;value++){output.Add(value%17,value);}});
                using var materializer=new global::Seamless.Linq.QueryGrouping.Materializer<int,int>(scope);results=lookup.Groups().Select(materializer.Materialize).ToArray();
                if(limit==(128<<10)){Assert.True(parent.PeakTemporaryStorageBytes>0);}
            }
            Assert.Equal(0,parent.MemoryBytes);Assert.Equal(0,parent.TemporaryStorageBytes);
            for(var key=0;key<17;key++){Assert.Equal(key,results[key].Key);Assert.Equal(Enumerable.Range(0,1024).Where(value=>value%17==key),results[key]);}
        }
    }
    [Fact]
    public void NullablePayloadLengthsAndUnknownCountGettersAreNotAssumedConstant(){
        using var scope=new Scope(new(){MemoryLimitBytes=65536,TemporaryStorageLimitBytes=0});using var materializer=new global::Seamless.Linq.QueryGrouping.Materializer<int,(int? Number,long Value)>(scope);
        var absent=materializer.Materialize(new[]{((int?)null,7L)}.GroupBy(static _=>0).Single());var before=scope.Budget.MemoryBytes;
        var present=materializer.Materialize(new[]{((int?)9,7L)}.GroupBy(static _=>0).Single());var presentCost=scope.Budget.MemoryBytes-before;
        var after=scope.Budget.MemoryBytes;materializer.Materialize(new[]{((int?)null,7L)}.GroupBy(static _=>0).Single());var absentCost=scope.Budget.MemoryBytes-after;
        Assert.True(presentCost>absentCost);Assert.Null(Assert.Single(absent).Number);Assert.Equal(9,Assert.Single(present).Number);
        using var values=new global::Seamless.Linq.QueryGrouping.Materializer<int,int>(scope);Assert.Equal(new[]{1,2},values.Materialize(new UntrustedCountGroup()));
    }
    private sealed class UntrustedCountGroup:IGrouping<int,int>,IReadOnlyCollection<int>{
        public int Key=>0;public int Count=>throw new InvalidOperationException("Do not inspect user Count.");
        public IEnumerator<int> GetEnumerator()=>((IEnumerable<int>)new[]{1,2}).GetEnumerator();
        global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();
    }
    private sealed class ObservedCodec:global::Seamless.Execution.Codecs.IAdapter<int>{
        internal List<int> Values{get;}=[];internal List<int> Threads{get;}=[];
        public string FormatId=>"query-group-observed-v1";
        public int GetEncodedLength(int value){this.Values.Add(value);this.Threads.Add(Environment.CurrentManagedThreadId);return sizeof(int);}
        public void Encode(int value,Span<byte> destination)=>BitConverter.TryWriteBytes(destination,value);
        public int Decode(ReadOnlySpan<byte> source)=>BitConverter.ToInt32(source);
    }
    [Fact]
    public void OwnedGroupsKeepReferencesAndReservationsUntilTheQueryFinishes(){
        var parent=new Budget(65536,0);IGrouping<string,Item> result;var key=new string('k',3);var first=new Item();var second=new Item();
        using(var scope=new Scope(new(){MemoryLimitBytes=65536,TemporaryStorageLimitBytes=0,ParentBudget=parent})){
            var source=new[]{first,second}.GroupBy(_=>key).Single();
            result=global::Seamless.Linq.QueryGrouping.Materialize(scope,source);
            Assert.True(parent.MemoryBytes>0);Assert.Same(key,result.Key);Assert.Same(first,result.First());Assert.Same(second,result.Last());
        }
        Assert.Equal(0,parent.MemoryBytes);Assert.Same(key,result.Key);Assert.Same(first,result.First());Assert.Same(second,result.Last());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceFailureAndCancellationReleaseTheGroupBeingBuilt(bool cancel){
        using var stop=new CancellationTokenSource();using var scope=new Scope(new(){MemoryLimitBytes=65536,TemporaryStorageLimitBytes=0,CancellationToken=stop.Token});
        var group=new InterruptedGroup(()=>{if(cancel){stop.Cancel();}else{throw new InvalidDataException("source failed");}});
        if(cancel){Assert.Throws<OperationCanceledException>(()=>global::Seamless.Linq.QueryGrouping.Materialize(scope,group));}
        else{Assert.Throws<InvalidDataException>(()=>global::Seamless.Linq.QueryGrouping.Materialize(scope,group));}
        Assert.Equal(0,scope.Budget.MemoryBytes);
    }
    private sealed class Item;
    private sealed class InterruptedGroup(Action interrupt):IGrouping<int,int>{
        public int Key=>0;
        public IEnumerator<int> GetEnumerator()=>this.Values().GetEnumerator();
        private IEnumerable<int> Values(){yield return 1;interrupt();yield return 2;}
        global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()=>this.GetEnumerator();
    }
}
