using System.Reflection;
using System.Linq.Expressions;
using global::System.Runtime.Loader;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Gateway.Hosting;

/// <summary>検証対象と稼働サーバーが同じ型付き保存データを開く。</summary>
public sealed class ComparisonHost:IDisposable{
    private readonly List<DatabaseContainer> opened=[];
    private FileStream? storageLease;
    public DatabaseRegistry Databases{get;}
    public ComparisonGeneration Generation{get;}
    public ComparisonHost(string directory,bool requireValidated=true){
        directory=Path.GetFullPath(directory);this.Generation=ComparisonGeneration.Read(directory);
        if(requireValidated&&this.Generation.State is not ("Validated" or "Active")){throw new InvalidDataException("Only a fully validated generation can be served.");}
        Directory.CreateDirectory(Path.Combine(directory,"data"));this.storageLease=new FileStream(Path.Combine(directory,"data",".seamless-open.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        this.Databases=new(this.Generation.CapturedUtc.UtcTicks);
        var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(ComparisonGeneration.ResolveManagedPath(directory,this.Generation.SchemaAssembly));
        try{
            foreach(var item in this.Generation.Databases){
                var type=assembly.GetType(item.TypeFullName,true)!;
                if(type.GetField("GeneratedSchemaFingerprint")?.GetRawConstantValue() as string!=item.Fingerprint){throw new InvalidDataException($"Schema assembly mismatch: {item.Name}.");}
                var options=DatabaseContainerOptions.OnStorage(ComparisonGeneration.ResolveManagedPath(directory,"data/"+item.StorageKey));
                var sizing=Path.Combine(options.StorageDirectory!,"storage-options.json");if(!Directory.Exists(options.StorageDirectory)||!File.Exists(sizing)){throw new InvalidDataException($"Imported storage is missing for '{item.Name}'.");}if(File.Exists(sizing)){options=global::System.Text.Json.JsonSerializer.Deserialize<DatabaseContainerOptions>(File.ReadAllText(sizing))??throw new InvalidDataException("Invalid storage options.");options=options with{StorageDirectory=ComparisonGeneration.ResolveManagedPath(directory,"data/"+item.StorageKey)};}
                ValidateRuntimeProfile(item,type,options);
                var database=(DatabaseContainer)Activator.CreateInstance(type,[options])!;this.opened.Add(database);
                var adapter=new InProcessDatabaseAdapter(database,item.Tables,item.Catalog);var views=item.Catalog.Schemas.SelectMany(s=>s.Views.Select(v=>new DatabaseViewDefinition(s.Name,v.Name,v.Definition??throw new InvalidDataException($"Encrypted or missing view: {item.Name}.{s.Name}.{v.Name}"),v.Columns.Select(c=>new SqlColumn(c.Name,ToSqlType(c.Type),c.Type.IsNullable,new(item.Name,s.Name,v.Name),c.Ordinal)).ToArray(),v.Columns))).ToArray();
                var routines=ResolveRoutines(item,assembly,database);
                this.Databases.Register(item.Name,adapter,item.ReadOnly,item.Fingerprint,views,routines,item.Catalog.DefaultCollation);
                var engine=new SqlQueryEngine(this.Databases);type.GetProperty("SqlViewRows")?.SetValue(database,(Func<string,IEnumerable<object?[]>>)(name=>engine.Prepare("SELECT * FROM "+name,item.Name).Execute().Rows));
            }
        }catch{this.Dispose();throw;}
    }
    /// <summary>manifest、生成型、保存形式の不一致を開く前に拒否する。</summary>
    public static void ValidateRuntimeProfile(ComparisonDatabase item,Type type,DatabaseContainerOptions options){
        ArgumentNullException.ThrowIfNull(item);ArgumentNullException.ThrowIfNull(type);ArgumentNullException.ThrowIfNull(options);
        if(type.FullName!=item.TypeFullName||type.GetField("GeneratedSchemaFingerprint")?.GetRawConstantValue() as string!=item.Fingerprint){throw new InvalidDataException("Schema assembly mismatch: "+item.Name);}
        var runtime=typeof(RuntimeSqlDatabaseContainer).IsAssignableFrom(type);
        if(runtime!=item.RuntimeSqlEnabled){throw new InvalidDataException("Runtime SQL manifest and generated type disagree: "+item.Name);}
        if(runtime&&(options.Transactions!=DatabaseTransactionMode.SharedV2||item.GeneratedNamespace!="Seamless.Generated."+item.GeneratedName||!item.GeneratedName.EndsWith("_RuntimeSqlV1",StringComparison.Ordinal))){throw new InvalidDataException("Invalid runtime SQL storage profile: "+item.Name);}
    }
    /// <summary>生成時の対応表を実アセンブリの署名と照合する。古い世代は再生成を要求する。</summary>
    public static IReadOnlyList<DatabaseRoutineDefinition>ResolveRoutines(ComparisonDatabase item,Assembly assembly,DatabaseContainer? database=null){
        var source=item.Catalog.Schemas.SelectMany(schema=>schema.Routines).ToArray();
        if(item.Routines.Count!=source.Length){throw new InvalidDataException("Generated routine bindings are missing or stale; rebuild generation: "+item.Name);}
        var bindings=item.Routines.ToDictionary(binding=>(binding.SourceSchemaName,binding.SourceName));
        var result=new List<DatabaseRoutineDefinition>();
        foreach(var routine in source){
            if(!bindings.TryGetValue((routine.SchemaName,routine.Name),out var binding)||binding.SourceDatabaseName!=item.Name||binding.Kind!=routine.Kind){throw new InvalidDataException("Generated routine identity mismatch: "+item.Name+"."+routine.SchemaName+"."+routine.Name);}
            var type=assembly.GetType(binding.DeclaringTypeName,true)!;
            if(type.Assembly!=assembly||!typeof(DatabaseSchema).IsAssignableFrom(type)||type.Namespace!=item.TypeNamespace+".Schemas"){throw new InvalidDataException("Generated routine declaring type mismatch.");}
            var methods=type.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).Where(method=>method.Name==binding.MethodName).ToArray();
            if(methods.Length!=1||methods[0].IsGenericMethod){throw new InvalidDataException("Generated routine method is missing or ambiguous: "+binding.MethodName);}
            var method=methods[0];var parameters=method.GetParameters();var declared=routine.Parameters.OrderBy(parameter=>parameter.Ordinal).ToArray();
            if(parameters.Length!=declared.Length){throw new InvalidDataException("Generated routine parameter count mismatch.");}
            for(var index=0;index<parameters.Length;index++){
                var expected=RoutineRuntimeType(declared[index].Type);var actual=parameters[index].ParameterType;
                if(actual.IsByRef){actual=actual.GetElementType()!;}
                if(actual!=expected||parameters[index].IsOut!=declared[index].IsOutput){throw new InvalidDataException("Generated routine parameter signature mismatch: "+binding.MethodName);}
            }
            var wrapper=routine.Kind switch{RoutineKind.ScalarFunction=>"Seamless.Database.ScalarFunction`1",RoutineKind.TableFunction=>"Seamless.Database.TableFunction`1",RoutineKind.StoredProcedure=>"Seamless.Database.StoredProcedure`1",_=>throw new InvalidDataException("Unknown routine kind.")};
            if(!method.ReturnType.IsGenericType||method.ReturnType.GetGenericTypeDefinition().FullName!=wrapper){throw new InvalidDataException("Generated routine return wrapper mismatch: "+binding.MethodName);}
            if(routine.Kind==RoutineKind.ScalarFunction){var expectedReturn=typeof(object);if(routine.ReturnType is not null){expectedReturn=RoutineRuntimeType(routine.ReturnType);}if(method.ReturnType.GetGenericArguments()[0]!=expectedReturn){throw new InvalidDataException("Generated scalar return type mismatch.");}}
            if(string.IsNullOrWhiteSpace(binding.CSharpSource)||string.IsNullOrWhiteSpace(binding.SourceFile)){throw new InvalidDataException("Generated routine source is missing.");}
            SqlNativeRoutine? native=null;
            if(!binding.IsStub){
                if(routine.Kind!=RoutineKind.ScalarFunction||routine.Implementation?.CompilerProfile!="SqlServerScalarTinyintCaseNvarcharV1"||routine.Parameters.Count!=1||routine.Parameters[0].IsOutput||routine.Parameters[0].IsTableValued||routine.Parameters[0].Type.Kind!=DatabaseTypeKind.Byte||routine.ReturnType?.Kind!=DatabaseTypeKind.String){throw new InvalidDataException("Generated routine is outside the supported native scalar profile: "+binding.MethodName);}
                if(database is not null){native=BindNativeRoutine(database,type,method,routine);}
            }
            result.Add(new(routine.SchemaName,routine.Name,routine.Definition,routine,binding,native));
        }
        return result.AsReadOnly();
    }
    private static SqlNativeRoutine BindNativeRoutine(DatabaseContainer database,Type schemaType,MethodInfo method,RoutineCatalog routine){
        var properties=database.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(property=>property.PropertyType==schemaType&&property.CanRead&&property.GetIndexParameters().Length==0).ToArray();
        if(properties.Length!=1||properties[0].GetValue(database) is not DatabaseSchema schema){throw new InvalidDataException("Generated routine schema instance is missing or ambiguous: "+schemaType.FullName);}
        var arguments=Expression.Parameter(typeof(IReadOnlyList<object?>),"arguments");
        var indexer=typeof(IReadOnlyList<object?>).GetProperty("Item")??throw new InvalidOperationException("Argument indexer is unavailable.");
        var methodParameters=method.GetParameters();var converted=new Expression[methodParameters.Length];
        for(var i=0;i<converted.Length;i++){converted[i]=Expression.Convert(Expression.Property(arguments,indexer,Expression.Constant(i)),methodParameters[i].ParameterType);}
        var call=Expression.Call(Expression.Constant(schema,schemaType),method,converted);
        var valueProperty=method.ReturnType.GetProperty("Value",BindingFlags.Public|BindingFlags.Instance)??throw new InvalidDataException("Generated scalar result value is unavailable.");
        var body=Expression.Convert(Expression.Property(call,valueProperty),typeof(object));
        var invoke=Expression.Lambda<Func<IReadOnlyList<object?>,object?>>(body,arguments).Compile();
        var parameters=routine.Parameters.OrderBy(parameter=>parameter.Ordinal).Select(parameter=>new SqlParameter(parameter.Name,ToSqlType(parameter.Type),parameter.Type.IsNullable)).ToArray();
        var returnType=routine.ReturnType??throw new InvalidDataException("Generated scalar result metadata is missing.");
        return new(parameters,ToSqlType(returnType),returnType.IsNullable,invoke);
    }
    private static Type RoutineRuntimeType(DatabaseType type){
        var runtime=type.RuntimeType;if(runtime is null){if(type.Kind==DatabaseTypeKind.Object){runtime=typeof(object);}else{runtime=ToSqlType(type).ClrType;}}
        if(type.IsNullable&&runtime.IsValueType&&Nullable.GetUnderlyingType(runtime)is null){runtime=typeof(Nullable<>).MakeGenericType(runtime);}
        return runtime;
    }
    private static string QuotePath(string name)=>string.Join(".",name.Split('.').Select(s=>"["+s.Replace("]","]]")+ "]"));
    public static SqlType ToSqlType(DatabaseType type){
        var runtime=type.RuntimeType??type.Kind switch{DatabaseTypeKind.Boolean=>typeof(bool),DatabaseTypeKind.Byte=>typeof(byte),DatabaseTypeKind.Int16=>typeof(short),DatabaseTypeKind.Int32=>typeof(int),DatabaseTypeKind.Int64=>typeof(long),DatabaseTypeKind.Decimal=>typeof(decimal),DatabaseTypeKind.Single=>typeof(float),DatabaseTypeKind.Double=>typeof(double),DatabaseTypeKind.Binary=>typeof(byte[]),DatabaseTypeKind.Guid=>typeof(Guid),DatabaseTypeKind.Date=>typeof(DateOnly),DatabaseTypeKind.Time=>typeof(TimeOnly),DatabaseTypeKind.DateTime=>typeof(DateTime),DatabaseTypeKind.DateTimeOffset=>typeof(DateTimeOffset),DatabaseTypeKind.String or DatabaseTypeKind.Xml=>typeof(string),_=>throw new NotSupportedException($"Database type '{type.Kind}' is not supported.")};
        if(runtime.FullName=="Seamless.Database.SqlServer.Types.Bit"){runtime=typeof(bool);}
        var name=type.ResultSqlTypeName??type.SqlTypeName??DefaultSqlTypeName(type.Kind,runtime);
        return new(name,runtime,type.ResultMaximumLength??type.MaximumLength,type.Precision,type.Scale,type.Collation);
    }
    private static string DefaultSqlTypeName(DatabaseTypeKind kind,Type runtime){
        if(kind==DatabaseTypeKind.Object||!Enum.IsDefined(kind)){throw new NotSupportedException($"Database type '{kind}'/'{runtime}' is not supported.");}
        if(kind==DatabaseTypeKind.Xml&&runtime==typeof(string)){return "xml";}
        if(runtime==typeof(bool)){return "bit";}if(runtime==typeof(byte)){return "tinyint";}if(runtime==typeof(short)){return "smallint";}if(runtime==typeof(int)){return "int";}if(runtime==typeof(long)){return "bigint";}
        if(runtime==typeof(decimal)){return "decimal";}if(runtime==typeof(float)){return "real";}if(runtime==typeof(double)){return "float";}if(runtime==typeof(string)){return "nvarchar";}if(runtime==typeof(byte[])){return "varbinary";}
        if(runtime==typeof(Guid)){return "uniqueidentifier";}if(runtime==typeof(DateOnly)){return "date";}if(runtime==typeof(TimeOnly)||runtime==typeof(TimeSpan)){return "time";}if(runtime==typeof(DateTime)){return "datetime2";}if(runtime==typeof(DateTimeOffset)){return "datetimeoffset";}
        throw new NotSupportedException($"Database type '{kind}'/'{runtime}' is not supported.");
    }
    public void Dispose(){foreach(var database in this.opened.AsEnumerable().Reverse()){database.Dispose();}this.opened.Clear();this.storageLease?.Dispose();this.storageLease=null;}
}
