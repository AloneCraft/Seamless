using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Seamless.Execution;

internal ref partial struct RecordGraphReader(ReadOnlySpan<byte> source,RecordGraphState state,Codecs.RecordGraphValidationLimits? limits=null){
    private readonly ReadOnlySpan<byte> source=source;
    private readonly RecordGraphState state=state;
    private readonly Codecs.RecordGraphValidationLimits? validationLimits=limits;
    private int position;
    private List<Type>? validatedReferences;
    private long validatedCharacters,validatedElements,validationNodes;
    private int validationDepth,typeDepth;
    internal bool AtEnd=>this.position==this.source.Length;

    internal void Header<T>(){
        if(!this.Bytes(4).SequenceEqual("SRG1"u8)||!this.Bytes(32).SequenceEqual(RecordGraphPolicy.Get(typeof(T)).Schema)){
            throw new InvalidDataException("Record graph format or type contract differs.");
        }
    }
    internal ReadOnlySpan<byte> Bytes(int length){
        if(length<0||length>this.source.Length-this.position){throw new InvalidDataException("Truncated record graph.");}
        var bytes=this.source.Slice(this.position,length);this.position+=length;return bytes;
    }
    internal byte Byte()=>this.Bytes(1)[0];
    internal int Int32()=>BinaryPrimitives.ReadInt32LittleEndian(this.Bytes(4));
    internal ulong UInt64()=>BinaryPrimitives.ReadUInt64LittleEndian(this.Bytes(8));
    internal ushort UInt16()=>BinaryPrimitives.ReadUInt16LittleEndian(this.Bytes(2));
    internal bool Boolean(){var value=this.Byte();if(value>1){throw new InvalidDataException("Invalid graph boolean.");}return value!=0;}

    internal int Unsigned(){
        uint value=0;
        for(var shift=0;shift<=28;shift+=7){
            var part=this.Byte();
            if(shift==28&&part>7){throw new InvalidDataException("Invalid graph identifier.");}
            value|=(uint)(part&127)<<shift;
            if(part<128){if(shift!=0&&part==0){throw new InvalidDataException("Noncanonical graph identifier.");}return(int)value;}
        }
        throw new InvalidDataException("Invalid graph identifier.");
    }

    internal string? Text(){
        var length=this.Int32();if(length==-1){return null;}
        if(length<0||length>(this.source.Length-this.position)/2){throw new InvalidDataException("Invalid graph string length.");}
        if(this.validationLimits is{} limits){
            if(length>limits.MaximumStringCharacters-this.validatedCharacters){throw new InvalidDataException("The graph exceeds the validation string limit.");}
            this.validatedCharacters+=length;
        }
        this.state.ReserveOutput(checked(32L+length*2L));
        var bytes=this.Bytes(length*2);
        if(this.validationLimits is not null){return string.Empty;}
        return string.Create(length,bytes,static (characters,data)=>{
            for(var index=0;index<characters.Length;index++){characters[index]=(char)BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(index*2,2));}
        });
    }

    public T Read<T>(){
        this.state.Check();
        if(RecordGraphNullable<T>.Read is{} nullable){return nullable(ref this);}
        if(RecordScalarValue<T>.IsScalar){return RecordGraphScalar<T>.Read(ref this);}
        RuntimeHelpers.EnsureSufficientExecutionStack();
        if(typeof(T).IsValueType){
            var policy=RecordGraphPolicy.Get(typeof(T));this.state.Reserve(policy.Values.ObjectBytes);
            object value=RuntimeHelpers.GetUninitializedObject(typeof(T));policy.ReadFields(value,ref this);return(T)value;
        }
        var marker=this.Byte();if(marker==0){return default!;}
        if(marker==3){
            if(!typeof(T).IsAssignableFrom(typeof(string))){throw new InvalidDataException("Unexpected string graph node.");}
            return(T)(object)(this.Text()??throw new InvalidDataException("Null string graph node."));
        }
        if(marker==1){
            var id=this.Unsigned();
            if(id==0||id>this.state.ReadReferences.Count||this.state.ReadReferences[id-1] is not T previous){throw new InvalidDataException("Invalid graph reference.");}
            return previous;
        }
        if(marker!=2){throw new InvalidDataException("Invalid graph node marker.");}
        var dynamicType=this.Byte();Type type;
        if(dynamicType==0){type=typeof(T);}else if(dynamicType==1){type=this.Type();}else{throw new InvalidDataException("Invalid graph type marker.");}
        if(!typeof(T).IsAssignableFrom(type)||type==typeof(string)||type.IsInterface||type.IsAbstract||Nullable.GetUnderlyingType(type) is not null){throw new InvalidDataException("Incompatible graph node type.");}
        var nodePolicy=RecordGraphPolicy.Get(type);
        this.state.Reserve(256);
        object instance;
        if(type.IsArray){instance=this.Array(type,nodePolicy);}
        else{this.state.ReserveOutput(Math.Max(32,nodePolicy.Values.ObjectBytes));instance=RuntimeHelpers.GetUninitializedObject(type);}
        this.state.ReadReferences.Add(instance);
        nodePolicy.ReadFields(instance,ref this);return(T)instance;
    }

    private Array Array(Type type,RecordGraphPolicy policy){
        var rank=type.GetArrayRank();Span<int> lengths=stackalloc int[rank];Span<int> bounds=stackalloc int[rank];
        this.ArrayBounds(type,policy,lengths,bounds);
        if(type.IsSZArray){return System.Array.CreateInstance(type.GetElementType()!,lengths[0]);}
        return System.Array.CreateInstance(type.GetElementType()!,lengths.ToArray(),bounds.ToArray());
    }

    private long ArrayBounds(Type type,RecordGraphPolicy policy,scoped Span<int> lengths,scoped Span<int> bounds){
        var rank=lengths.Length;
        long count=1;
        for(var dimension=0;dimension<rank;dimension++){
            lengths[dimension]=this.Int32();bounds[dimension]=this.Int32();
            if(lengths[dimension]<0||lengths[dimension]>0&&(long)bounds[dimension]+lengths[dimension]-1>int.MaxValue){throw new InvalidDataException("Invalid graph array bounds.");}
            try{count=checked(count*lengths[dimension]);}catch(OverflowException error){throw new InvalidDataException("Invalid graph array size.",error);}
        }
        if(type.IsSZArray&&bounds[0]!=0){throw new InvalidDataException("Invalid vector bounds.");}
        if(this.validationLimits is{} limits){
            if(count>limits.MaximumArrayElements-this.validatedElements){throw new InvalidDataException("The graph exceeds the validation array limit.");}
            this.validatedElements+=count;
        }
        long bytes;try{bytes=checked(64L+rank*16L+count*policy.ElementSize);}catch(OverflowException error){throw new InvalidDataException("Invalid graph array byte size.",error);}
        this.state.ReserveOutput(bytes);
        return count;
    }

    private Type Type(){
        if(this.validationLimits is null){return this.ReadType();}
        if(++this.typeDepth>this.validationLimits.MaximumDepth){throw new InvalidDataException("The graph type exceeds the validation depth limit.");}
        try{this.ValidationNode();return this.ReadType();}finally{this.typeDepth--;}
    }

    private Type ReadType(){
        this.state.Check();RuntimeHelpers.EnsureSufficientExecutionStack();
        var id=this.Unsigned();
        if(id!=0){return this.state.ReadTypes.TryGetValue(id,out var previous)?previous:throw new InvalidDataException("Invalid graph type reference.");}
        id=this.Unsigned();if(id!=this.state.NextTypeId++){throw new InvalidDataException("Invalid graph type definition.");}
        this.state.Reserve(512);
        Type type;var kind=this.Byte();
        if(kind==2||kind==3){
            var rank=1;if(kind==3){rank=this.Unsigned();if(rank<1||rank>32){throw new InvalidDataException("Invalid graph array rank.");}}
            var element=this.Type();type=kind==2?element.MakeArrayType():element.MakeArrayType(rank);
        }else if(kind==1){
            var module=this.state.ResolveModule(new Guid(this.Bytes(16)));var token=this.Int32();
            if((token&unchecked((int)0xff000000))!=0x02000000){throw new InvalidDataException("Invalid graph type token.");}
            try{type=module.ResolveType(token);}catch(Exception error)when(error is ArgumentException or TypeLoadException or BadImageFormatException){throw new InvalidDataException("Invalid graph type token.",error);}
            var count=this.Unsigned();var expected=type.IsGenericTypeDefinition?type.GetGenericArguments().Length:0;
            if(count!=expected){throw new InvalidDataException("Invalid graph generic arity.");}
            if(count!=0){
                this.state.Reserve(checked(count*16L));var arguments=new Type[count];for(var index=0;index<count;index++){arguments[index]=this.Type();}
                try{type=type.MakeGenericType(arguments);}catch(ArgumentException error){throw new InvalidDataException("Invalid graph generic arguments.",error);}
            }
        }else{throw new InvalidDataException("Invalid graph type descriptor.");}
        if(type.ContainsGenericParameters||type.IsPointer||type.IsByRef||type.IsByRefLike||type==typeof(void)){throw new InvalidDataException("Invalid graph data type.");}
        if(!this.Bytes(32).SequenceEqual(RecordGraphPolicy.Get(type).Schema)){throw new InvalidDataException("Record graph type version differs.");}
        this.state.ReadTypes.Add(id,type);return type;
    }
}
