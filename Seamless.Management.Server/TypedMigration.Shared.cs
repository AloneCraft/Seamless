using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Seamless.Database;

namespace Seamless.Management.Server;

internal static partial class TypedMigration{
    private delegate void RowWriter(IEnumerable<object[]> rows,CancellationToken cancellation,Action rowCopied,DatabaseContainer.UnpublishedSnapshotBuilder? snapshot);
    private static bool SameAssemblyName(Type before,Type after)=>string.Equals(before.Assembly.GetName().Name,after.Assembly.GetName().Name,StringComparison.OrdinalIgnoreCase);
    private static bool UseValueBridge(Type before,Type after,TablePlan[] tables)=>SameAssemblyName(before,after)||tables.Any(table=>table.Types.Any(type=>type.IsCollectible));
    private static CompiledMigration CompileReader(Type before,Type after,byte[] beforeBytes,TablePlan[] tables,CapturedSourceBuild? beforeBuild){
        //同名の新旧DLLを一つのALCへ束縛できないため、旧型だけを参照する読み出し側を生成する。
        var code=new StringBuilder("extern alias previous;public static class Migration{");
        var converter=new MigrationValueConverter(before.Assembly,after.Assembly);
        for(var index=0;index<tables.Length;index++){
            var table=tables[index];var parameters=table.Types;
            code.Append("public static global::System.Collections.Generic.IEnumerable<object[]> Read").Append(index).Append("(object source,global::System.Threading.CancellationToken cancellation){var before=(previous::").Append(TypeName(before)).Append(")source;foreach(var row in before.").Append(Access(table.Before.Path)).Append("){cancellation.ThrowIfCancellationRequested();yield return new object[]{");
            for(var column=0;column<parameters.Length;column++){
                if(column>0){code.Append(',');}
                if(table.Values[column]=="default"){code.Append("null");continue;}
                var type=converter.Prepare(parameters[column]);
                code.Append('(').Append(ReaderTypeName(type,before.Assembly)).Append(")(").Append(table.Values[column]).Append(')');
            }
            code.Append("};}}");
        }
        code.Append('}');var references=SourceEditor.PlatformReferences().ToList();references.Add(MetadataReference.CreateFromImage(beforeBytes,new MetadataReferenceProperties(aliases:["previous"])));
        if(beforeBuild is not null){
            foreach(var dependency in beforeBuild.Build.Dependencies.Where(dependency=>!dependency.Shared)){
                references.Add(MetadataReference.CreateFromImage(beforeBuild.Bytes(dependency.Path)));
            }
        }
        return Emit(code,references);
    }
    private static string ReaderTypeName(Type type,Assembly before){
        if(type.IsArray){return ReaderTypeName(type.GetElementType()!,before)+"["+new string(',',type.GetArrayRank()-1)+"]";}
        var prefix="global::";if(type.Assembly==before){prefix="previous::";}
        var definition=type;if(type.IsGenericType){definition=type.GetGenericTypeDefinition();}
        var arguments=type.GetGenericArguments();var index=0;
        return prefix+string.Join(".",definition.FullName!.Split('+').Select(part=>{
            var marker=part.IndexOf('`');var name=part;if(marker>=0){name=part[..marker];}
            name=string.Join(".",name.Split('.').Select(identifier=>"@"+identifier));
            if(marker<0){return name;}
            var count=int.Parse(part[(marker+1)..],System.Globalization.CultureInfo.InvariantCulture);
            var values=arguments.Skip(index).Take(count).Select(argument=>ReaderTypeName(argument,before));index+=count;
            return name+"<"+string.Join(",",values)+">";
        }));
    }
    private static Action<object,object,CancellationToken,Action<string,long>?,DatabaseContainer.UnpublishedSnapshotBuilder?> CreateSharedCopy(Assembly readerAssembly,DatabaseInstance before,DatabaseInstance after,MigrationDefinition? migration){
        var tables=Plan(before.Database.GetType(),after.Database.GetType(),migration).Tables;var readers=new Func<object,CancellationToken,IEnumerable<object[]>>[tables.Length];var writers=new RowWriter[tables.Length];
        var converter=new MigrationValueConverter(before.Assembly,after.Assembly);var parameters=new Type[tables.Length][];
        for(var index=0;index<tables.Length;index++){
            var table=tables[index];readers[index]=readerAssembly.GetType("Migration")!.GetMethod("Read"+index)!.CreateDelegate<Func<object,CancellationToken,IEnumerable<object[]>>>();
            parameters[index]=table.Types;
            for(var parameter=0;parameter<parameters[index].Length;parameter++){
                if(table.Values[parameter]=="default"){converter.PrepareDefault(parameters[index][parameter]);}
                else{converter.Prepare(parameters[index][parameter]);}
            }
            var types=table.After.Property.PropertyType.GenericTypeArguments;var method=nameof(CreateSetWriter);if(types.Length==2){method=nameof(CreateKeyedWriter);}
            var factory=typeof(TypedMigration).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.MakeGenericMethod(types).CreateDelegate<Func<object,TablePlan,RowWriter>>();
            writers[index]=factory(table.After.GetTable(after.Database),table);
        }
        return(source,_,cancellation,checkpoint,snapshot)=>{
            long count=0;
            for(var index=0;index<tables.Length;index++){
                var path=tables[index].After.Path;
                var rows=converter.ConvertRows(readers[index](source,cancellation),parameters[index],after.Database.ExecutionResources.Budget,cancellation);
                writers[index](rows,cancellation,()=>{count++;checkpoint?.Invoke(path,count);},snapshot);
            }
        };
    }
    // 列の値の配列(TablePlan.Columns の順)から行を作る。生成した行は作り方に従い、手書きの行は同じ型の並びのコンストラクターを呼ぶ。
    private static Func<object[],TRecord> Constructor<TRecord>(TablePlan table){
        var values=Expression.Parameter(typeof(object[]),"values");
        Expression Value(int index)=>Expression.Convert(Expression.ArrayIndex(values,Expression.Constant(index)),table.Types[index]);
        Expression body;
        if(GeneratedRecordShape.TryCreate(typeof(TRecord)) is{} shape){body=shape.New(name=>Value(Array.IndexOf(table.Columns,name)));}
        else{
            var constructor=typeof(TRecord).GetConstructor(table.Types)??throw new ManagementException("MigrationRequired","公開された行コンストラクターが必要です。");
            body=Expression.New(constructor,table.Types.Select((_,index)=>Value(index)));
        }
        return Expression.Lambda<Func<object[],TRecord>>(body,values).Compile();
    }
    private static RowWriter CreateKeyedWriter<TKey,TRecord>(object target,TablePlan plan) where TKey:struct where TRecord:struct{
        var table=(Table<TKey,TRecord>)target;var create=Constructor<TRecord>(plan);
        return(rows,cancellation,rowCopied,snapshot)=>{
            foreach(var values in rows){
                cancellation.ThrowIfCancellationRequested();var row=create(values);
                if(snapshot is not null){snapshot.Insert(table,row);}
                else if(!table.Add(row)){throw new InvalidOperationException("移行先のキーが重複します。");}
                rowCopied();
            }
        };
    }
    private static RowWriter CreateSetWriter<TRecord>(object target,TablePlan plan) where TRecord:struct{
        var table=(Table<TRecord>)target;var create=Constructor<TRecord>(plan);
        return(rows,cancellation,rowCopied,snapshot)=>{
            foreach(var values in rows){
                cancellation.ThrowIfCancellationRequested();var row=create(values);
                if(snapshot is not null){throw new ManagementException("MigrationRequired","キーなし表の非公開スナップショット移行には対応していません。");}
                if(!table.Add(row)){throw new InvalidOperationException("移行先のキーが重複します。");}
                rowCopied();
            }
        };
    }
}
