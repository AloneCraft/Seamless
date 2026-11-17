using System.Security.Cryptography;
using System.Text;

namespace Seamless.Database.Rdbms;

public static partial class CSharpDatabaseGenerator {
    public static GeneratedDatabase Generate(DatabaseCatalog catalog,bool preserveSqlRows=false,bool executableViews=false)=>Generate(catalog,preserveSqlRows,executableViews,false);
    public static GeneratedDatabase Generate(DatabaseCatalog catalog,bool preserveSqlRows,bool executableViews,bool runtimeSql)=>Generate(catalog,preserveSqlRows,executableViews,runtimeSql,false);
    public static GeneratedDatabase Generate(DatabaseCatalog catalog,bool preserveSqlRows,bool executableViews,bool runtimeSql,bool collectible)=>
        Generate(catalog,preserveSqlRows,executableViews,runtimeSql,collectible,null);

    /// <summary>
    /// 取り込み用に生成する。ビューは <paramref name="views"/> の訳で C# のビューにし、訳せないビューは取り込まずに
    /// <see cref="GeneratedDatabase.Compatibility"/> へ回避策つきで入れる。写しは利用者が選んだビューだけ。
    /// </summary>
    public static GeneratedDatabase Generate(DatabaseCatalog catalog,ViewImportOptions views){
        ArgumentNullException.ThrowIfNull(views);
        return Generate(catalog,false,false,false,false,views);
    }

    private static GeneratedDatabase Generate(DatabaseCatalog catalog,bool preserveSqlRows,bool executableViews,bool runtimeSql,bool collectible,ViewImportOptions? viewImport) {
        var originalCatalog=catalog;
        if(preserveSqlRows){catalog=CreateImportCatalog(catalog);}
        ArgumentNullException.ThrowIfNull(catalog);
        Validate(catalog);
        var databaseName = CSharpIdentifier.Normalize(catalog.Name);
        if(preserveSqlRows){databaseName+="_"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(catalog.Name)))[..8];}
        if(runtimeSql){databaseName=FileName(databaseName)+"_RuntimeSqlV1";}
        var typeNamespace=runtimeSql?"Seamless.Generated."+FileName(databaseName):databaseName;
        var schemas = CreateNames(catalog,databaseName);
        var fingerprint = CreateFingerprint(originalCatalog);
        if(preserveSqlRows||executableViews){fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint+"|"+preserveSqlRows+"|"+executableViews)));}
        if(runtimeSql){fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint+"|RuntimeSqlV1")));}
        if(collectible){fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint+"|CollectibleV1")));}
        // [生成物の形] 行を Tables と [Key] で宣言する形(旧方式の Keys と Records の二重書きは廃止)。前の形の生成物を同じ指紋で使わない。
        fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint+"|DeclaredTablesV1")));
        var views=viewImport is null?null:TranslateViews(catalog,schemas,typeNamespace,$"global::{typeNamespace}.{databaseName}",viewImport);
        if(views is not null){
            // 訳の版と、写しにすると選んだビューが変われば、生成物も変わる。
            fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint+"|ViewTranslation:"+viewImport!.Translator.Version+":"+string.Join(",",views.Snapshots.Order(StringComparer.OrdinalIgnoreCase)))));
        }
        var routines=new List<GeneratedRoutineBinding>();
        var files = new List<GeneratedSourceFile> {
            new($"{FileName(databaseName)}.cs",CreateDatabaseSource(databaseName,typeNamespace,fingerprint,schemas,executableViews,catalog.Name,routines,runtimeSql,collectible)),
        };
        // [宣言の形] 表は Tables.<スキーマ> に [Key] 付きで書き、キーの型とコンテナの表のプロパティは DB の生成器が作る。
        // ビューは、訳す指定(ViewImportOptions)があれば C# のビューにし、写しは利用者が選んだものだけ(CSharpDatabaseGenerator.Views.cs)。
        // 指定が無ければ、取り込んだ時点の写しを表として出す(SQL Server へ直接問い合わせる実行用のビューは、行の型だけ Views.<スキーマ> に出す)。
        // 関数・手続きの結果の行は Functions・Procedures に出す。
        foreach(var schema in schemas) {
            foreach(var table in schema.Tables) {
                files.Add(new(
                    $"Tables/{FileName(schema.Name)}/{FileName(table.Name)}.cs",
                    CreateTableSource(typeNamespace,schema,table)));
            }
            foreach(var view in schema.Views) {
                if(views is not null){
                    // 訳したビューの行の型と入口は、まとめて1つのファイル(下)に出す。訳せないビューは出さない。
                    if(views.IsSnapshot(schema.Source.Name,view.Source.Name)){files.Add(new($"Tables/{FileName(schema.Name)}/{FileName(view.Name)}.cs",CreateTableSource(typeNamespace,schema,view)));}
                }else if(executableViews){
                    files.Add(new($"Views/{FileName(schema.Name)}/{FileName(view.Name)}.cs",CreateViewRowSource(typeNamespace,schema,view)));
                }else{
                    files.Add(new($"Tables/{FileName(schema.Name)}/{FileName(view.Name)}.cs",CreateTableSource(typeNamespace,schema,view)));
                }
            }
            foreach(var routine in schema.Routines.Where(static routine=>routine.Source.ResultColumns.Count>0)) {
                files.Add(new(
                    $"{RoutineRowNamespace(routine)}/{FileName(routine.TypeName)}.cs",
                    CreateRoutineRowSource(typeNamespace,routine)));
            }
        }
        var tables=schemas.SelectMany(schema=>
            schema.Tables.Select(table=>new GeneratedTableBinding(
                $"{FileName(schema.Name)}.{FileName(table.Name)}",
                schema.Source.Name,
                table.Source.Name,
                table.Columns.Select(static column=>new GeneratedColumnBinding(
                    column.Source.Name,
                    FileName(column.Name),
                    column.Source.Type,column.Source.IsSynthetic)).ToArray(),
                GeneratedRelationKind.Table))
             .Concat(schema.Views.Select(view=>new GeneratedTableBinding(
                 $"{FileName(schema.Name)}.{FileName(view.Name)}",
                schema.Source.Name,
                view.Source.Name,
                view.Columns.Select(static column=>new GeneratedColumnBinding(
                    column.Source.Name,
                    FileName(column.Name),
                    column.Source.Type,column.Source.IsSynthetic)).ToArray(),
                 GeneratedRelationKind.View)))).ToArray();
        if(executableViews){tables=tables.Where(static table=>table.Kind==GeneratedRelationKind.Table).ToArray();}
        var compatibility=new List<CompatibilityIssue>();
        if(views is not null){
            // 写しにするビューだけが行を取り込む表として残る。
            tables=tables.Where(table=>table.Kind==GeneratedRelationKind.Table||views.IsSnapshot(table.SourceSchema,table.SourceTable)).ToArray();
            if(views.Source is not null){files.Add(views.Source);}
            // 元のビューの定義を残す(同じ製品へ書き出すとき元の定義のまま作り直す。訳せず取り込まなかったビューも含む)。Queries/SourceDefinitions.cs に出す。
            // 関数・手続きの元の定義も残す(計算列・既定値・CHECK が使う関数を、書き出しで表より先に作る)。
            static string Verbatim(string text)=>"@\""+text.Replace("\"","\"\"",StringComparison.Ordinal)+"\"";
            var definitions=catalog.Schemas.SelectMany(static schema=>schema.Views.Where(static view=>view.Definition is not null)
                .Select(view=>$"[assembly: global::Seamless.Database.ImportedDatabaseView({Literal(schema.Name)},{Literal(view.Name)},{Verbatim(view.Definition!)})]"))
                .Concat(catalog.Schemas.SelectMany(static schema=>schema.Routines.Where(static routine=>routine.Definition is not null)
                .Select(routine=>$"[assembly: global::Seamless.Database.ImportedDatabaseRoutine({Literal(schema.Name)},{Literal(routine.Name)},{Verbatim(routine.Definition!)})]"))).ToArray();
            if(definitions.Length>0){
                var builder=Header();
                foreach(var definition in definitions){builder.AppendLine(definition);}
                files.Add(new("Queries/SourceDefinitions.cs",builder.ToString()));
            }
            compatibility.AddRange(views.Issues);
            compatibility.AddRange(RoutineIssues(routines));
        }
        return new(catalog.Name,FileName(databaseName),fingerprint,files.AsReadOnly(),tables){Routines=routines.AsReadOnly(),RootNamespace=runtimeSql?typeNamespace:null,IsCollectible=collectible,Compatibility=compatibility.AsReadOnly(),Views=views?.Views??[]};
    }

    private static DatabaseCatalog CreateImportCatalog(DatabaseCatalog catalog){
        var schemas=catalog.Schemas.Select(schema=>new SchemaCatalog(schema.Name,schema.Tables.Select(table=>{
            if(table.PrimaryKey is not null){return table;}
            var name="__SeamlessImportRowId";while(table.Columns.Any(c=>c.Name==name)){name="_"+name;}
            var ordinal=0;if(table.Columns.Count>0){ordinal=table.Columns.Max(c=>c.Ordinal)+1;}
            var column=new ColumnCatalog(ordinal,name,new DatabaseType(DatabaseTypeKind.Int64,false),false,IsSynthetic:true);
            return new TableCatalog(table.Name,table.Columns.Append(column).ToArray(),new PrimaryKeyCatalog("__SeamlessImportKey",[new KeyColumnCatalog(name,false)]),table.UniqueKeys,table.ForeignKeys,table.CheckConstraints,table.Triggers){CreateDate=table.CreateDate};
        }).ToArray(),schema.Views,schema.Routines)).ToArray();
        return new(catalog.Name,schemas);
    }

    private static IReadOnlyList<SchemaName> CreateNames(DatabaseCatalog catalog,string databaseName) {
        var schemaScope = new CSharpIdentifierScope();
        schemaScope.Reserve(databaseName);
        var typeScope = new CSharpIdentifierScope();
        // 関数は静的クラス Functions.<スキーマ> に出し、結果の行の型は Functions に出すので、行の型の名前にスキーマ名を使わない。
        {
            var reservedSchemas=new CSharpIdentifierScope();reservedSchemas.Reserve(databaseName);
            foreach(var schema in catalog.Schemas.OrderBy(static value=>value.Name,StringComparer.Ordinal)){typeScope.Reserve(reservedSchemas.Get(schema.Name));}
        }
        var schemas = new List<SchemaName>(catalog.Schemas.Count);
        foreach(var schema in catalog.Schemas.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
            var schemaName = schemaScope.Get(schema.Name);
            var relationScope = new CSharpIdentifierScope();
            relationScope.Reserve(schemaName);
            var tables = new List<TableName>(schema.Tables.Count);
            foreach(var table in schema.Tables.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
                var tableName=relationScope.Get(table.Name);
                var typeName=typeScope.Get(table.Name,$"{schema.Name}.{table.Name}");
                var columnScope = new CSharpIdentifierScope();
                columnScope.Reserve(tableName);
                ReserveDeclaredMembers(columnScope);
                var columns = table.Columns
                    .OrderBy(static value=>value.Ordinal)
                    .Select(column=>new ColumnName(column,columnScope.Get(column.Name)))
                    .ToArray();
                tables.Add(new(table,tableName,typeName,columns));
            }
            var views=new List<ViewName>(schema.Views.Count);
            foreach(var view in schema.Views.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
                var viewName=relationScope.Get(view.Name);
                var typeName=typeScope.Get(view.Name,$"{schema.Name}.{view.Name}");
                var columnScope=new CSharpIdentifierScope();
                columnScope.Reserve(viewName);
                ReserveDeclaredMembers(columnScope);
                var columns=view.Columns
                    .OrderBy(static value=>value.Ordinal)
                    .Select(column=>new ColumnName(column,columnScope.Get(column.Name)))
                    .ToArray();
                views.Add(new(view,viewName,typeName,columns));
            }
            var routines=new List<RoutineName>(schema.Routines.Count);
            foreach(var routine in schema.Routines.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
                var routineName=relationScope.Get(routine.Name);
                var typeName=typeScope.Get(routine.Name,$"{schema.Name}.{routine.Name}");
                var columnScope=new CSharpIdentifierScope();
                columnScope.Reserve(routineName);
                var columns=routine.ResultColumns
                    .OrderBy(static value=>value.Ordinal)
                    .Select(column=>new ColumnName(column,columnScope.Get(column.Name)))
                    .ToArray();
                routines.Add(new(routine,routineName,typeName,columns));
            }
            schemas.Add(new(schema,schemaName,tables,views,routines));
        }
        return schemas;
    }

    private static string CreateDatabaseSource(
        string databaseName,string typeNamespace,
        string fingerprint,
        IReadOnlyList<SchemaName> schemas,bool executableViews,string sourceDatabaseName,List<GeneratedRoutineBinding> routines,bool runtimeSql,bool collectible) {
        var builder = Header();
        builder.AppendLine($"namespace {typeNamespace} {{");
        builder.AppendLine();
        if(collectible){builder.AppendLine("internal static class DatabaseGenerator{public const bool Collectible=true;}");}
        else{builder.AppendLine("internal static class DatabaseGenerator;");}
        builder.AppendLine();
        if(runtimeSql){
            builder.AppendLine($"public sealed partial class {databaseName}:global::Seamless.Database.Query.RuntimeSqlDatabaseContainer {{");
            builder.AppendLine($"    public {databaseName}(global::Seamless.Database.DatabaseContainerOptions? options=null):base(options??(global::Seamless.Database.DatabaseContainerOptions.InMemory with{{}}),{Literal(fingerprint)}) {{");
            builder.AppendLine("        try {");
        }else{
            builder.AppendLine($"public sealed partial class {databaseName}:global::Seamless.Database.DatabaseContainer {{");
            builder.AppendLine($"    public {databaseName}(global::Seamless.Database.DatabaseContainerOptions? options=null):base(options) {{");
        }
        foreach(var schema in schemas) {
            builder.AppendLine($"        this.{schema.Name}=new(this);");
        }
        if(runtimeSql){
            builder.AppendLine("            this.EnsureTablesCreated();");
            builder.AppendLine("        }catch(global::System.Exception error){");
            builder.AppendLine("            try{this.Dispose();}catch(global::System.Exception cleanup){throw new global::System.AggregateException(error,cleanup);}throw;");
            builder.AppendLine("        }");
        }
        builder.AppendLine("    }");
        builder.AppendLine();
        if(executableViews){builder.AppendLine("    public global::System.Func<string,global::System.Collections.Generic.IEnumerable<object?[]>>? SqlViewRows{get;set;}");}
        builder.AppendLine($"    public const string GeneratedSchemaFingerprint=\"{fingerprint}\";");
        foreach(var schema in schemas) {
            builder.AppendLine($"    public global::{typeNamespace}.Schemas.{schema.Name} {schema.Name} {{ get; }}");
        }
        builder.AppendLine("}");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine($"namespace {typeNamespace}.Schemas {{");
        foreach(var schema in schemas) {
            // [スキーマ/表] 表(とビューの写し)のプロパティは DB の生成器がこのクラスへ足すので partial にする。
            builder.AppendLine($"    public sealed partial class {schema.Name}(global::Seamless.Database.DatabaseContainer database):global::Seamless.Database.DatabaseSchema(database,\"{Escape(schema.Source.Name)}\") {{");
            if(executableViews){
                foreach(var view in schema.Views) {
                    builder.AppendLine($"        public global::System.Collections.Generic.IEnumerable<global::{typeNamespace}.Views.{schema.Name}.{view.Name}> {view.Name}=>global::Seamless.Database.Rdbms.GeneratedSqlViewReader.Read<global::{typeNamespace}.Views.{schema.Name}.{view.Name}>(((global::{typeNamespace}.{databaseName})database).SqlViewRows,\"{Escape("["+schema.Source.Name.Replace("]","]]")+"].["+view.Source.Name.Replace("]","]]")+"]")}\");");
                }
            }
            foreach(var routine in schema.Routines.Where(static routine=>routine.Source.Kind==RoutineKind.StoredProcedure)) {
                var start=builder.Length;
                AppendRoutineSource(builder,typeNamespace,routine);
                routines.Add(new(sourceDatabaseName,schema.Source.Name,routine.Source.Name,routine.Source.Kind,
                    $"{FileName(typeNamespace)}.Schemas.{FileName(schema.Name)}",FileName(routine.Name),
                    $"{FileName(databaseName)}.cs",builder.ToString(start,builder.Length-start),!HasRoutineImplementation(routine.Source)));
            }
            builder.AppendLine("    }");
        }
        builder.AppendLine("}");
        // [関数/形] 関数はスキーマの入口(I<スキーマ>Reader)への拡張メンバー。スキーマごとの静的クラス <名前空間>.Functions.<スキーマ> に置く。
        // 呼び出し側は r.<スキーマ>.<関数>(…)・tx.<スキーマ>.<関数>(…) と書く。
        var functionSchemas=schemas.Where(static schema=>schema.Routines.Any(static routine=>routine.Source.Kind!=RoutineKind.StoredProcedure)).ToArray();
        if(functionSchemas.Length>0){
            builder.AppendLine();
            builder.AppendLine($"namespace {typeNamespace}.Functions {{");
            foreach(var schema in functionSchemas){
                builder.AppendLine($"    public static partial class {schema.Name} {{");
                builder.AppendLine($"        extension(global::{typeNamespace}.{databaseName}.I{FileName(schema.Name)}Reader q) {{");
                foreach(var routine in schema.Routines.Where(static routine=>routine.Source.Kind!=RoutineKind.StoredProcedure)){
                    var start=builder.Length;
                    AppendRoutineSource(builder,typeNamespace,routine);
                    routines.Add(new(sourceDatabaseName,schema.Source.Name,routine.Source.Name,routine.Source.Kind,
                        $"{FileName(typeNamespace)}.Functions.{FileName(schema.Name)}",FileName(routine.Name),
                        $"{FileName(databaseName)}.cs",builder.ToString(start,builder.Length-start),!HasRoutineImplementation(routine.Source)));
                }
                builder.AppendLine("        }");
                builder.AppendLine("    }");
            }
            builder.AppendLine("}");
        }
        return builder.ToString();
    }

    // 行の型の生成器が作るメンバー。列の名前と重なると宣言のエラー(SDB003)になるので、列の名前を付け替える。
    private static void ReserveDeclaredMembers(CSharpIdentifierScope scope){
        foreach(var name in new[]{"Key","_Key","_C","_S","C","S","Create","__NormalizeScalars","__ReadColumns","Strategy","Equals","GetHashCode"}){scope.Reserve(name);}
    }

    private static string RoutineRowNamespace(RoutineName routine)=>routine.Source.Kind==RoutineKind.StoredProcedure?"Procedures":"Functions";

    private static string CreateTableSource(string databaseName,SchemaName schema,TableName table)=>
        CreateDeclaredTableSource(databaseName,schema,table.Name,table.Columns,table.Source.PrimaryKey is null?[]:TableKeyColumns(table),
            $"[global::Seamless.Database.ImportedDatabaseObject({Literal(schema.Source.Name)},{Literal(table.Source.Name)},{Literal(PreservedTableFeatures(table.Source))})]",true);

    // ビューの写し。キーはビューの一意キー(無ければすべての列)。
    private static string CreateTableSource(string databaseName,SchemaName schema,ViewName view)=>
        CreateDeclaredTableSource(databaseName,schema,view.Name,view.Columns,view.Source.UniqueKey is null?[]:ViewKeyColumns(view),null,false);

    private static string CreateDeclaredTableSource(string databaseName,SchemaName schema,string name,IReadOnlyList<ColumnName> columns,IReadOnlyList<ColumnName> keys,string? objectAttribute,bool importedColumns){
        var builder=Header();
        builder.AppendLine($"namespace {databaseName}.Tables.{schema.Name};");
        builder.AppendLine();
        if(objectAttribute is not null){builder.AppendLine(objectAttribute);}
        builder.AppendLine($"public partial struct {name} {{");
        foreach(var column in columns) {
            if(keys.Contains(column)){builder.AppendLine("    [global::Seamless.Database.Key]");}
            if(importedColumns){
                // 書き出しで元の型(decimal(p,s)・datetime2(n) など)に戻せるよう、精度と小数部の桁も残す。
                var precision=column.Source.Type.Precision is{} p?$",Precision={p}":"";
                var scale=column.Source.Type.Scale is{} s?$",Scale={s}":"";
                builder.AppendLine($"    [global::Seamless.Database.ImportedDatabaseColumn({Literal(column.Source.Name)},{column.Source.Type.MaximumLength??-1},{Literal(column.Source.Type.SqlTypeName)},{Literal(column.Source.Type.Collation)},IsSynthetic={column.Source.IsSynthetic.ToString().ToLowerInvariant()}{precision}{scale})]");
            }
            if(column.Source.ComputedDefinition is not null){builder.AppendLine($"    [global::Seamless.Database.ComputedColumn({column.Source.Ordinal})]");}
            builder.AppendLine($"    public partial {TypeName(column.Source.Type)} {column.Name} {{ get; }}");
        }
        builder.AppendLine("}");
        return builder.ToString();
    }

    // SQL Server へ直接問い合わせる実行用のビューの行。列の順に受け取るコンストラクターで作る(GeneratedSqlViewReader)。
    private static string CreateViewRowSource(string databaseName,SchemaName schema,ViewName view)=>
        CreateRowSource($"{databaseName}.Views.{schema.Name}",view.Name,view.Columns);

    private static string CreateRoutineRowSource(string databaseName,RoutineName routine)=>
        CreateRowSource($"{databaseName}.{RoutineRowNamespace(routine)}",routine.TypeName,routine.Columns);

    private static string CreateRowSource(string namespaceName,string name,IReadOnlyList<ColumnName> columns){
        var builder=Header();
        builder.AppendLine($"namespace {namespaceName};");
        builder.AppendLine();
        builder.AppendLine($"public readonly record struct {name}({string.Join(",",columns.Select(static column=>$"{TypeName(column.Source.Type)} {column.Name}"))});");
        return builder.ToString();
    }

    private static void AppendRoutineSource(StringBuilder builder,string databaseName,RoutineName routine) {
        var parameters=string.Join(",",routine.Source.Parameters.OrderBy(static value=>value.Ordinal)
            .Select(parameter=>$"{(parameter.IsOutput?"out ":string.Empty)}{TypeName(parameter.Type)} {ParameterName(parameter.Name)}"));
        var outputParameters=routine.Source.Parameters.Where(static parameter=>parameter.IsOutput).ToArray();
        switch(routine.Source.Kind) {
            // [関数/形] 関数は、生成される入口(I<スキーマ>Reader)への拡張メソッドとして書く(拡張ブロックの中に置く。CreateDatabaseSource)。
            case RoutineKind.TableFunction:
                if(routine.Columns.Count==0) {
                    throw new NotSupportedException($"Table function '{routine.Source.SchemaName}.{routine.Source.Name}' has no result columns.");
                }
                AppendFunctionMethod(builder,
                    $"        public global::Seamless.Collections.ISet<global::{databaseName}.{RoutineRowNamespace(routine)}.{routine.TypeName}> {routine.Name}({parameters})",
                    $"throw new global::System.NotSupportedException({Literal($"Imported SQL Server table function '{routine.Source.SchemaName}.{routine.Source.Name}' requires relational-algebra execution.")});",
                    outputParameters);
                break;
            case RoutineKind.ScalarFunction:
                var scalarType=routine.Source.ReturnType is null
                    ?"object?"
                    :TypeName(routine.Source.ReturnType);
                var scalarBody=$"throw new global::System.NotSupportedException({Literal($"Imported SQL Server scalar function '{routine.Source.SchemaName}.{routine.Source.Name}' requires relational-algebra execution.")});";
                if(HasRoutineImplementation(routine.Source)){scalarBody=routine.Source.Implementation!.CSharpBody;}
                AppendFunctionMethod(builder,
                    $"        public {scalarType} {routine.Name}({parameters})",
                    scalarBody,
                    outputParameters);
                break;
            case RoutineKind.StoredProcedure:
                var resultType=routine.Columns.Count==0
                    ?"object?"
                    :$"global::System.Collections.Generic.IReadOnlyList<global::{databaseName}.{RoutineRowNamespace(routine)}.{routine.TypeName}>";
                AppendRoutineMethod(builder,
                    $"        public global::Seamless.Database.StoredProcedure<{resultType}> {routine.Name}({parameters})",
                    $"this.StoredProcedure<{resultType}>(()=>throw new global::System.NotSupportedException({Literal($"Imported SQL Server stored procedure '{routine.Source.SchemaName}.{routine.Source.Name}' requires relational-algebra execution.")}))",
                    outputParameters);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static bool HasRoutineImplementation(RoutineCatalog routine){
        if(routine.Kind!=RoutineKind.ScalarFunction||routine.Definition is null||routine.Implementation is not{}implementation||implementation.CompilerProfile!="SqlServerScalarTinyintCaseNvarcharV1"||string.IsNullOrWhiteSpace(implementation.CSharpBody)){return false;}
        return string.Equals(implementation.SourceDefinitionSha256,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(routine.Definition))),StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendFunctionMethod(StringBuilder builder,string signature,string body,IReadOnlyList<ParameterCatalog> outputParameters){
        builder.AppendLine($"{signature} {{");
        foreach(var parameter in outputParameters) {
            builder.AppendLine($"            {ParameterName(parameter.Name)}=default!;");
        }
        builder.AppendLine($"            {body}");
        builder.AppendLine("        }");
    }

    private static void AppendRoutineMethod(
        StringBuilder builder,
        string signature,
        string expression,
        IReadOnlyList<ParameterCatalog> outputParameters) {
        if(outputParameters.Count==0) {
            builder.AppendLine($"{signature}=>{expression};");
            return;
        }
        builder.AppendLine($"{signature} {{");
        foreach(var parameter in outputParameters) {
            builder.AppendLine($"            {ParameterName(parameter.Name)}=default!;");
        }
        builder.AppendLine($"            return {expression};");
        builder.AppendLine("        }");
    }

    private static IReadOnlyList<ColumnName> TableKeyColumns(TableName table)=>table.Source.PrimaryKey is null
        ?table.Columns
        :table.Source.PrimaryKey.Columns
            .Select(key=>table.Columns.Single(column=>column.Source.Name==key.Name))
            .ToArray();

    private static IReadOnlyList<ColumnName> ViewKeyColumns(ViewName view)=>view.Source.UniqueKey is null
        ?view.Columns
        :view.Source.UniqueKey.Columns
            .Select(key=>view.Columns.Single(column=>column.Source.Name==key.Name))
            .ToArray();

    private static string CreateFingerprint(DatabaseCatalog catalog) {
        var canonical = new StringBuilder();
        Append(canonical,catalog.Name);
        if(catalog.DefaultCollation is not null){Append(canonical,"DatabaseCollation");Append(canonical,catalog.DefaultCollation);}
        foreach(var schema in catalog.Schemas.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
            Append(canonical,schema.Name);
            foreach(var table in schema.Tables.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
                Append(canonical,table.Name);
                foreach(var column in table.Columns.OrderBy(static value=>value.Ordinal)) {
                    Append(canonical,column.Ordinal.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
                    Append(canonical,column.Name);
                    AppendType(canonical,column.Type);
                    Append(canonical,column.IsIdentity.ToString());
                    if(column.IsIdentity){Append(canonical,column.IdentitySeed.ToString(global::System.Globalization.CultureInfo.InvariantCulture));Append(canonical,column.IdentityIncrement.ToString(global::System.Globalization.CultureInfo.InvariantCulture));}
                    Append(canonical,column.DefaultDefinition??string.Empty);
                    Append(canonical,column.ComputedDefinition??string.Empty);
                    if(column.ComputedDefinition is not null){Append(canonical,column.IsComputedPersisted.ToString());}
                }
                Append(canonical,table.PrimaryKey?.Name??string.Empty);
                Append(canonical,table.PrimaryKey?.IndexType?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty);
                if(table.PrimaryKey is not null) {
                    foreach(var column in table.PrimaryKey.Columns) {
                        Append(canonical,column.Name);
                        Append(canonical,column.IsDescending.ToString());
                    }
                }
                Append(canonical,"UniqueKeys");
                foreach(var key in table.UniqueKeys.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
                    AppendKey(canonical,key);
                }
                Append(canonical,"ForeignKeys");
                foreach(var key in table.ForeignKeys.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
                    Append(canonical,key.Name);
                    Append(canonical,key.ReferencedSchema);
                    Append(canonical,key.ReferencedTable);
                    Append(canonical,key.DeleteAction);
                    Append(canonical,key.UpdateAction);
                    foreach(var column in key.Columns) {
                        Append(canonical,column.Column);
                        Append(canonical,column.ReferencedColumn);
                    }
                }
                Append(canonical,"Checks");
                foreach(var constraint in table.CheckConstraints.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
                    Append(canonical,constraint.Name);
                    Append(canonical,constraint.Definition);
                    Append(canonical,constraint.IsDisabled.ToString());
                }
                Append(canonical,"Triggers");
                foreach(var trigger in table.Triggers.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
                    Append(canonical,trigger.Name);
                    Append(canonical,trigger.Definition);
                    Append(canonical,trigger.IsInsteadOf.ToString());
                    Append(canonical,trigger.IsDisabled.ToString());
                }
            }
            Append(canonical,"Views");
            foreach(var view in schema.Views.OrderBy(static value=>value.Name,StringComparer.Ordinal)) {
                Append(canonical,view.Name);
                foreach(var column in view.Columns.OrderBy(static value=>value.Ordinal)) {
                    Append(canonical,column.Ordinal.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
                    Append(canonical,column.Name);
                    AppendType(canonical,column.Type);
                }
                Append(canonical,view.UniqueKey?.Name??string.Empty);
                if(view.UniqueKey is not null) {
                    foreach(var column in view.UniqueKey.Columns) {
                        Append(canonical,column.Name);
                        Append(canonical,column.IsDescending.ToString());
                    }
                }
                Append(canonical,view.Definition??string.Empty);
                foreach(var dependency in (view.Dependencies??[]).OrderBy(static value=>value,StringComparer.Ordinal)) {
                    Append(canonical,dependency);
                }
            }
            Append(canonical,"Routines");
            foreach(var routine in schema.Routines.OrderBy(static value=>value.Name,StringComparer.Ordinal)
                .ThenBy(static value=>value.Kind)) {
                Append(canonical,routine.Name);
                Append(canonical,routine.Kind.ToString());
                Append(canonical,routine.SourceObjectType??string.Empty);
                Append(canonical,routine.Implementation?.CompilerProfile??string.Empty);
                Append(canonical,routine.Implementation?.SourceDefinitionSha256??string.Empty);
                Append(canonical,routine.Implementation?.CSharpBody??string.Empty);
                AppendType(canonical,routine.ReturnType);
                foreach(var parameter in routine.Parameters.OrderBy(static value=>value.Ordinal)) {
                    Append(canonical,parameter.Ordinal.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
                    Append(canonical,parameter.Name);
                    AppendType(canonical,parameter.Type);
                    Append(canonical,parameter.IsOutput.ToString());
                    Append(canonical,parameter.IsTableValued.ToString());
                }
                foreach(var column in routine.ResultColumns.OrderBy(static value=>value.Ordinal)) {
                    Append(canonical,column.Ordinal.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
                    Append(canonical,column.Name);
                    AppendType(canonical,column.Type);
                    Append(canonical,column.IsIdentity.ToString());
                }
                Append(canonical,routine.Definition??string.Empty);
                Append(canonical,routine.HasMutation.ToString());
                Append(canonical,routine.ResultSetCount.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
                Append(canonical,routine.ReturnCount.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
                if(routine.RelationalAlgebra is not null) {
                    foreach(var operation in routine.RelationalAlgebra.Operations) {
                        AppendRelationalAlgebra(canonical,operation);
                    }
                }
            }
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void AppendType(StringBuilder builder,DatabaseType? type) {
        if(type is null) {
            Append(builder,string.Empty);
            return;
        }
        Append(builder,type.Kind.ToString());
        Append(builder,type.IsNullable.ToString());
        Append(builder,type.MaximumLength?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty);
        Append(builder,type.Precision?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty);
        Append(builder,type.Scale?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty);
        Append(builder,type.RuntimeType?.FullName??string.Empty);
        Append(builder,type.SqlTypeName??string.Empty);
        Append(builder,type.Collation??string.Empty);
        Append(builder,type.SourceMaximumLength?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty);
        Append(builder,type.SourcePrecision?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty);
        Append(builder,type.SourceScale?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty);
        if(type.ResultSqlTypeName is not null||type.ResultMaximumLength is not null){Append(builder,"ResultSqlType");Append(builder,type.ResultSqlTypeName??string.Empty);Append(builder,type.ResultMaximumLength?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty);}
    }

    private static void AppendKey(StringBuilder builder,UniqueKeyCatalog key) {
        Append(builder,key.Name);
        foreach(var column in key.Columns) {
            Append(builder,column.Name);
            Append(builder,column.IsDescending.ToString());
        }
    }

    private static void AppendRelationalAlgebra(
        StringBuilder builder,
        RelationalAlgebraNode operation) {
        Append(builder,operation.Operator.ToString());
        Append(builder,operation.RelationName??string.Empty);
        foreach(var input in operation.Inputs) {
            AppendRelationalAlgebra(builder,input);
        }
    }

    private static void Append(StringBuilder builder,string value) {
        builder.Append(value.Length);
        builder.Append(':');
        builder.Append(value);
        builder.Append(';');
    }

    private static void Validate(DatabaseCatalog catalog) {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalog.Name);
        var schemaNames=catalog.Schemas.Select(static schema=>schema.Name).ToHashSet(StringComparer.Ordinal);
        if(schemaNames.Count!=catalog.Schemas.Count) {
            throw new ArgumentException("The catalog contains duplicate schema names.",nameof(catalog));
        }
        foreach(var schema in catalog.Schemas) {
            ArgumentException.ThrowIfNullOrWhiteSpace(schema.Name);
            var relationNames=schema.Tables.Select(static table=>table.Name)
                .Concat(schema.Views.Select(static view=>view.Name))
                .Concat(schema.Routines.Select(static routine=>routine.Name))
                .ToHashSet(StringComparer.Ordinal);
            if(relationNames.Count!=schema.Tables.Count+schema.Views.Count+schema.Routines.Count) {
                throw new ArgumentException($"Schema '{schema.Name}' has duplicate relation names.",nameof(catalog));
            }
            foreach(var table in schema.Tables) {
                ArgumentException.ThrowIfNullOrWhiteSpace(table.Name);
                if(table.Columns.Count==0) {
                    throw new ArgumentException($"Table '{schema.Name}.{table.Name}' has no columns.",nameof(catalog));
                }
                var names = table.Columns.Select(static column=>column.Name).ToHashSet(StringComparer.Ordinal);
                if(names.Count!=table.Columns.Count) {
                    throw new ArgumentException($"Table '{schema.Name}.{table.Name}' has duplicate columns.",nameof(catalog));
                }
                if(table.PrimaryKey is not null&&table.PrimaryKey.Columns.Any(column=>!names.Contains(column.Name))) {
                    throw new ArgumentException($"Table '{schema.Name}.{table.Name}' has an invalid primary key.",nameof(catalog));
                }
                foreach(var key in table.UniqueKeys) {
                    if(key.Columns.Count==0||key.Columns.Any(column=>!names.Contains(column.Name))) {
                        throw new ArgumentException($"Table '{schema.Name}.{table.Name}' has an invalid unique key '{key.Name}'.",nameof(catalog));
                    }
                }
                foreach(var foreignKey in table.ForeignKeys) {
                    if(foreignKey.Columns.Count==0
                        ||foreignKey.Columns.Any(column=>!names.Contains(column.Column))) {
                        throw new ArgumentException($"Table '{schema.Name}.{table.Name}' has an invalid foreign key '{foreignKey.Name}'.",nameof(catalog));
                    }
                    var referencedTable=catalog.Schemas
                        .SingleOrDefault(value=>value.Name==foreignKey.ReferencedSchema)?
                        .Tables.SingleOrDefault(value=>value.Name==foreignKey.ReferencedTable);
                    if(referencedTable is null
                        ||foreignKey.Columns.Any(column=>!referencedTable.Columns.Any(value=>value.Name==column.ReferencedColumn))) {
                        throw new ArgumentException($"Table '{schema.Name}.{table.Name}' has an unresolved foreign key '{foreignKey.Name}'.",nameof(catalog));
                    }
                }
                if(table.CheckConstraints.Any(static constraint=>string.IsNullOrWhiteSpace(constraint.Name)
                    ||string.IsNullOrWhiteSpace(constraint.Definition))) {
                    throw new ArgumentException($"Table '{schema.Name}.{table.Name}' has an invalid check constraint.",nameof(catalog));
                }
                if(table.Triggers.Any(static trigger=>string.IsNullOrWhiteSpace(trigger.Name))) {
                    throw new ArgumentException($"Table '{schema.Name}.{table.Name}' has an invalid trigger.",nameof(catalog));
                }
            }
            foreach(var view in schema.Views) {
                ArgumentException.ThrowIfNullOrWhiteSpace(view.Name);
                if(view.Columns.Count==0) {
                    throw new ArgumentException($"View '{schema.Name}.{view.Name}' has no columns.",nameof(catalog));
                }
                var names=view.Columns.Select(static column=>column.Name).ToHashSet(StringComparer.Ordinal);
                if(names.Count!=view.Columns.Count) {
                    throw new ArgumentException($"View '{schema.Name}.{view.Name}' has duplicate columns.",nameof(catalog));
                }
                if(view.UniqueKey is not null
                    &&(view.UniqueKey.Columns.Count==0||view.UniqueKey.Columns.Any(column=>!names.Contains(column.Name)))) {
                    throw new ArgumentException($"View '{schema.Name}.{view.Name}' has an invalid unique key.",nameof(catalog));
                }
            }
            foreach(var routine in schema.Routines) {
                ArgumentException.ThrowIfNullOrWhiteSpace(routine.Name);
                if(routine.Kind==RoutineKind.TableFunction&&routine.ResultColumns.Count==0) {
                    throw new ArgumentException($"Table function '{schema.Name}.{routine.Name}' has no result columns.",nameof(catalog));
                }
                var names=routine.ResultColumns.Select(static column=>column.Name).ToHashSet(StringComparer.Ordinal);
                if(names.Count!=routine.ResultColumns.Count) {
                    throw new ArgumentException($"Routine '{schema.Name}.{routine.Name}' has duplicate result columns.",nameof(catalog));
                }
            }
        }
    }

    private static StringBuilder Header() => new("// <auto-generated/>\n#nullable enable\n#pragma warning disable CS8981\n\n");
    private static string Escape(string value)=>value.Replace("\\","\\\\",StringComparison.Ordinal).Replace("\"","\\\"",StringComparison.Ordinal);
    private static string Literal(string?value){if(value is null){return "null";}return $"\"{Escape(value)}\"";}
    private static string FileName(string identifier)=>identifier.TrimStart('@');
    private static string ParameterName(string name) {
        var normalized=name.StartsWith('@')?name[1..]:name;
        return CSharpIdentifier.Normalize(normalized);
    }
    private static string TypeName(DatabaseType type) {
        var name=type.RuntimeType is null
            ?type.Kind switch {
            DatabaseTypeKind.Boolean=>"bool",
            DatabaseTypeKind.Byte=>"byte",
            DatabaseTypeKind.Int16=>"short",
            DatabaseTypeKind.Int32=>"int",
            DatabaseTypeKind.Int64=>"long",
            DatabaseTypeKind.Decimal=>"decimal",
            DatabaseTypeKind.Single=>"float",
            DatabaseTypeKind.Double=>"double",
            DatabaseTypeKind.String or DatabaseTypeKind.Xml=>"string",
            DatabaseTypeKind.Binary=>"byte[]",
            DatabaseTypeKind.Guid=>"global::System.Guid",
            DatabaseTypeKind.Date=>"global::System.DateOnly",
            DatabaseTypeKind.Time=>"global::System.TimeOnly",
            DatabaseTypeKind.DateTime=>"global::System.DateTime",
            DatabaseTypeKind.DateTimeOffset=>"global::System.DateTimeOffset",
            DatabaseTypeKind.Object=>"object",
                _=>throw new ArgumentOutOfRangeException(nameof(type)),
            }
            :RuntimeTypeName(type.RuntimeType);
        return type.IsNullable?$"{name}?":name;
    }

    private sealed record ColumnName(ColumnCatalog Source,string Name);
    private sealed record TableName(TableCatalog Source,string Name,string TypeName,IReadOnlyList<ColumnName> Columns);
    private sealed record ViewName(ViewCatalog Source,string Name,string TypeName,IReadOnlyList<ColumnName> Columns);
    private sealed record RoutineName(RoutineCatalog Source,string Name,string TypeName,IReadOnlyList<ColumnName> Columns);
    private sealed record SchemaName(
        SchemaCatalog Source,
        string Name,
        IReadOnlyList<TableName> Tables,
        IReadOnlyList<ViewName> Views,
        IReadOnlyList<RoutineName> Routines);
}
