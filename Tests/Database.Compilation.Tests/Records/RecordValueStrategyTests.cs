using Seamless.Execution;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Test.Seamless.Database.Records;

public sealed class RecordValueStrategyTests{
    [Fact,Trait("Category","Unit")]
    public void Normalize_PreservesSharingCyclesAndPrivateFieldsWithoutChangingInput(){
        var samples=new[]{BitConverter.Int64BitsToDouble(long.MinValue),BitConverter.Int64BitsToDouble(0x7ff0000000000001)};
        var node=new Node(samples[0],samples);
        node.Link(node);
        var original=new Graph(node,node,samples);
        var normalized=RecordValueStrategy<Graph>.Normalize(original);

        Assert.NotSame(node,normalized.First);
        Assert.Same(normalized.First,normalized.Second);
        Assert.Same(normalized.First,normalized.First.Next);
        Assert.NotSame(samples,normalized.Samples);
        Assert.Same(normalized.Samples,normalized.First.Samples);
        Assert.Equal(0,BitConverter.DoubleToInt64Bits(normalized.First.Number));
        Assert.Equal(0,BitConverter.DoubleToInt64Bits(normalized.Samples[0]));
        Assert.Equal(0x7ff8000000000000,BitConverter.DoubleToInt64Bits(normalized.Samples[1]));
        Assert.Equal(long.MinValue,BitConverter.DoubleToInt64Bits(node.Number));
        Assert.Equal(long.MinValue,BitConverter.DoubleToInt64Bits(samples[0]));
        Assert.Same(node,node.Next);
    }

    [Fact,Trait("Category","Unit")]
    public void Comparison_UsesNormalizedValuesAndTheReferenceTopology(){
        var first=new Node(BitConverter.Int64BitsToDouble(long.MinValue),[]);
        first.Link(first);
        var second=new Node(0,[]);
        second.Link(second);
        var left=new Pair<Node>(first,first);
        var equal=new Pair<Node>(second,second);
        Assert.True(RecordValueStrategy<Pair<Node>>.Equals(left,equal));
        Assert.Equal(RecordValueStrategy<Pair<Node>>.GetHashCode(left),RecordValueStrategy<Pair<Node>>.GetHashCode(equal));

        var other=new Node(0,[]);
        other.Link(other);
        Assert.False(RecordValueStrategy<Pair<Node>>.Equals(left,new(second,other)));
        second.Link(other);
        other.Link(second);
        Assert.False(RecordValueStrategy<Pair<Node>>.Equals(left,new(second,second)));
    }

    [Fact,Trait("Category","Unit")]
    public void Strings_CompareByContentRegardlessOfSharing(){
        var text=new string('x',7);
        var shared=new Pair<string>(text,text);
        var separate=new Pair<string>(new string('x',7),new string('x',7));
        Assert.True(RecordValueStrategy<Pair<string>>.Equals(shared,separate));
        Assert.Equal(RecordValueStrategy<Pair<string>>.GetHashCode(shared),RecordValueStrategy<Pair<string>>.GetHashCode(separate));
    }

    [Fact,Trait("Category","Unit")]
    public void BoxedValues_PreserveTheirReferenceSharing(){
        object value=BitConverter.Int64BitsToDouble(long.MinValue);
        var original=new Pair<object>(value,value);
        var normalized=RecordValueStrategy<Pair<object>>.Normalize(original);
        Assert.NotSame(value,normalized.First);
        Assert.Same(normalized.First,normalized.Second);
        Assert.Equal(0,BitConverter.DoubleToInt64Bits((double)normalized.First));
        Assert.True(RecordValueStrategy<Pair<object>>.Equals(original,normalized));
        Assert.False(RecordValueStrategy<Pair<object>>.Equals(original,new((object)0.0,(object)0.0)));
    }

    [Fact,Trait("Category","Unit")]
    public void Arrays_PreserveRankBoundsAndValues(){
        var input=(double[,])Array.CreateInstance(typeof(double),[2,1],[-1,3]);
        input[-1,3]=BitConverter.Int64BitsToDouble(long.MinValue);
        input[0,3]=2.5;
        var normalized=RecordValueStrategy<double[,]>.Normalize(input);
        Assert.NotSame(input,normalized);
        Assert.Equal(-1,normalized.GetLowerBound(0));
        Assert.Equal(3,normalized.GetLowerBound(1));
        Assert.Equal(0,BitConverter.DoubleToInt64Bits(normalized[-1,3]));
        Assert.Equal(2.5,normalized[0,3]);
        Assert.True(RecordValueStrategy<double[,]>.Equals(input,normalized));
        Assert.Equal(RecordValueStrategy<double[,]>.GetHashCode(input),RecordValueStrategy<double[,]>.GetHashCode(normalized));
    }

    [Fact,Trait("Category","Unit")]
    public void BudgetAndCancellation_ReleaseWorkReservations(){
        var budget=new ResourceBudget(32,0);
        Assert.Throws<ResourceLimitException>(()=>RecordValueStrategy<double[]>.Normalize(new double[100],budget));
        Assert.Equal(0,budget.MemoryBytes);
        using var cancellation=new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(()=>RecordValueStrategy<double[]>.Normalize([],budget,cancellation.Token));
        Assert.Equal(0,budget.MemoryBytes);
    }

    [Fact,Trait("Category","Unit")]
    public void ArraySelfCycle_IsPreserved(){
        var input=new object[1];
        input[0]=input;
        var normalized=RecordValueStrategy<object[]>.Normalize(input);
        Assert.NotSame(input,normalized);
        Assert.Same(normalized,normalized[0]);
        Assert.True(RecordValueStrategy<object[]>.Equals(input,normalized));
        Assert.Equal(RecordValueStrategy<object[]>.GetHashCode(input),RecordValueStrategy<object[]>.GetHashCode(normalized));
    }

    [Fact,Trait("Category","Unit")]
    public void SameInstanceAcrossComparisons_StillRegistersItsChildren(){
        var child=new Node(0,[]);
        var parent=new Node(1,[]);
        parent.Link(child);
        var separate=new Node(0,[]);
        Assert.False(RecordValueStrategy<Pair<Node>>.Equals(new(parent,child),new(parent,separate)));
    }

    [Fact,Trait("Category","Unit")]
    public void NullablePrivateStruct_NormalizesItsFields(){
        Timed? input=new(new DateTime(2026,11,7,12,0,0),2.3000m);
        var normalized=RecordValueStrategy<Timed?>.Normalize(input);
        Assert.Equal(DateTimeKind.Utc,normalized!.Value.Time.Kind);
        Assert.Equal(0x00010000,decimal.GetBits(normalized.Value.Amount)[3]);
        Assert.True(RecordValueStrategy<Timed?>.Equals(input,normalized));
        Assert.Equal(RecordValueStrategy<Timed?>.GetHashCode(input),RecordValueStrategy<Timed?>.GetHashCode(normalized));
        Assert.Null(RecordValueStrategy<Timed?>.Normalize(null));
    }

    [Fact,Trait("Category","Unit")]
    public void InheritedPrivateFields_AreNormalized(){
        var original=new Derived(BitConverter.Int64BitsToDouble(long.MinValue));
        var normalized=RecordValueStrategy<Derived>.Normalize(original);
        Assert.NotSame(original,normalized);
        Assert.Equal(0,BitConverter.DoubleToInt64Bits(normalized.Number));
        Assert.Equal(long.MinValue,BitConverter.DoubleToInt64Bits(original.Number));
    }

    [Fact,Trait("Category","Unit")]
    public void ExplicitLayoutSize_IsIncludedInTheBudget(){
        var budget=new ResourceBudget(4096,0);
        Assert.Throws<ResourceLimitException>(()=>RecordValueStrategy<LargeLayout>.Normalize(new(){Number=1},budget));
        Assert.Equal(0,budget.MemoryBytes);
    }

    [Fact,Trait("Category","Unit")]
    public void OverlappingFields_RequireAnExplicitAdapter(){
        var input=new Overlapping{Number=BitConverter.Int64BitsToDouble(long.MinValue)};
        Assert.Equal(long.MinValue,input.Bits);
        Assert.Throws<NotSupportedException>(()=>RecordValueStrategy<Overlapping>.Normalize(input));
    }

    [Fact,Trait("Category","Unit")]
    public void UnsupportedChild_ReleasesReservationsAfterPartialTraversal(){
        var budget=new ResourceBudget(16*1024,0);
        var samples=new[]{BitConverter.Int64BitsToDouble(long.MinValue)};
        object[] input=[samples,(Action)(static()=>{})];
        Assert.Throws<NotSupportedException>(()=>RecordValueStrategy<object[]>.Normalize(input,budget));
        Assert.Equal(0,budget.MemoryBytes);
        Assert.Same(samples,input[0]);
        Assert.Equal(long.MinValue,BitConverter.DoubleToInt64Bits(samples[0]));
    }

    [Theory,Trait("Category","Unit")]
    [InlineData("normalize")]
    [InlineData("equal")]
    [InlineData("hash")]
    public void LargeValue_WorkIsBudgetedBeforeBoxing(string operation){
        var input=new LargeValue{Number=1};
        _=RecordValueStrategy<LargeValue>.Normalize(input);
        var budget=new ResourceBudget(4096,0);
        Action action=operation switch{
            "equal"=>()=>RecordValueStrategy<LargeValue>.Equals(input,input,budget),
            "hash"=>()=>RecordValueStrategy<LargeValue>.GetHashCode(input,budget),
            _=>()=>RecordValueStrategy<LargeValue>.Normalize(input,budget)
        };
        var before=GC.GetAllocatedBytesForCurrentThread();
        Assert.Throws<ResourceLimitException>(action);
        var allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Assert.True(allocated<8192,$"Allocated {allocated} bytes before rejecting a value larger than the work budget.");
        Assert.Equal(0,budget.MemoryBytes);
    }

    [Fact,Trait("Category","Unit")]
    public void TypeCaches_DoNotKeepCollectibleTypesAlive(){
        var reference=UseCollectibleType();
        for(var attempt=0;attempt<10&&reference.IsAlive;attempt++){
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Assert.False(reference.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference UseCollectibleType(){
        var assembly=AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("RecordCache_"+Guid.NewGuid().ToString("N")),AssemblyBuilderAccess.RunAndCollect);
        var module=assembly.DefineDynamicModule("Records");
        var builder=module.DefineType("Node",TypeAttributes.Public|TypeAttributes.Sealed);
        var number=builder.DefineField("number",typeof(double),FieldAttributes.Private|FieldAttributes.InitOnly);
        var next=builder.DefineField("next",builder,FieldAttributes.Private|FieldAttributes.InitOnly);
        var constructor=builder.DefineConstructor(MethodAttributes.Public,CallingConventions.Standard,[typeof(double)]);
        var il=constructor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call,typeof(object).GetConstructor(Type.EmptyTypes)!);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld,number);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Stfld,next);
        il.Emit(OpCodes.Ret);
        var type=builder.CreateType()!;
        var original=Activator.CreateInstance(type,BitConverter.Int64BitsToDouble(long.MinValue))!;
        var normalized=RecordValueStrategy<object>.Normalize(original);
        Assert.True(RecordValueStrategy<object>.Equals(original,normalized));
        Assert.Equal(RecordValueStrategy<object>.GetHashCode(original),RecordValueStrategy<object>.GetHashCode(normalized));
        return new WeakReference(type);
    }

    private readonly record struct Graph(Node First,Node Second,double[] Samples);
    private readonly record struct Pair<T>(T First,T Second);
    private readonly struct Timed(DateTime time,decimal amount){
        private readonly DateTime time=time;
        private readonly decimal amount=amount;
        public DateTime Time=>this.time;
        public decimal Amount=>this.amount;
    }
    private class Base(double number){
        private readonly double number=number;
        public double Number=>this.number;
    }
    private sealed class Derived(double number):Base(number);
    [StructLayout(LayoutKind.Explicit,Size=8192)]
    private sealed class LargeLayout{
        [FieldOffset(0)]public double Number;
    }
    [StructLayout(LayoutKind.Explicit)]
    private struct Overlapping{
        [FieldOffset(0)]public double Number;
        [FieldOffset(0)]public long Bits;
    }
    [StructLayout(LayoutKind.Sequential,Size=8192)]
    private struct LargeValue{
        public double Number;
    }
    private sealed class Node(double number,double[] samples){
        private readonly double number=number;
        private readonly double[] samples=samples;
        private Node? next;
        public double Number=>this.number;
        public double[] Samples=>this.samples;
        public Node? Next=>this.next;
        public void Link(Node value)=>this.next=value;
    }
}
