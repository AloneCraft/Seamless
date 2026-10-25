using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
namespace Seamless.Execution;
public sealed class CodecRegistry{
    private readonly Dictionary<Type,RegisteredCodec> registered=[];
    private readonly Dictionary<Type,object> codecs=[];
    private readonly Dictionary<Type,MemberInfo[]> members=[];
    private bool frozen;
    public void Register<T,TStrategy>(TStrategy strategy) where TStrategy:struct,Codecs.IStrategy<TStrategy,T>{this.Register<T>(new Codecs.Adapter<T,TStrategy>(strategy));}
    public void Register<T>(global::Seamless.Execution.Codecs.IAdapter<T> codec){
        ArgumentNullException.ThrowIfNull(codec);
        if(this.frozen){throw new InvalidOperationException("Codecs cannot change after a scope has resolved its first codec.");}
        ArgumentException.ThrowIfNullOrWhiteSpace(codec.FormatId);
        this.registered[typeof(T)]=new RegisteredCodec<T>(codec);
    }
    public global::Seamless.Execution.Codecs.IAdapter<T> Get<T>(){
        if(this.codecs.TryGetValue(typeof(T),out var cached)){return(global::Seamless.Execution.Codecs.IAdapter<T>)cached;}
        var schema=this.Schema(typeof(T),new HashSet<Type>());
        this.frozen=true;
        var codec=new CanonicalCodec<T>(this,SHA256.HashData(Encoding.UTF8.GetBytes("seamless-binary-v1|"+schema)));
        this.codecs.Add(typeof(T),codec);
        return codec;
    }
    public bool CanEncode<T>(){
        try{this.Get<T>();return true;}
        catch(CodecUnavailableException){return false;}
    }
    private string Schema(Type type,HashSet<Type> visiting){
        if(this.registered.TryGetValue(type,out var custom)){return type.FullName+"|custom:"+custom.FormatId;}
        if(type.IsPointer||type.IsByRefLike||!visiting.Add(type)){throw new CodecUnavailableException(type);}
        try{
            var nullable=Nullable.GetUnderlyingType(type);
            if(nullable is not null){return "nullable:"+this.Schema(nullable,visiting);}
            if(type.IsEnum){return type.FullName+"|enum:"+this.Schema(Enum.GetUnderlyingType(type),visiting);}
            if(IsScalar(type)){return type.FullName!;}
            if(type.IsArray&&type.GetArrayRank()==1){return "array:"+this.Schema(type.GetElementType()!,visiting);}
            var anonymous=IsAnonymous(type);
            if(!type.IsValueType&&!anonymous){throw new CodecUnavailableException(type);}
            var fields=this.GetMembers(type);
            string prefix="anonymous";
            if(!anonymous){
                var identity=type;
                if(type.IsGenericType){identity=type.GetGenericTypeDefinition();}
                prefix=identity.Assembly.GetName().Name+":"+identity.FullName;
            }
            return prefix+"{"+string.Join(";",fields.Select(m=>m.Name+":"+this.Schema(MemberType(m),visiting)))+"}";
        }finally{visiting.Remove(type);}
    }
    private static bool IsScalar(Type type)=>type==typeof(string)||type==typeof(Guid)||type==typeof(decimal)||Type.GetTypeCode(type) is TypeCode.Boolean or TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Char or TypeCode.Single or TypeCode.Double or TypeCode.DateTime;
    private static bool IsAnonymous(Type type)=>type.IsDefined(typeof(CompilerGeneratedAttribute),false)&&type.Name.Contains("AnonymousType",StringComparison.Ordinal);
    private MemberInfo[] GetMembers(Type type){
        if(this.members.TryGetValue(type,out var result)){return result;}
        if(IsAnonymous(type)){result=type.GetProperties(BindingFlags.Public|BindingFlags.Instance).OrderBy(static p=>p.MetadataToken).Cast<MemberInfo>().ToArray();}
        else{result=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).OrderBy(static p=>p.Name,StringComparer.Ordinal).Cast<MemberInfo>().ToArray();}
        this.members.Add(type,result);
        return result;
    }
    private static Type MemberType(MemberInfo member){
        if(member is PropertyInfo property){return property.PropertyType;}
        return((FieldInfo)member).FieldType;
    }
    private static object? MemberValue(MemberInfo member,object instance){
        if(member is PropertyInfo property){return property.GetValue(instance);}
        return((FieldInfo)member).GetValue(instance);
    }
    internal void Write(Type type,object? value,ref Encoder writer){
        if(this.registered.TryGetValue(type,out var custom)){custom.Write(value,ref writer);return;}
        if(value is null){writer.Byte(0);return;}
        writer.Byte(1);
        var nullable=Nullable.GetUnderlyingType(type);
        if(nullable is not null){this.Write(nullable,value,ref writer);return;}
        if(type.IsEnum){this.Write(Enum.GetUnderlyingType(type),Convert.ChangeType(value,Enum.GetUnderlyingType(type),System.Globalization.CultureInfo.InvariantCulture),ref writer);return;}
        if(type==typeof(Guid)){
            Span<byte> bytes=stackalloc byte[16];((Guid)value).TryWriteBytes(bytes);writer.Bytes(bytes);return;
        }
        switch(Type.GetTypeCode(type)){
            case TypeCode.Boolean:if((bool)value){writer.Byte(1);}else{writer.Byte(0);}return;
            case TypeCode.Byte:writer.Byte((byte)value);return;
            case TypeCode.SByte:writer.Byte(unchecked((byte)(sbyte)value));return;
            case TypeCode.Int16:writer.Int16((short)value);return;
            case TypeCode.UInt16:writer.Int16(unchecked((short)(ushort)value));return;
            case TypeCode.Char:writer.Int16(unchecked((short)(char)value));return;
            case TypeCode.Int32:writer.Int32((int)value);return;
            case TypeCode.UInt32:writer.Int32(unchecked((int)(uint)value));return;
            case TypeCode.Int64:writer.Int64((long)value);return;
            case TypeCode.UInt64:writer.Int64(unchecked((long)(ulong)value));return;
            case TypeCode.Single:writer.Int32(BitConverter.SingleToInt32Bits((float)value));return;
            case TypeCode.Double:writer.Int64(BitConverter.DoubleToInt64Bits((double)value));return;
            case TypeCode.Decimal:
                Span<int> parts=stackalloc int[4];decimal.GetBits((decimal)value,parts);
                foreach(var part in parts){writer.Int32(part);}return;
            case TypeCode.DateTime:var date=(DateTime)value;writer.Int64(date.Ticks);writer.Byte((byte)date.Kind);return;
            case TypeCode.String:writer.String((string)value);return;
        }
        if(type.IsArray){
            var array=(Array)value;
            writer.Int32(array.Length);
            foreach(var element in array){this.Write(type.GetElementType()!,element,ref writer);}
            return;
        }
        foreach(var member in this.GetMembers(type)){this.Write(MemberType(member),MemberValue(member,value),ref writer);}
    }
    internal object? Read(Type type,ref Decoder reader){
        if(this.registered.TryGetValue(type,out var custom)){return custom.Read(ref reader);}
        var present=reader.Byte();
        if(present==0){
            if(type.IsValueType&&Nullable.GetUnderlyingType(type) is null){throw new InvalidDataException("Null encoded for a value type.");}
            return null;
        }
        if(present!=1){throw new InvalidDataException("Invalid value marker.");}
        var nullable=Nullable.GetUnderlyingType(type);
        if(nullable is not null){return this.Read(nullable,ref reader);}
        if(type.IsEnum){return Enum.ToObject(type,this.Read(Enum.GetUnderlyingType(type),ref reader)!);}
        if(type==typeof(Guid)){return new Guid(reader.Bytes(16));}
        switch(Type.GetTypeCode(type)){
            case TypeCode.Boolean:
                var boolean=reader.Byte();if(boolean>1){throw new InvalidDataException("Invalid Boolean.");}return boolean==1;
            case TypeCode.Byte:return reader.Byte();
            case TypeCode.SByte:return unchecked((sbyte)reader.Byte());
            case TypeCode.Int16:return reader.Int16();
            case TypeCode.UInt16:return unchecked((ushort)reader.Int16());
            case TypeCode.Char:return unchecked((char)reader.Int16());
            case TypeCode.Int32:return reader.Int32();
            case TypeCode.UInt32:return unchecked((uint)reader.Int32());
            case TypeCode.Int64:return reader.Int64();
            case TypeCode.UInt64:return unchecked((ulong)reader.Int64());
            case TypeCode.Single:return BitConverter.Int32BitsToSingle(reader.Int32());
            case TypeCode.Double:return BitConverter.Int64BitsToDouble(reader.Int64());
            case TypeCode.Decimal:
                Span<int> parts=stackalloc int[4];for(var i=0;i<4;i++){parts[i]=reader.Int32();}return new decimal(parts);
            case TypeCode.DateTime:return new DateTime(reader.Int64(),(DateTimeKind)reader.Byte());
            case TypeCode.String:return reader.String();
        }
        if(type.IsArray){
            var count=reader.Int32();
            if(count<0||count>reader.Remaining){throw new InvalidDataException("Invalid array length.");}
            var elementType=type.GetElementType()!;
            var array=Array.CreateInstance(elementType,count);
            for(var i=0;i<count;i++){array.SetValue(this.Read(elementType,ref reader),i);}
            return array;
        }
        var members=this.GetMembers(type);
        if(IsAnonymous(type)){
            var arguments=new object?[members.Length];
            for(var i=0;i<members.Length;i++){arguments[i]=this.Read(MemberType(members[i]),ref reader);}
            return Activator.CreateInstance(type,arguments);
        }
        var value=Activator.CreateInstance(type)!;
        foreach(var member in members){((FieldInfo)member).SetValue(value,this.Read(MemberType(member),ref reader));}
        return value;
    }
    private abstract class RegisteredCodec{
        public abstract string FormatId{get;}
        public abstract void Write(object? value,ref Encoder writer);
        public abstract object? Read(ref Decoder reader);
    }
    private sealed class RegisteredCodec<T>(global::Seamless.Execution.Codecs.IAdapter<T> codec):RegisteredCodec{
        private readonly global::Seamless.Execution.Codecs.IAdapter<T> codec=codec;
        public override string FormatId=>this.codec.FormatId;
        public override void Write(object? value,ref Encoder writer){
            var typed=(T)value!;
            var length=this.codec.GetEncodedLength(typed);
            ArgumentOutOfRangeException.ThrowIfNegative(length);
            writer.Int32(length);
            var target=writer.Reserve(length);
            if(!writer.Counting){this.codec.Encode(typed,target);}
        }
        public override object? Read(ref Decoder reader)=>this.codec.Decode(reader.Bytes(reader.Int32()));
    }
    private sealed class CanonicalCodec<T>(CodecRegistry registry,byte[] header):global::Seamless.Execution.Codecs.IAdapter<T>{
        private readonly CodecRegistry registry=registry;
        private readonly byte[] header=header;
        public string FormatId=>"seamless-binary-v1:"+Convert.ToHexString(this.header);
        public int GetEncodedLength(T value){
            var writer=new Encoder(default,true);
            writer.Bytes(this.header);
            this.registry.Write(typeof(T),value,ref writer);
            return writer.Position;
        }
        public void Encode(T value,Span<byte> destination){
            var writer=new Encoder(destination,false);
            writer.Bytes(this.header);
            this.registry.Write(typeof(T),value,ref writer);
            if(writer.Position!=destination.Length){throw new InvalidDataException("Destination length does not match the codec length.");}
        }
        public T Decode(ReadOnlySpan<byte> source){
            var reader=new Decoder(source);
            if(!reader.Bytes(this.header.Length).SequenceEqual(this.header)){throw new InvalidDataException("Codec type or format version does not match.");}
            var value=(T)this.registry.Read(typeof(T),ref reader)!;
            if(reader.Remaining!=0){throw new InvalidDataException("Trailing codec data.");}
            return value;
        }
    }
    internal ref struct Encoder(Span<byte> destination,bool counting){
        private Span<byte> destination=destination;
        public readonly bool Counting=counting;
        public int Position{get;private set;}
        public Span<byte> Reserve(int length){
            var offset=this.Position;
            this.Position=checked(this.Position+length);
            if(this.Counting){return default;}
            return this.destination.Slice(offset,length);
        }
        public void Byte(byte value){var target=this.Reserve(1);if(!this.Counting){target[0]=value;}}
        public void Int16(short value){var target=this.Reserve(2);if(!this.Counting){BinaryPrimitives.WriteInt16LittleEndian(target,value);}}
        public void Int32(int value){var target=this.Reserve(4);if(!this.Counting){BinaryPrimitives.WriteInt32LittleEndian(target,value);}}
        public void Int64(long value){var target=this.Reserve(8);if(!this.Counting){BinaryPrimitives.WriteInt64LittleEndian(target,value);}}
        public void Bytes(scoped ReadOnlySpan<byte> value){var target=this.Reserve(value.Length);if(!this.Counting){value.CopyTo(target);}}
        public void String(string value){
            this.Int32(value.Length);
            foreach(var character in value){this.Int16(unchecked((short)character));}
        }
    }
    internal ref struct Decoder(ReadOnlySpan<byte> source){
        private ReadOnlySpan<byte> source=source;
        public readonly int Remaining=>this.source.Length;
        public ReadOnlySpan<byte> Bytes(int count){
            if(count<0||count>this.source.Length){throw new InvalidDataException("Truncated codec data.");}
            var value=this.source[..count];this.source=this.source[count..];return value;
        }
        public byte Byte()=>this.Bytes(1)[0];
        public short Int16()=>BinaryPrimitives.ReadInt16LittleEndian(this.Bytes(2));
        public int Int32()=>BinaryPrimitives.ReadInt32LittleEndian(this.Bytes(4));
        public long Int64()=>BinaryPrimitives.ReadInt64LittleEndian(this.Bytes(8));
        public string String(){
            var count=this.Int32();
            var bytes=this.Bytes(checked(count*2));
            if(BitConverter.IsLittleEndian){return new string(System.Runtime.InteropServices.MemoryMarshal.Cast<byte,char>(bytes));}
            var characters=new char[count];
            for(var i=0;i<count;i++){characters[i]=(char)BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(i*2,2));}
            return new string(characters);
        }
    }
}
