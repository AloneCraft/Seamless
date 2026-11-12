using Seamless.Execution;
using Seamless.Execution.Codecs;
using Seamless.Collections.Tsavorite;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Reflection;

namespace Test.Seamless.Database.Records;

public sealed class RecordGraphSerializerTests{
    [Fact,Trait("Category","Unit")]
    public void RoundTrip_PreservesSharedPrivateReadonlyFieldsAndCycles(){
        var values=new[]{BitConverter.Int64BitsToDouble(long.MinValue),2.5};
        var node=new Node(values);node.Link(node);
        var input=new Graph(node,node,values);
        var bytes=RecordGraphSerializer<Graph>.Serialize(input);
        var result=RecordGraphSerializer<Graph>.Deserialize(bytes);
        Assert.NotSame(node,result.First);
        Assert.Same(result.First,result.Second);
        Assert.Same(result.First,result.First.Next);
        Assert.Same(result.Values,result.First.Values);
        Assert.Equal(0,BitConverter.DoubleToInt64Bits(result.Values[0]));
        Assert.Equal(long.MinValue,BitConverter.DoubleToInt64Bits(values[0]));
        Assert.True(RecordValueStrategy<Graph>.Equals(input,result));
        Assert.Equal(bytes,RecordGraphSerializer<Graph>.Serialize(result));
    }

    [Fact,Trait("Category","Unit")]
    public void RoundTrip_PreservesDistinctReferencesAndBoxedValueSharing(){
        object boxed=BitConverter.Int64BitsToDouble(long.MinValue);
        object[] input=[boxed,boxed,new[]{1},new[]{1},null!,new string('x',3)];
        var result=RecordGraphSerializer<object[]>.Deserialize(RecordGraphSerializer<object[]>.Serialize(input));
        Assert.Same(result[0],result[1]);
        Assert.NotSame(boxed,result[0]);
        Assert.NotSame(result[2],result[3]);
        Assert.Null(result[4]);
        Assert.Equal("xxx",result[5]);
        Assert.True(RecordValueStrategy<object[]>.Equals(input,result));
    }

    [Fact,Trait("Category","Unit")]
    public void RoundTrip_ArraySelfCycleAndNonzeroBounds(){
        var input=(object[,])Array.CreateInstance(typeof(object),[2,1],[-1,3]);
        input[-1,3]=input;input[0,3]=12.00m;
        var result=RecordGraphSerializer<object[,]>.Deserialize(RecordGraphSerializer<object[,]>.Serialize(input));
        Assert.Same(result,result[-1,3]);
        Assert.Equal(-1,result.GetLowerBound(0));
        Assert.Equal(3,result.GetLowerBound(1));
        Assert.Equal(decimal.GetBits(12m),decimal.GetBits((decimal)result[0,3]));
    }

    [Fact,Trait("Category","Unit")]
    public void RoundTrip_CanonicalizesAllScalarFamiliesAndNullable(){
        var input=new Scalars(-0.0f,BitConverter.UInt16BitsToHalf(0x8000),12.00m,
            new DateTime(2026,11,7,0,0,0,DateTimeKind.Unspecified),new DateTimeOffset(2026,11,7,9,0,0,TimeSpan.FromHours(9)),null,
            Guid.NewGuid(),new DateOnly(2026,11,7),new TimeOnly(12,34),Int128.MinValue,UInt128.MaxValue,Sample.Two);
        var result=RecordGraphSerializer<Scalars>.Deserialize(RecordGraphSerializer<Scalars>.Serialize(input));
        Assert.True(RecordValueStrategy<Scalars>.Equals(input,result));
        Assert.Equal(0,BitConverter.SingleToInt32Bits(result.Single));
        Assert.Equal(0,BitConverter.HalfToUInt16Bits(result.Half));
        Assert.Equal(decimal.GetBits(12m),decimal.GetBits(result.Decimal));
        Assert.Equal(DateTimeKind.Utc,result.Date.Kind);
        Assert.Equal(TimeSpan.Zero,result.Offset.Offset);
        Assert.Null(result.Nullable);
        Assert.Equal(3.5m,RecordGraphSerializer<decimal?>.Deserialize(RecordGraphSerializer<decimal?>.Serialize(3.50m)));
    }

    [Fact,Trait("Category","Unit")]
    public void Strings_PreserveOrdinalUtf16AndIgnoreSharing(){
        var text="x\ud800\0\udfff";
        var first=new Pair<string>(text,text);
        var second=new Pair<string>(new(text.ToCharArray()),new(text.ToCharArray()));
        var bytes=RecordGraphSerializer<Pair<string>>.Serialize(first);
        Assert.Equal(bytes,RecordGraphSerializer<Pair<string>>.Serialize(second));
        Assert.Equal(text,RecordGraphSerializer<Pair<string>>.Deserialize(bytes).First);
    }

    [Fact,Trait("Category","Unit")]
    public void EachMessage_OwnsAnIndependentGraph(){
        var input=new Pair<double[]>(new[]{-0.0},new[]{1.0});
        var bytes=RecordGraphSerializer<Pair<double[]>>.Serialize(input);
        var first=RecordGraphSerializer<Pair<double[]>>.Deserialize(bytes);
        var second=RecordGraphSerializer<Pair<double[]>>.Deserialize(bytes);
        first.First[0]=7;
        Assert.Equal(0,second.First[0]);
        Assert.Equal(0,input.First[0]);
        Assert.NotSame(first.Second,second.Second);
    }

    [Fact,Trait("Category","Unit")]
    public void InlineArrays_NormalizeCompareAndRoundTripEveryElement(){
        InlineDoubles input=default;input[0]=1;input[1]=-0.0;input[2]=7;
        var normalized=RecordValueStrategy<InlineDoubles>.Normalize(input);
        Assert.Equal(0,BitConverter.DoubleToInt64Bits(normalized[1]));
        var result=RecordGraphSerializer<InlineDoubles>.Deserialize(RecordGraphSerializer<InlineDoubles>.Serialize(input));
        Assert.Equal(7,result[2]);
        Assert.Equal(0,BitConverter.DoubleToInt64Bits(result[1]));
        Assert.True(RecordValueStrategy<InlineDoubles>.Equals(input,result));
        result[2]=8;
        Assert.False(RecordValueStrategy<InlineDoubles>.Equals(input,result));
        Assert.NotEqual(RecordValueStrategy<InlineDoubles>.GetHashCode(input),RecordValueStrategy<InlineDoubles>.GetHashCode(result));
        var text=new global::Seamless.Collections.FixedText<global::Seamless.Collections.TextBuffer16>("abcdef");
        Assert.Equal(text,RecordGraphSerializer<global::Seamless.Collections.FixedText<global::Seamless.Collections.TextBuffer16>>.Deserialize(
            RecordGraphSerializer<global::Seamless.Collections.FixedText<global::Seamless.Collections.TextBuffer16>>.Serialize(text)));
    }

    [Fact,Trait("Category","Unit")]
    public void InvalidDataAndDifferentContracts_AreRejected(){
        var bytes=RecordGraphSerializer<int>.Serialize(3);
        Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<long>.Deserialize(bytes));
        Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<int>.Deserialize(bytes[..^1]));
        Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<int>.Deserialize([..bytes,0]));
        bytes[0]^=1;
        Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<int>.Deserialize(bytes));
    }

    [Fact,Trait("Category","Unit")]
    public void BudgetCancellationAndUnsupportedTypes_ReleaseReservations(){
        var budget=new ResourceBudget(2048,0);
        Assert.Throws<ResourceLimitException>(()=>RecordGraphSerializer<byte[]>.Serialize(new byte[10000],budget));
        Assert.Equal(0,budget.MemoryBytes);
        var bytes=RecordGraphSerializer<byte[]>.Serialize(new byte[10000]);
        Assert.Throws<ResourceLimitException>(()=>RecordGraphSerializer<byte[]>.Deserialize(bytes,budget));
        Assert.Equal(0,budget.MemoryBytes);
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(()=>RecordGraphSerializer<int>.Serialize(1,budget,cancellation.Token));
        Assert.Throws<OperationCanceledException>(()=>RecordGraphSerializer<byte[]>.Deserialize(bytes,budget,cancellation.Token));
        Assert.Equal(0,budget.MemoryBytes);
        Assert.Throws<NotSupportedException>(()=>RecordGraphSerializer<object>.Serialize((Action)(()=>{})));
    }

    [Fact,Trait("Category","Unit")]
    public void ExecutionScope_UsesTheGraphProfileAndReleasesOwnedBuffers(){
        using var scope=new ExecutionScope(new(){MemoryLimitBytes=1<<20});
        scope.Codecs.UseRecordGraphSerialization(scope.Budget,scope.CancellationToken);
        var values=new[]{-0.0};var node=new Node(values);node.Link(node);
        var input=new Graph(node,node,values);
        Assert.StartsWith("seamless-record-graph-v1|",scope.Codecs.Get<Graph>().FormatId);
        using(var buffer=scope.Encode(input)){
            Assert.True(scope.Budget.MemoryBytes>=buffer.Memory.Length);
            var result=scope.Codecs.Get<Graph>().Decode(buffer.Memory.Span);
            Assert.True(RecordValueStrategy<Graph>.Equals(input,result));
            Assert.Same(result.First,result.Second);
        }
        Assert.Equal(0,scope.Budget.MemoryBytes);
        Assert.Throws<InvalidOperationException>(()=>scope.Codecs.UseRecordGraphSerialization(scope.Budget));
        using var child=scope.CreateChild(memoryLimitBytes:2048);
        Assert.Throws<ResourceLimitException>(()=>child.Encode(new byte[10000]));
        Assert.Equal(0,scope.Budget.MemoryBytes);
        Assert.Equal(0,child.Budget.MemoryBytes);
    }

    [Fact,Trait("Category","Unit")]
    public void DynamicModuleVersionsAndBackReferences_AreValidated(){
        var bytes=RecordGraphSerializer<object>.Serialize(new Node([]));
        bytes[41]^=128;
        Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<object>.Deserialize(bytes));
        byte[] invalidReference=[..RecordGraphSerializer<object>.Serialize(null!),1];
        invalidReference[36]=1;
        Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<object>.Deserialize(invalidReference));
    }

    [Fact,Trait("Category","Unit")]
    public void CodecPolicies_ReleaseCollectibleAssemblyTypes(){
        var weak=RoundTripCollectibleAssembly();
        for(var index=0;index<20&&weak.IsAlive;index++){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}
        Assert.False(weak.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RoundTripCollectibleAssembly(){
        var context=new AssemblyLoadContext("record-graph-test-"+Guid.NewGuid().ToString("N"),true);
        var assembly=context.LoadFromAssemblyPath(typeof(RecordGraphSerializerTests).Assembly.Location);
        var type=assembly.GetType(typeof(Node).FullName!,true)!;
        var instance=Activator.CreateInstance(type,[new double[]{-0.0}])!;
        typeof(RecordGraphSerializerTests).GetMethod(nameof(VerifyCollectibleRoundTrip),BindingFlags.Static|BindingFlags.NonPublic)!
            .MakeGenericMethod(type).Invoke(null,[instance]);
        var weak=new WeakReference(type);context.Unload();return weak;
    }

    private static void VerifyCollectibleRoundTrip<T>(T value){
        var bytes=RecordGraphSerializer<T>.Serialize(value);
        var result=RecordGraphSerializer<T>.Deserialize(bytes);
        Assert.Equal(value!.GetType(),result!.GetType());
        Assert.True(RecordValueStrategy<T>.Equals(value,result));
        using var store=global::Seamless.Execution.Store.CreateMemory(new(){MemoryBytes=32<<20,DiskBytes=0});
        var data=store.CreateTsavorite<int,T>("collectible",new TsavoriteTuning{IndexSize=1024,PageSize=4096,SegmentSize=1<<16,LogMemorySize=1<<16});
        using var rows=data.Create<global::Seamless.Collections.Serializers.Blittable<int>,RecordGraphSerializer<T>>();
        rows[1]=value;Assert.True(RecordValueStrategy<T>.Equals(value,rows[1]));
    }

    private sealed class Node{
        private readonly double[] values;
        private Node? next;
        public Node(double[] values)=>this.values=values;
        public double[] Values=>this.values;
        public Node Next=>this.next!;
        public void Link(Node value)=>this.next=value;
    }
    private readonly record struct Graph(Node First,Node Second,double[] Values);
    private readonly record struct Pair<T>(T First,T Second);
    private enum Sample:short{Two=2}
    [InlineArray(3)]private struct InlineDoubles{private double element;}
    private readonly record struct Scalars(float Single,Half Half,decimal Decimal,DateTime Date,DateTimeOffset Offset,int? Nullable,
        Guid Guid,DateOnly Day,TimeOnly Time,Int128 Signed,UInt128 Unsigned,Sample Enum);
}
