using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Reflection.Emit;

namespace Seamless.Execution;

internal delegate void RecordGraphSkipFields(ref RecordGraphReader reader,long count);

internal sealed partial class RecordGraphPolicy{
    private static RecordGraphSkipFields BuildSkipper(Type type,RecordTypePolicy values){
        var underlying=Nullable.GetUnderlyingType(type);
        if(underlying is not null){return SkipMethod(nameof(SkipNullable),underlying);}
        if(RecordScalarValue.IsScalar(type)){return SkipMethod(nameof(SkipScalar),type);}
        if(type.IsArray){return SkipMethod(nameof(SkipElements),type.GetElementType()!);}
        if(values.InlineElement is{} element){
            var skip=SkipMethod(nameof(SkipElements),element);var length=values.InlineLength;
            return(ref RecordGraphReader reader,long count)=>skip(ref reader,length);
        }
        var method=new DynamicMethod("SkipRecordGraphFields",typeof(void),[typeof(RecordGraphReader).MakeByRefType(),typeof(long)],typeof(RecordGraphPolicy).Module,true);
        var il=method.GetILGenerator();
        foreach(var field in values.Fields){
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call,typeof(RecordGraphReader).GetMethod(nameof(RecordGraphReader.Skip))!.MakeGenericMethod(field.FieldType));
        }
        il.Emit(OpCodes.Ret);return method.CreateDelegate<RecordGraphSkipFields>();
    }
    private static RecordGraphSkipFields SkipMethod(string method,Type type)=>typeof(RecordGraphPolicy)
        .GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.MakeGenericMethod(type).CreateDelegate<RecordGraphSkipFields>();
    private static void SkipScalar<T>(ref RecordGraphReader reader,long count)=>_ = RecordGraphScalar<T>.Read(ref reader);
    private static void SkipNullable<T>(ref RecordGraphReader reader,long count) where T:struct{
        if(reader.Boolean()){reader.Skip<T>();}
    }
    private static void SkipElements<T>(ref RecordGraphReader reader,long count){
        for(long index=0;index<count;index++){reader.Skip<T>();}
    }
}
