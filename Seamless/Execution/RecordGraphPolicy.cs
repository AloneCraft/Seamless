using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Seamless.Execution;

internal delegate void RecordGraphWriteFields(object value,ref RecordGraphWriter writer);
internal delegate void RecordGraphReadFields(object value,ref RecordGraphReader reader);
internal delegate void RecordGraphWriteValue<T>(ref RecordGraphWriter writer,T value);
internal delegate T RecordGraphReadValue<T>(ref RecordGraphReader reader);

internal sealed partial class RecordGraphPolicy{
    private static readonly ConditionalWeakTable<Type,RecordGraphPolicy> Policies=new();
    internal byte[] Schema{get;private init;}=null!;
    internal RecordTypePolicy Values{get;private init;}=null!;
    internal RecordGraphWriteFields WriteFields{get;private set;}=null!;
    internal RecordGraphReadFields ReadFields{get;private set;}=null!;
    internal RecordGraphSkipFields SkipFields{get;private set;}=null!;
    internal int ElementSize{get;private set;}
    internal static RecordGraphPolicy Get(Type type)=>Policies.GetValue(type,Create);

    private static RecordGraphPolicy Create(Type type){
        var values=RecordTypePolicy.Get(type);
        var schema=new StringBuilder("seamless-record-graph-v1|normalization-v1|");
        AppendSchema(type,schema,new HashSet<Type>());
        var policy=new RecordGraphPolicy{Values=values,Schema=SHA256.HashData(Encoding.UTF8.GetBytes(schema.ToString()))};
        if(values.InlineElement is{} element){
            var inline=typeof(RecordGraphPolicy).GetMethod(nameof(InlineMethods),BindingFlags.Static|BindingFlags.NonPublic)!
                .MakeGenericMethod(type,element).CreateDelegate<Func<(RecordGraphWriteFields,RecordGraphReadFields)>>()();
            policy.WriteFields=inline.Item1;policy.ReadFields=inline.Item2;
        }else if(type.IsArray){
            var array=typeof(RecordGraphPolicy).GetMethod(nameof(ArrayMethods),BindingFlags.Static|BindingFlags.NonPublic)!
                .MakeGenericMethod(type.GetElementType()!).CreateDelegate<Func<(RecordGraphWriteFields,RecordGraphReadFields,int)>>()();
            policy.WriteFields=array.Item1;policy.ReadFields=array.Item2;policy.ElementSize=array.Item3;
        }else{
            policy.WriteFields=BuildWriter(type,values.Fields);
            policy.ReadFields=BuildReader(type,values.Fields);
        }
        policy.SkipFields=BuildSkipper(type,values);
        return policy;
    }

    private static void AppendSchema(Type type,StringBuilder schema,HashSet<Type> visited){
        RuntimeHelpers.EnsureSufficientExecutionStack();
        if(type.IsArray){schema.Append(type.IsSZArray?"vector":"array").Append(type.GetArrayRank());AppendSchema(type.GetElementType()!,schema,visited);return;}
        if(type.Assembly.IsDynamic){throw new NotSupportedException("Record graph storage requires precompiled types.");}
        schema.Append(type.Module.ModuleVersionId).Append('/').Append(type.MetadataToken).Append('[');
        if(type.IsGenericType){foreach(var argument in type.GetGenericArguments()){AppendSchema(argument,schema,visited);}}
        schema.Append(']');
        if(!visited.Add(type)){schema.Append('@');return;}
        if(RecordScalarValue.IsScalar(type)){return;}
        var values=RecordTypePolicy.Get(type);
        if(values.InlineElement is{} element){schema.Append("inline").Append(values.InlineLength);AppendSchema(element,schema,visited);}
        foreach(var field in values.Fields){schema.Append('{').Append(field.MetadataToken).Append(':');AppendSchema(field.FieldType,schema,visited);schema.Append('}');}
    }

    private static void Owner(ILGenerator il,Type type){il.Emit(OpCodes.Ldarg_0);il.Emit(type.IsValueType?OpCodes.Unbox:OpCodes.Castclass,type);}

    private static RecordGraphWriteFields BuildWriter(Type type,FieldInfo[] fields){
        if(RecordScalarValue.IsScalar(type)){
            return typeof(RecordGraphPolicy).GetMethod(nameof(WriteScalar),BindingFlags.Static|BindingFlags.NonPublic)!
                .MakeGenericMethod(type).CreateDelegate<RecordGraphWriteFields>();
        }
        var method=new DynamicMethod("WriteRecordGraphFields",typeof(void),[typeof(object),typeof(RecordGraphWriter).MakeByRefType()],typeof(RecordGraphPolicy).Module,true);
        var il=method.GetILGenerator();
        foreach(var field in fields){
            il.Emit(OpCodes.Ldarg_1);Owner(il,field.DeclaringType!);il.Emit(OpCodes.Ldfld,field);
            il.Emit(OpCodes.Call,typeof(RecordGraphWriter).GetMethod(nameof(RecordGraphWriter.Write))!.MakeGenericMethod(field.FieldType));
        }
        il.Emit(OpCodes.Ret);return method.CreateDelegate<RecordGraphWriteFields>();
    }

    private static RecordGraphReadFields BuildReader(Type type,FieldInfo[] fields){
        if(type==typeof(string)||Nullable.GetUnderlyingType(type) is not null){
            return static (object value,ref RecordGraphReader reader)=>throw new InvalidDataException("This scalar cannot be restored as a boxed graph node.");
        }
        if(RecordScalarValue.IsScalar(type)){
            return typeof(RecordGraphPolicy).GetMethod(nameof(ReadScalar),BindingFlags.Static|BindingFlags.NonPublic)!
                .MakeGenericMethod(type).CreateDelegate<RecordGraphReadFields>();
        }
        var method=new DynamicMethod("ReadRecordGraphFields",typeof(void),[typeof(object),typeof(RecordGraphReader).MakeByRefType()],typeof(RecordGraphPolicy).Module,true);
        var il=method.GetILGenerator();
        foreach(var field in fields){
            Owner(il,field.DeclaringType!);il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call,typeof(RecordGraphReader).GetMethod(nameof(RecordGraphReader.Read))!.MakeGenericMethod(field.FieldType));
            il.Emit(OpCodes.Stfld,field);
        }
        il.Emit(OpCodes.Ret);return method.CreateDelegate<RecordGraphReadFields>();
    }

    private static void WriteScalar<T>(object value,ref RecordGraphWriter writer)=>writer.Write((T)value);
    private static void ReadScalar<T>(object value,ref RecordGraphReader reader) where T:struct=>Unsafe.Unbox<T>(value)=reader.Read<T>();

    private static (RecordGraphWriteFields,RecordGraphReadFields) InlineMethods<T,TElement>() where T:struct{
        var length=RecordTypePolicy.Get(typeof(T)).InlineLength;
        return(
            (object value,ref RecordGraphWriter writer)=>{
                ref var element=ref Unsafe.As<T,TElement>(ref Unsafe.Unbox<T>(value));
                for(var index=0;index<length;index++){writer.Write(Unsafe.Add(ref element,index));}
            },
            (object value,ref RecordGraphReader reader)=>{
                ref var element=ref Unsafe.As<T,TElement>(ref Unsafe.Unbox<T>(value));
                for(var index=0;index<length;index++){Unsafe.Add(ref element,index)=reader.Read<TElement>();}
            }
        );
    }

    private static (RecordGraphWriteFields,RecordGraphReadFields,int) ArrayMethods<T>()=>(WriteArray<T>,ReadArray<T>,Unsafe.SizeOf<T>());
    private static void WriteArray<T>(object value,ref RecordGraphWriter writer){
        if(value is T[] vector){foreach(var item in vector){writer.Write(item);}return;}
        foreach(T item in(Array)value){writer.Write(item);}
    }
    private static void ReadArray<T>(object value,ref RecordGraphReader reader){
        if(value is T[] vector){for(var index=0;index<vector.Length;index++){vector[index]=reader.Read<T>();}return;}
        var array=(Array)value;
        var indices=global::System.Linq.Enumerable.Range(0,array.Rank).Select(array.GetLowerBound).ToArray();
        for(long index=0;index<array.LongLength;index++){
            array.SetValue(reader.Read<T>(),indices);
            for(var dimension=array.Rank-1;dimension>=0;dimension--){
                if(indices[dimension]<array.GetUpperBound(dimension)){indices[dimension]++;break;}
                indices[dimension]=array.GetLowerBound(dimension);
            }
        }
    }
}
