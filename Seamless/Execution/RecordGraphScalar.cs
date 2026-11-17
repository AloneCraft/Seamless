using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Seamless.Execution;

internal static class RecordGraphNullable<T>{
    internal static readonly RecordGraphWriteValue<T>? Write=Create<RecordGraphWriteValue<T>>(nameof(WriteValue));
    internal static readonly RecordGraphReadValue<T>? Read=Create<RecordGraphReadValue<T>>(nameof(ReadValue));
    private static TDelegate? Create<TDelegate>(string method) where TDelegate:Delegate{
        var underlying=Nullable.GetUnderlyingType(typeof(T));
        if(underlying is null){return null;}
        return typeof(RecordGraphNullable<T>).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!
            .MakeGenericMethod(underlying).CreateDelegate<TDelegate>();
    }
    private static void WriteValue<TValue>(ref RecordGraphWriter writer,TValue? value) where TValue:struct{
        writer.Byte(value.HasValue?(byte)1:(byte)0);if(value.HasValue){writer.Write(value.GetValueOrDefault());}
    }
    private static TValue? ReadValue<TValue>(ref RecordGraphReader reader) where TValue:struct{
        if(!reader.Boolean()){return null;}return reader.Read<TValue>();
    }
}

internal static class RecordGraphScalar<T>{
    internal static void Write(ref RecordGraphWriter writer,T value){
        value=RecordScalarValue<T>.Normalize(value);
        if(typeof(T)==typeof(string)){writer.Text(Unsafe.As<T,string?>(ref value));return;}
        if(typeof(T)==typeof(decimal)){
            Span<int> bits=stackalloc int[4];decimal.GetBits(Unsafe.As<T,decimal>(ref value),bits);
            foreach(var part in bits){writer.Int32(part);}return;
        }
        if(typeof(T)==typeof(DateTime)){writer.UInt64((ulong)Unsafe.As<T,DateTime>(ref value).Ticks);return;}
        if(typeof(T)==typeof(DateTimeOffset)){writer.UInt64((ulong)Unsafe.As<T,DateTimeOffset>(ref value).UtcTicks);return;}
        if(typeof(T)==typeof(DateOnly)){writer.Int32(Unsafe.As<T,DateOnly>(ref value).DayNumber);return;}
        if(typeof(T)==typeof(TimeOnly)){writer.UInt64((ulong)Unsafe.As<T,TimeOnly>(ref value).Ticks);return;}
        if(typeof(T)==typeof(TimeSpan)){writer.UInt64((ulong)Unsafe.As<T,TimeSpan>(ref value).Ticks);return;}
        if(typeof(T)==typeof(Guid)){
            Span<byte> bytes=stackalloc byte[16];Unsafe.As<T,Guid>(ref value).TryWriteBytes(bytes);writer.Bytes(bytes);return;
        }
        if(typeof(T)==typeof(Int128)||typeof(T)==typeof(UInt128)){
            var number=Unsafe.As<T,UInt128>(ref value);writer.UInt64((ulong)number);writer.UInt64((ulong)(number>>64));return;
        }
        switch(Unsafe.SizeOf<T>()){
            case 1:writer.Byte(Unsafe.As<T,byte>(ref value));return;
            case 2:writer.UInt16(Unsafe.As<T,ushort>(ref value));return;
            case 4:writer.Int32(Unsafe.As<T,int>(ref value));return;
            case 8:writer.UInt64(Unsafe.As<T,ulong>(ref value));return;
            default:throw new NotSupportedException($"Unsupported record scalar '{typeof(T)}'.");
        }
    }

    internal static T Read(ref RecordGraphReader reader){
        T value=default!;
        if(typeof(T)==typeof(string)){Unsafe.As<T,string?>(ref value)=reader.Text();return value;}
        if(typeof(T)==typeof(decimal)){
            Span<int> bits=stackalloc int[4];for(var index=0;index<4;index++){bits[index]=reader.Int32();}
            try{Unsafe.As<T,decimal>(ref value)=new decimal(bits);}catch(ArgumentException error){throw new InvalidDataException("Invalid graph decimal.",error);}
        }else if(typeof(T)==typeof(DateTime)){
            var ticks=reader.UInt64();if(ticks>(ulong)DateTime.MaxValue.Ticks){throw new InvalidDataException("Invalid graph date.");}
            Unsafe.As<T,DateTime>(ref value)=new((long)ticks,DateTimeKind.Utc);
        }else if(typeof(T)==typeof(DateTimeOffset)){
            var ticks=reader.UInt64();if(ticks>(ulong)DateTime.MaxValue.Ticks){throw new InvalidDataException("Invalid graph date offset.");}
            Unsafe.As<T,DateTimeOffset>(ref value)=new((long)ticks,TimeSpan.Zero);
        }else if(typeof(T)==typeof(DateOnly)){
            var day=reader.Int32();if((uint)day>(uint)DateOnly.MaxValue.DayNumber){throw new InvalidDataException("Invalid graph day.");}
            Unsafe.As<T,DateOnly>(ref value)=DateOnly.FromDayNumber(day);
        }else if(typeof(T)==typeof(TimeOnly)){
            var ticks=reader.UInt64();if(ticks>(ulong)TimeOnly.MaxValue.Ticks){throw new InvalidDataException("Invalid graph time.");}
            Unsafe.As<T,TimeOnly>(ref value)=new((long)ticks);
        }else if(typeof(T)==typeof(TimeSpan)){
            Unsafe.As<T,TimeSpan>(ref value)=new(unchecked((long)reader.UInt64()));
        }else if(typeof(T)==typeof(Guid)){
            Unsafe.As<T,Guid>(ref value)=new(reader.Bytes(16));
        }else if(typeof(T)==typeof(Int128)||typeof(T)==typeof(UInt128)){
            var low=reader.UInt64();var high=reader.UInt64();Unsafe.As<T,UInt128>(ref value)=((UInt128)high<<64)|low;
        }else{
            switch(Unsafe.SizeOf<T>()){
                case 1:var part=reader.Byte();if(typeof(T)==typeof(bool)&&part>1){throw new InvalidDataException("Invalid graph boolean.");}Unsafe.As<T,byte>(ref value)=part;break;
                case 2:Unsafe.As<T,ushort>(ref value)=reader.UInt16();break;
                case 4:Unsafe.As<T,int>(ref value)=reader.Int32();break;
                case 8:Unsafe.As<T,ulong>(ref value)=reader.UInt64();break;
                default:throw new NotSupportedException($"Unsupported record scalar '{typeof(T)}'.");
            }
        }
        // 不正な非正規表現も同じ規則へ収束させ、境界後は既定表現だけを使う。
        return RecordScalarValue<T>.Normalize(value);
    }
}
