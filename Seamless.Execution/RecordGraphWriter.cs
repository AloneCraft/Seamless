using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Seamless.Execution;

internal ref struct RecordGraphWriter(Span<byte> destination,RecordGraphState state,bool measuring){
    private readonly Span<byte> destination=destination;
    private readonly RecordGraphState state=state;
    private readonly bool measuring=measuring;
    private int position;
    internal int Length=>this.position;

    internal void Header<T>(){this.Bytes("SRG1"u8);this.Bytes(RecordGraphPolicy.Get(typeof(T)).Schema);}
    internal void Byte(byte value){var bytes=this.Take(1);if(!this.measuring){bytes[0]=value;}}
    internal void Int32(int value){var bytes=this.Take(4);if(!this.measuring){BinaryPrimitives.WriteInt32LittleEndian(bytes,value);}}
    internal void UInt64(ulong value){var bytes=this.Take(8);if(!this.measuring){BinaryPrimitives.WriteUInt64LittleEndian(bytes,value);}}
    internal void UInt16(ushort value){var bytes=this.Take(2);if(!this.measuring){BinaryPrimitives.WriteUInt16LittleEndian(bytes,value);}}
    internal void Bytes(scoped ReadOnlySpan<byte> value){var bytes=this.Take(value.Length);if(!this.measuring){value.CopyTo(bytes);}}

    private Span<byte> Take(int length){
        var start=this.position;this.position=checked(start+length);
        if(this.measuring){return default;}
        if(this.position>this.destination.Length){throw new InvalidDataException("The record graph exceeds its measured length.");}
        return this.destination.Slice(start,length);
    }

    internal void Unsigned(int value){
        var remaining=checked((uint)value);
        while(remaining>=128){this.Byte((byte)(remaining|128));remaining>>=7;}
        this.Byte((byte)remaining);
    }

    internal void Text(string? value){
        if(value is null){this.Int32(-1);return;}
        this.state.CountOutput(checked(32L+value.Length*2L));
        this.Int32(value.Length);
        if(this.measuring){this.Take(checked(value.Length*2));return;}
        foreach(var character in value){this.UInt16(character);}
    }

    public void Write<T>(T value){
        this.state.Check();
        if(RecordGraphNullable<T>.Write is{} nullable){nullable(ref this,value);return;}
        if(RecordScalarValue<T>.IsScalar){RecordGraphScalar<T>.Write(ref this,value);return;}
        RuntimeHelpers.EnsureSufficientExecutionStack();
        if(typeof(T).IsValueType){
            this.state.Reserve(32L+Unsafe.SizeOf<T>());
            RecordGraphPolicy.Get(typeof(T)).WriteFields(value!,ref this);return;
        }
        if(value is null){this.Byte(0);return;}
        object instance=value;
        if(instance is string text){this.Byte(3);this.Text(text);return;}
        if(this.state.WrittenReferences.TryGetValue(instance,out var previous)){this.Byte(1);this.Unsigned(previous);return;}
        var type=instance.GetType();var policy=RecordGraphPolicy.Get(type);
        this.state.CountOutput(policy.Values.EstimatedBytes(instance));
        this.state.Reserve(256);
        this.state.WrittenReferences.Add(instance,this.state.WrittenReferences.Count+1);
        this.Byte(2);
        if(type==typeof(T)){this.Byte(0);}else{this.Byte(1);this.Type(type);}
        if(instance is Array array){
            for(var dimension=0;dimension<array.Rank;dimension++){
                this.Int32(array.GetLength(dimension));this.Int32(array.GetLowerBound(dimension));
            }
        }
        policy.WriteFields(instance,ref this);
    }

    private void Type(Type type){
        RuntimeHelpers.EnsureSufficientExecutionStack();
        if(this.state.WrittenTypes.TryGetValue(type,out var previous)){this.Unsigned(previous);return;}
        this.state.Reserve(256);
        var id=this.state.WrittenTypes.Count+1;this.state.WrittenTypes.Add(type,id);
        this.Unsigned(0);this.Unsigned(id);
        if(type.IsArray){
            if(type.IsSZArray){this.Byte(2);}else{this.Byte(3);this.Unsigned(type.GetArrayRank());}
            this.Type(type.GetElementType()!);
        }else{
            this.Byte(1);Span<byte> module=stackalloc byte[16];type.Module.ModuleVersionId.TryWriteBytes(module);this.Bytes(module);
            this.Int32(type.MetadataToken);
            var arguments=type.GenericTypeArguments;this.Unsigned(arguments.Length);
            foreach(var argument in arguments){this.Type(argument);}
        }
        this.Bytes(RecordGraphPolicy.Get(type).Schema);
    }
}
