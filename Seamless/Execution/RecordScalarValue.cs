using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Runtime.CompilerServices;
using Seamless.Collections;

namespace Seamless.Execution;

internal static class RecordHash{
    internal const int Seed=unchecked((int)2166136261);
    internal static int Add(int hash,int value)=>unchecked((hash^value)*16777619);
    internal static int Text(string? value){
        if(value is null){return 0;}
        var hash=Seed;
        foreach(var character in value){hash=Add(hash,character);}
        return hash;
    }
}

internal static class RecordScalarValue{
    internal static bool IsScalar(Type type){
        var actual=Nullable.GetUnderlyingType(type)??type;
        return actual.IsPrimitive||actual.IsEnum||actual==typeof(string)||actual==typeof(decimal)
            ||actual==typeof(DateTime)||actual==typeof(DateTimeOffset)||actual==typeof(Half)
            ||actual==typeof(Guid)||actual==typeof(TimeSpan)||actual==typeof(DateOnly)||actual==typeof(TimeOnly)
            ||actual==typeof(Int128)||actual==typeof(UInt128);
    }

    internal static T? NormalizeNullable<T>(T? value) where T:struct=>
        value.HasValue?RecordScalarValue<T>.Normalize(value.GetValueOrDefault()):null;
    internal static int HashNullable<T>(T? value) where T:struct=>
        value.HasValue?RecordScalarValue<T>.Hash(value.GetValueOrDefault()):0;
    internal static bool IsNullableEmpty<T>(T? value) where T:struct=>!value.HasValue;
}

internal static class RecordScalarValue<T>{
    internal static readonly bool IsScalar=RecordScalarValue.IsScalar(typeof(T));
    internal static readonly Func<T,T> Normalize=CreateNormalizer();
    internal static readonly Func<T,int> Hash=CreateHasher();
    internal static readonly Func<T,bool> IsNull=CreateNullCheck();

    private static Func<T,bool> CreateNullCheck(){
        var underlying=Nullable.GetUnderlyingType(typeof(T));
        if(underlying is null){
            if(typeof(T).IsValueType){return static _=>false;}
            return static value=>value is null;
        }
        return typeof(RecordScalarValue).GetMethod(nameof(RecordScalarValue.IsNullableEmpty),BindingFlags.Static|BindingFlags.NonPublic)!
            .MakeGenericMethod(underlying).CreateDelegate<Func<T,bool>>();
    }

    private static Func<T,T> CreateNormalizer(){
        var underlying=Nullable.GetUnderlyingType(typeof(T));
        if(underlying is null){return NormalizeValue;}
        return typeof(RecordScalarValue).GetMethod(nameof(RecordScalarValue.NormalizeNullable),BindingFlags.Static|BindingFlags.NonPublic)!
            .MakeGenericMethod(underlying).CreateDelegate<Func<T,T>>();
    }

    private static Func<T,int> CreateHasher(){
        var underlying=Nullable.GetUnderlyingType(typeof(T));
        if(underlying is null){return HashValue;}
        return typeof(RecordScalarValue).GetMethod(nameof(RecordScalarValue.HashNullable),BindingFlags.Static|BindingFlags.NonPublic)!
            .MakeGenericMethod(underlying).CreateDelegate<Func<T,int>>();
    }

    private static T NormalizeValue(T value){
        if(typeof(T)==typeof(float)){Unsafe.As<T,float>(ref value)=ScalarNormalization.Normalize(Unsafe.As<T,float>(ref value));}
        else if(typeof(T)==typeof(double)){Unsafe.As<T,double>(ref value)=ScalarNormalization.Normalize(Unsafe.As<T,double>(ref value));}
        else if(typeof(T)==typeof(Half)){Unsafe.As<T,Half>(ref value)=ScalarNormalization.Normalize(Unsafe.As<T,Half>(ref value));}
        else if(typeof(T)==typeof(decimal)){Unsafe.As<T,decimal>(ref value)=ScalarNormalization.Normalize(Unsafe.As<T,decimal>(ref value));}
        else if(typeof(T)==typeof(DateTime)){Unsafe.As<T,DateTime>(ref value)=ScalarNormalization.Normalize(Unsafe.As<T,DateTime>(ref value));}
        else if(typeof(T)==typeof(DateTimeOffset)){Unsafe.As<T,DateTimeOffset>(ref value)=ScalarNormalization.Normalize(Unsafe.As<T,DateTimeOffset>(ref value));}
        return value;
    }

    internal static bool Equal(T first,T second)=>EqualityComparer<T>.Default.Equals(Normalize(first),Normalize(second));

    private static int HashValue(T value){
        value=Normalize(value);
        if(typeof(T)==typeof(string)){return RecordHash.Text(Unsafe.As<T,string?>(ref value));}
        if(typeof(T)==typeof(decimal)){
            Span<int> bits=stackalloc int[4];
            decimal.GetBits(Unsafe.As<T,decimal>(ref value),bits);
            var hash=RecordHash.Seed;
            foreach(var part in bits){hash=RecordHash.Add(hash,part);}
            return hash;
        }
        if(typeof(T)==typeof(DateTime)){return Unsafe.As<T,DateTime>(ref value).Ticks.GetHashCode();}
        if(typeof(T)==typeof(DateTimeOffset)){return Unsafe.As<T,DateTimeOffset>(ref value).UtcTicks.GetHashCode();}
        if(typeof(T)==typeof(Guid)){
            Span<byte> bytes=stackalloc byte[16];
            Unsafe.As<T,Guid>(ref value).TryWriteBytes(bytes);
            var hash=RecordHash.Seed;
            foreach(var part in bytes){hash=RecordHash.Add(hash,part);}
            return hash;
        }
        return EqualityComparer<T>.Default.GetHashCode(value!);
    }
}
