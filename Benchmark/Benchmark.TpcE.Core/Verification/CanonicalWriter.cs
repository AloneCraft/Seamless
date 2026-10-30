using System.Buffers.Binary;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
namespace Benchmark.TpcE.Core;
/// <summary>並びの順序に意味が無いことを示す(比較の前に要素のバイト列で並べ替える)。</summary>
[AttributeUsage(AttributeTargets.Property|AttributeTargets.Field)]
public sealed class UnorderedAttribute:Attribute;
/// <summary>
/// 値を「意味が同じなら同じバイト列」に正規化して書く。取引の出力・表の行・索引の要素の比較とダイジェストに使う。
/// 型ごとの書き方はリフレクションで 1 回だけ組み立てて使い回す(項目の書き漏らしで一致して見えるのを防ぐため手書きにしない)。
/// 規則: 整数・bool・enum は固定幅、文字列は UTF-8 と長さ、null は印、decimal は値で正規化(1.0 と 1.00 は同じ、-0 は 0)、
/// double/float は例外(足す順で値が変わり、保存先で列挙順が違うので、正しくても食い違って見えるため)。
/// 構造体とクラスは公開のプロパティとフィールドを宣言順に書く。
/// </summary>
public static class CanonicalWriter{
    private static readonly ConcurrentDictionary<Type,Action<CanonicalBuffer,object?>> writers=new();
    public static byte[] ToBytes(object? value){
        var buffer=new CanonicalBuffer();
        Write(buffer,value);
        return buffer.ToArray();
    }
    public static UInt128 Hash(object? value){
        var buffer=new CanonicalBuffer();
        Write(buffer,value);
        return buffer.Hash();
    }
    public static void Write(CanonicalBuffer buffer,object? value){
        if(value is null){
            buffer.WriteByte(0);
            return;
        }
        buffer.WriteByte(1);
        WriterFor(value.GetType())(buffer,value);
    }
    internal static Action<CanonicalBuffer,object?> WriterFor(Type type)=>writers.GetOrAdd(type,Build);
    private static Action<CanonicalBuffer,object?> Build(Type type){
        if(type==typeof(double)||type==typeof(float)||type==typeof(Half)){
            return (_,_)=>throw new NotSupportedException($"{type.Name} cannot be compared canonically. Use decimal.");
        }
        if(type==typeof(string)){
            return (buffer,value)=>buffer.WriteString((string?)value);
        }
        if(type==typeof(byte[])){
            return (buffer,value)=>buffer.WriteBytes((byte[]?)value);
        }
        if(type==typeof(decimal)){
            return (buffer,value)=>buffer.WriteDecimal((decimal)value!);
        }
        if(type==typeof(bool)){
            return (buffer,value)=>{
                if((bool)value!){
                    buffer.WriteByte(1);
                }else{
                    buffer.WriteByte(0);
                }
            };
        }
        if(type.IsEnum){
            return (buffer,value)=>buffer.WriteInt64(Convert.ToInt64(value,System.Globalization.CultureInfo.InvariantCulture));
        }
        if(type.IsPrimitive){
            return BuildPrimitive(type);
        }
        if(Nullable.GetUnderlyingType(type) is Type underlying){
            var inner=WriterFor(underlying);
            return (buffer,value)=>{
                if(value is null){
                    buffer.WriteByte(0);
                    return;
                }
                buffer.WriteByte(1);
                inner(buffer,value);
            };
        }
        if(type.IsArray){
            return BuildSequence(type.GetElementType()!,false);
        }
        if(type.IsGenericType&&type.GetGenericTypeDefinition()==typeof(List<>)){
            return BuildSequence(type.GetGenericArguments()[0],false);
        }
        return BuildRecord(type);
    }
    private static Action<CanonicalBuffer,object?> BuildPrimitive(Type type){
        if(type==typeof(long)){
            return (buffer,value)=>buffer.WriteInt64((long)value!);
        }
        if(type==typeof(int)){
            return (buffer,value)=>buffer.WriteInt64((int)value!);
        }
        if(type==typeof(short)){
            return (buffer,value)=>buffer.WriteInt64((short)value!);
        }
        if(type==typeof(sbyte)){
            return (buffer,value)=>buffer.WriteInt64((sbyte)value!);
        }
        if(type==typeof(byte)){
            return (buffer,value)=>buffer.WriteInt64((byte)value!);
        }
        if(type==typeof(ushort)){
            return (buffer,value)=>buffer.WriteInt64((ushort)value!);
        }
        if(type==typeof(uint)){
            return (buffer,value)=>buffer.WriteInt64((uint)value!);
        }
        if(type==typeof(ulong)){
            return (buffer,value)=>buffer.WriteUInt64((ulong)value!);
        }
        if(type==typeof(char)){
            return (buffer,value)=>buffer.WriteInt64((char)value!);
        }
        throw new NotSupportedException($"{type} is not supported.");
    }
    private static Action<CanonicalBuffer,object?> BuildSequence(Type elementType,bool unordered){
        return (buffer,value)=>{
            if(value is null){
                buffer.WriteInt64(-1);
                return;
            }
            var items=(IList)value;
            buffer.WriteInt64(items.Count);
            if(!unordered){
                foreach(var item in items){
                    Write(buffer,item);
                }
                return;
            }
            var encoded=new List<byte[]>(items.Count);
            foreach(var item in items){
                encoded.Add(ToBytes(item));
            }
            encoded.Sort(static (left,right)=>left.AsSpan().SequenceCompareTo(right));
            foreach(var bytes in encoded){
                buffer.WriteRaw(bytes);
            }
        };
    }
    private static Action<CanonicalBuffer,object?> BuildRecord(Type type){
        var members=new List<(MemberInfo Member,Type Type,bool Unordered)>();
        foreach(var member in type.GetMembers(BindingFlags.Public|BindingFlags.Instance)){
            if(member is PropertyInfo property){
                if(!property.CanRead||property.GetIndexParameters().Length>0||property.GetMethod is null||!property.GetMethod.IsPublic){
                    continue;
                }
                members.Add((property,property.PropertyType,property.IsDefined(typeof(UnorderedAttribute))));
            }else if(member is FieldInfo field){
                members.Add((field,field.FieldType,field.IsDefined(typeof(UnorderedAttribute))));
            }
        }
        members.Sort(static (left,right)=>left.Member.MetadataToken.CompareTo(right.Member.MetadataToken));
        var steps=new List<Action<CanonicalBuffer,object>>(members.Count);
        foreach(var (member,memberType,unordered) in members){
            var getter=CompileGetter(type,member);
            Action<CanonicalBuffer,object?> write;
            if(unordered&&memberType.IsArray){
                write=BuildSequence(memberType.GetElementType()!,true);
            }else if(memberType.IsValueType&&Nullable.GetUnderlyingType(memberType) is null){
                write=WriterFor(memberType);
            }else{
                // 参照型と Nullable は null の印を書く。
                write=(buffer,value)=>Write(buffer,value);
            }
            steps.Add((buffer,target)=>write(buffer,getter(target)));
        }
        return (buffer,value)=>{
            foreach(var step in steps){
                step(buffer,value!);
            }
        };
    }
    private static Func<object,object?> CompileGetter(Type type,MemberInfo member){
        var parameter=Expression.Parameter(typeof(object),"target");
        var typed=Expression.Convert(parameter,type);
        Expression access=member switch{
            PropertyInfo property=>Expression.Property(typed,property),
            FieldInfo field=>Expression.Field(typed,field),
            _=>throw new NotSupportedException(),
        };
        return Expression.Lambda<Func<object,object?>>(Expression.Convert(access,typeof(object)),parameter).Compile();
    }
}
/// <summary>正規化したバイト列の書き込み先。</summary>
public sealed class CanonicalBuffer{
    private byte[] data=new byte[256];
    private int length;
    public int Length=>this.length;
    public void Clear()=>this.length=0;
    public ReadOnlySpan<byte> Span=>this.data.AsSpan(0,this.length);
    public byte[] ToArray()=>this.Span.ToArray();
    public UInt128 Hash()=>System.IO.Hashing.XxHash128.HashToUInt128(this.Span);
    public void WriteByte(byte value){
        this.Ensure(1);
        this.data[this.length++]=value;
    }
    public void WriteInt64(long value){
        this.Ensure(8);
        BinaryPrimitives.WriteInt64BigEndian(this.data.AsSpan(this.length),value);
        this.length+=8;
    }
    public void WriteUInt64(ulong value){
        this.Ensure(8);
        BinaryPrimitives.WriteUInt64BigEndian(this.data.AsSpan(this.length),value);
        this.length+=8;
    }
    public void WriteDecimal(decimal value){
        // 末尾の 0 を落として scale を揃える(1.0 と 1.00 を同じにする)。-0 は 0 にする。
        var normalized=value/1.0000000000000000000000000000m;
        if(normalized==0m){
            normalized=0m;
        }
        Span<int> bits=stackalloc int[4];
        decimal.GetBits(normalized,bits);
        foreach(var part in bits){
            this.WriteInt64(part);
        }
    }
    public void WriteString(string? value){
        if(value is null){
            this.WriteInt64(-1);
            return;
        }
        var count=Encoding.UTF8.GetByteCount(value);
        this.WriteInt64(count);
        this.Ensure(count);
        Encoding.UTF8.GetBytes(value,this.data.AsSpan(this.length));
        this.length+=count;
    }
    public void WriteBytes(byte[]? value){
        if(value is null){
            this.WriteInt64(-1);
            return;
        }
        this.WriteInt64(value.Length);
        this.WriteRaw(value);
    }
    public void WriteRaw(ReadOnlySpan<byte> value){
        this.Ensure(value.Length);
        value.CopyTo(this.data.AsSpan(this.length));
        this.length+=value.Length;
    }
    private void Ensure(int extra){
        if(this.length+extra<=this.data.Length){
            return;
        }
        var size=this.data.Length*2;
        while(size<this.length+extra){
            size*=2;
        }
        Array.Resize(ref this.data,size);
    }
}
