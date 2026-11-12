using Seamless.Execution;
using Seamless.Execution.Codecs;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Test.Seamless.Database.Records;

public sealed class RecordGraphValidationTests{
    [Fact]
    public void Validate_AcceptsSharingCyclesPrivateFieldsAndScalarFamilies(){
        object[] array=[-0.0,12.00m,null!,Guid.NewGuid(),new DateTimeOffset(2026,11,7,9,0,0,TimeSpan.FromHours(9)),(Half)2,Int128.MinValue];
        array[2]=array;var input=new Graph(array,array);Check(input);
        Check(new int?[]{null,1,2});Check(new decimal?[]{null,1.25m});Check(new DateTime(2026,11,7));
        var matrix=(object[,])Array.CreateInstance(typeof(object),[2,1],[-2,4]);matrix[-2,4]=matrix;matrix[-1,4]="\ud800\0";Check(matrix);
        var inline=new Inline();inline[0]=input;inline[1]=input;Check(inline);
        static void Check<T>(T value){
            var bytes=RecordGraphSerializer<T>.Serialize(value);var budget=new ResourceBudget(2<<20,0);
            RecordGraphSerializer<T>.Validate(bytes,budget);Assert.Equal(0,budget.MemoryBytes);
            for(var length=0;length<bytes.Length;length++){
                var truncated=bytes[..length];Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<T>.Validate(truncated,budget));Assert.Equal(0,budget.MemoryBytes);
            }
            Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<T>.Validate([..bytes,0],budget));
        }
    }

    [Fact]
    public void Validate_DoesNotAllocateTheRestoredObject(){
        var bytes=RecordGraphSerializer<LargeObject>.Serialize(new());var budget=new ResourceBudget(16<<20,0);
        for(var index=0;index<8;index++){RecordGraphSerializer<LargeObject>.Validate(bytes,budget);}
        var before=GC.GetAllocatedBytesForCurrentThread();RecordGraphSerializer<LargeObject>.Validate(bytes,budget);
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread()-before,0,64<<10);Assert.Equal(0,budget.MemoryBytes);
    }

    [Fact]
    public void Validate_DoesNotInvokeUserCodeAndBoundsTypeDescriptions(){
        var hook=new Hook();var bytes=RecordGraphSerializer<Hook>.Serialize(hook);var calls=Hook.Constructions;
        RecordGraphSerializer<Hook>.Validate(bytes);Assert.Equal(calls,Hook.Constructions);Assert.Equal(0,Hook.GetterCalls);
        object array=Array.Empty<int>();for(var index=0;index<8;index++){array=Array.CreateInstance(array.GetType(),0);}
        var nested=RecordGraphSerializer<object>.Serialize(array);
        RecordGraphSerializer<object>.Validate(nested);
        Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<object>.Validate(nested,limits:new(){MaximumDepth=4}));
    }

    [Fact]
    public void Validate_EnforcesAggregateBoundsAndReleasesReservations(){
        var budget=new ResourceBudget(2<<20,0);var array=RecordGraphSerializer<string[]>.Serialize(["aa","bb"]);
        Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<string[]>.Validate(array,budget,limits:new(){MaximumStringCharacters=3}));
        Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<string[]>.Validate(array,budget,limits:new(){MaximumArrayElements=1}));
        Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<string[]>.Validate(array,budget,limits:new(){MaximumNodes=2}));
        Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<string[]>.Validate(array,budget,limits:new(){MaximumBytes=array.Length-1}));
        Assert.Throws<ArgumentOutOfRangeException>(()=>RecordGraphSerializer<string[]>.Validate(array,budget,limits:new(){MaximumDepth=0}));
        var node=new Link();var head=node;for(var index=0;index<8;index++){node.Next=new();node=node.Next;}
        var nested=RecordGraphSerializer<Link>.Serialize(head);
        Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<Link>.Validate(nested,budget,limits:new(){MaximumDepth=4}));
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(()=>RecordGraphSerializer<string[]>.Validate(array,budget,cancellation.Token));
        var small=new ResourceBudget(1024,0);Assert.Throws<ResourceLimitException>(()=>RecordGraphSerializer<string[]>.Validate(array,small));
        Assert.Equal(0,small.MemoryBytes);Assert.Equal(0,budget.MemoryBytes);
        RecordGraphSerializer<string[]>.Validate(array,budget);
    }

    [Fact]
    public void Validate_RejectsInvalidReferencesTypesLengthsAndScalars(){
        var valid=RecordGraphSerializer<object[]>.Serialize([null!]);
        var reference=(byte[])valid.Clone();reference[^1]=1;Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<object[]>.Validate([..reference,2]));
        var marker=(byte[])valid.Clone();marker[^1]=255;Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<object[]>.Validate(marker));
        var version=(byte[])valid.Clone();version[4]^=1;Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<object[]>.Validate(version));
        var length=(byte[])valid.Clone();BinaryPrimitives.WriteInt32LittleEndian(length.AsSpan(38),int.MaxValue);
        Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<object[]>.Validate(length));
        var boolean=RecordGraphSerializer<bool>.Serialize(true);boolean[^1]=2;Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<bool>.Validate(boolean));
        var number=RecordGraphSerializer<decimal>.Serialize(1m);number[^1]=127;Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<decimal>.Validate(number));
        var date=RecordGraphSerializer<DateTime>.Serialize(DateTime.UtcNow);date.AsSpan(36).Fill(255);Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<DateTime>.Validate(date));
        var typed=RecordGraphSerializer<TypedReferences>.Serialize(new());typed[^1]=1;
        Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<TypedReferences>.Validate([..typed,1]));
        var module=RecordGraphSerializer<object>.Serialize(new Link());module[41]^=255;
        Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<object>.Validate(module));
    }

    [Fact]
    public void Validate_AcceptedMutationsCanBeRestored(){
        object[] values=[new[]{1,2,3},"日本語",12m,true,null!];values[4]=values;
        var original=RecordGraphSerializer<object[]>.Serialize(values);var random=new Random(7129);
        var budget=new ResourceBudget(2<<20,0);var accepted=0;var rejected=0;
        for(var attempt=0;attempt<300;attempt++){
            var bytes=(byte[])original.Clone();bytes[random.Next(bytes.Length)]^=(byte)(1<<random.Next(8));
            try{RecordGraphSerializer<object[]>.Validate(bytes,budget);}catch(Exception error)when(error is InvalidDataException or NotSupportedException or ResourceLimitException){rejected++;continue;}
            _ = RecordGraphSerializer<object[]>.Deserialize(bytes,budget);accepted++;Assert.Equal(0,budget.MemoryBytes);
        }
        Assert.True(accepted>0);Assert.True(rejected>0);Assert.Equal(0,budget.MemoryBytes);
    }

    [Fact]
    public void Validate_RejectsOverflowInTheAggregateElementCount(){
        var empty=Array.CreateInstance(typeof(object),[0,1,1,1,1,1,1]);
        var bytes=RecordGraphSerializer<object[]>.Serialize([empty]);
        int[] factors=[7,7,73,127,337,92737,649657];
        var bounds=bytes.Length-factors.Length*8;
        for(var index=0;index<factors.Length;index++){BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(bounds+index*8),factors[index]);}
        var budget=new ResourceBudget(2<<20,0);
        Assert.Throws<InvalidDataException>(()=>RecordGraphSerializer<object[]>.Validate(bytes,budget));Assert.Equal(0,budget.MemoryBytes);
    }

    private sealed class Graph(object[] first,object[] second){private readonly object[] first=first,second=second;}
    private sealed class Link{public Link? Next;}
    private sealed class TypedReferences{public object[]? First=null;public int[]? Second=null;}
    private sealed class Hook{
        public static int Constructions,GetterCalls;
        private readonly double value=-0.0;
        public Hook(){Constructions++;}
        public double Value{get{GetterCalls++;return this.value;}}
    }
    [InlineArray(2)]private struct Inline{private Graph value;}
    [StructLayout(LayoutKind.Sequential,Size=4<<20)]private sealed class LargeObject{public byte Value=1;}
}
