using System.Reflection;
using System.Text.Json;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Transfer;

/// <summary>書き出す表1つ。<paramref name="ObjectName"/> はコンテナからたどる表の名前(「スキーマ.表」の C# の名前)。</summary>
public sealed record ExportTable(string ObjectName,string Schema,string Name,IReadOnlyList<ExportColumn> Columns){
    /// <summary>このままでは書き出せない(回避策を選ぶか、外す必要がある)。理由は報告の Issues にある。</summary>
    public bool Blocked{get;init;}
}

/// <summary>書き出す列1つ。行の型のプロパティと、書き出し先の列名。</summary>
public sealed record ExportColumn(string PropertyName,string Name);

/// <summary>取り込んだときの元のビュー・関数・手続きの定義(同じ製品へ書き出すとき、そのまま作り直す)。</summary>
public sealed record ExportSourceDefinition(string Schema,string Name,string Definition);

/// <summary>Seamless の DB の定義を、製品に依存しない目録に読み直した結果。</summary>
public sealed record SeamlessCatalog(DatabaseCatalog Catalog,IReadOnlyList<ExportTable> Tables,IReadOnlyList<ExportSourceDefinition> Views,IReadOnlyList<CompatibilityIssue> Issues){
    /// <summary>取り込んだ関数・手続きの元の定義。</summary>
    public IReadOnlyList<ExportSourceDefinition> Routines{get;init;}=[];
}

/// <summary>
/// 生成した DB のコンテナの型から、表・列・型・キー・制約を読み、書き出し用の目録(<see cref="DatabaseCatalog"/>)にする。
/// 取り込んだ表は、取り込みの生成器が残した元の型(<see cref="ImportedDatabaseColumnAttribute"/>)と制約(<see cref="ImportedDatabaseObjectAttribute"/>)から元の形に戻す。
/// C# で書いた表は、C# の型を SQL Server の型へ対応させる。同じにならない所は回避策つきで Issues に入れる。
/// 生成した DB は別の読み込み領域で読むことがあるので、属性は完全な名前で見分ける。
/// </summary>
public static class SeamlessCatalogReader{
    private const string ObjectAttribute="Seamless.Database.ImportedDatabaseObjectAttribute";
    private const string ColumnAttribute="Seamless.Database.ImportedDatabaseColumnAttribute";
    private const string ViewAttribute="Seamless.Database.ImportedDatabaseViewAttribute";
    private const string RoutineAttribute="Seamless.Database.ImportedDatabaseRoutineAttribute";
    private const string XmlSchemaCollectionAttribute="Seamless.Database.ImportedDatabaseXmlSchemaCollectionAttribute";
    private const string ForeignKeyAttributeName="Seamless.Database.ForeignKeyAttribute";

    /// <summary>コンテナの型から目録を読む。</summary>
    public static SeamlessCatalog Read(Type containerType){
        ArgumentNullException.ThrowIfNull(containerType);
        var declarations=DatabaseTableDeclaration.Read(containerType);
        var issues=new List<CompatibilityIssue>();
        var tables=new List<(string Schema,TableCatalog Table)>();
        var exports=new List<ExportTable>();
        var byPath=new Dictionary<string,(string Schema,string Name,IReadOnlyList<ExportColumn> Columns)>(StringComparer.Ordinal);
        var pending=new List<(DatabaseTableDeclaration Declaration,string Schema,string Name,IReadOnlyList<ExportColumn> Columns)>();
        foreach(var declaration in declarations){
            var arguments=declaration.Property.PropertyType.GetGenericArguments();
            var rowType=arguments[^1];
            var origin=rowType.GetCustomAttributesData().SingleOrDefault(static data=>data.AttributeType.FullName==ObjectAttribute);
            var (schema,name)=origin is not null
                ?((string)origin.ConstructorArguments[0].Value!,(string)origin.ConstructorArguments[1].Value!)
                :SchemaAndName(declaration.Path,issues);
            var features=origin?.ConstructorArguments.ElementAtOrDefault(2).Value as string;
            var blocked=false;
            var columns=new List<ColumnCatalog>();
            var exportColumns=new List<ExportColumn>();
            var shape=GeneratedRecordShape.TryCreate(rowType);
            // 取り込んだ表は、取り込みの印(ImportedDatabaseColumn)の付いたプロパティを宣言の順(= 元の列の順)に読む。
            // 計算列は行のコンストラクターで受け取らないので、行の型の形(shape)の列には入らない。
            var importedNames=rowType.GetProperties(BindingFlags.Public|BindingFlags.Instance)
                .Where(static property=>property.GetCustomAttributesData().Any(static data=>data.AttributeType.FullName==ColumnAttribute))
                .OrderBy(static property=>property.MetadataToken).Select(static property=>property.Name).ToArray();
            var names=origin is not null&&importedNames.Length>0?importedNames
                :shape?.Columns.ToArray()??rowType.GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(static property=>property.GetIndexParameters().Length==0).Select(static property=>property.Name).ToArray();
            var nullability=new NullabilityInfoContext();
            var ordinal=0;
            foreach(var propertyName in names){
                var property=rowType.GetProperty(propertyName,BindingFlags.Public|BindingFlags.Instance)??throw new InvalidDataException($"行の型 {rowType.FullName} に列 {propertyName} がありません。");
                var imported=property.GetCustomAttributesData().SingleOrDefault(static data=>data.AttributeType.FullName==ColumnAttribute);
                if(imported is not null&&Named<bool>(imported,"IsSynthetic")){continue;}
                var columnName=imported is null?propertyName:(string)imported.ConstructorArguments[0].Value!;
                var nullable=Nullable.GetUnderlyingType(property.PropertyType) is not null||!property.PropertyType.IsValueType&&nullability.Create(property).ReadState!=NullabilityState.NotNull;
                var type=imported is not null?ImportedType(imported,property.PropertyType,nullable):NativeType(property.PropertyType,nullable);
                if(type is null){
                    blocked=true;
                    issues.Add(new(CompatibilityObjectKind.Column,schema,name,columnName,"UNSUPPORTED_TYPE",
                        $"C# の型 {property.PropertyType.FullName} に対応する SQL Server の型が無い。",
                        [new("ExcludeTable","この表を書き出しから外す。"),new("ChangeType","列の型を、SQL Server に対応する型(数値・文字列・日時・バイト列など)に変える。")]));
                    continue;
                }
                if(imported is null&&type.Kind==DatabaseTypeKind.Decimal){
                    // C# の decimal は値ごとに桁と小数部の桁が変わる。SQL Server の decimal(p,s) は列で固定なので、黙って決めない。
                    issues.Add(new(CompatibilityObjectKind.Column,schema,name,columnName,"DECIMAL_SCALE",
                        "C# の decimal は値ごとに小数部の桁が変わるが、SQL Server の decimal は列ごとに桁(p,s)が固定。既定の decimal(38,10) では、小数部が10桁を超える値は丸められ、整数部が28桁を超える値は書き出せない。",
                        [new("AcceptDefault","decimal(38,10) で書き出す。"),new("DeclarePrecision","列に [ImportedDatabaseColumn(…,Precision=p,Scale=s)] を付けて桁を決める。")]));
                }
                columns.Add(new(ordinal++,columnName,type,false));
                exportColumns.Add(new(propertyName,columnName));
            }
            var featureList=ParseFeatures(features);
            columns=ApplyColumnFeatures(columns,featureList);
            // 主キー：取り込んだ表は元の主キー(残してある制約)に従う。生成器は主キーの無い表を「全部の列がキー」にするので、
            // それを主キーにしない。主キーを制約に残す前の生成物では、[Key] の付いた列(全部の列ではないとき)を主キーとみなす。
            // C# で書いた表は [Key] の列を主キーにする。
            var keyColumns=shape is{IsKeyed:true}?shape.KeyColumns.ToArray():[];
            var declaredKey=keyColumns.Length>0&&(origin is null||keyColumns.Length<shape!.Columns.Count())
                ?new PrimaryKeyCatalog("PK_"+name,keyColumns.Select(column=>new KeyColumnCatalog(exportColumns.Single(item=>item.PropertyName==column).Name,false)).ToArray())
                :null;
            var primaryKey=PrimaryKey(featureList)??declaredKey;
            var table=new TableCatalog(name,columns,primaryKey,UniqueKeys(featureList),ForeignKeys(featureList),Checks(featureList),Triggers(featureList));
            tables.Add((schema,table));
            exports.Add(new(declaration.Path,schema,name,exportColumns){Blocked=blocked});
            byPath[declaration.Path]=(schema,name,exportColumns);
            pending.Add((declaration,schema,name,exportColumns));
        }
        // C# で書いた外部キー([ForeignKey] を表のプロパティに付ける)を、相手の表の SQL の名前に直す。
        var native=new Dictionary<(string Schema,string Name),List<ForeignKeyCatalog>>();
        foreach(var (declaration,schema,name,columns) in pending){
            foreach(var data in declaration.Property.GetCustomAttributesData().Where(static data=>data.AttributeType.FullName==ForeignKeyAttributeName)){
                var fkName=(string)data.ConstructorArguments[0].Value!;
                var referenced=(string)data.ConstructorArguments[1].Value!;
                var childColumns=Strings(data.ConstructorArguments[2]);
                var parentColumns=Strings(data.ConstructorArguments[3]);
                if(!byPath.TryGetValue(referenced,out var parent)){throw new InvalidDataException($"外部キー {fkName} の相手の表 {referenced} がありません。");}
                var pairs=childColumns.Zip(parentColumns,(child,parentColumn)=>(columns.Single(column=>column.PropertyName==child).Name,parent.Columns.Single(column=>column.PropertyName==parentColumn).Name)).ToArray();
                if(!native.TryGetValue((schema,name),out var list)){list=[];native.Add((schema,name),list);}
                list.Add(new(fkName,parent.Schema,parent.Name,pairs,"NO_ACTION","NO_ACTION"));
            }
        }
        var schemas=tables.GroupBy(static item=>item.Schema,StringComparer.Ordinal).Select(group=>new SchemaCatalog(group.Key,group.Select(item=>{
            if(!native.TryGetValue((group.Key,item.Table.Name),out var foreignKeys)){return item.Table;}
            return new TableCatalog(item.Table.Name,item.Table.Columns,item.Table.PrimaryKey,item.Table.UniqueKeys,item.Table.ForeignKeys.Concat(foreignKeys).ToArray(),item.Table.CheckConstraints,item.Table.Triggers);
        }).ToArray())).ToArray();
        ExportSourceDefinition[] Definitions(string attribute)=>containerType.Assembly.GetCustomAttributesData().Where(data=>data.AttributeType.FullName==attribute)
            .Select(static data=>new ExportSourceDefinition((string)data.ConstructorArguments[0].Value!,(string)data.ConstructorArguments[1].Value!,(string)data.ConstructorArguments[2].Value!)).ToArray();
        var views=Definitions(ViewAttribute);
        // DB の既定の照合順序は生成物に残していない。列ごとの照合順序は元の型と一緒に戻す。
        var collections=Definitions(XmlSchemaCollectionAttribute).Select(static item=>new XmlSchemaCollectionCatalog(item.Schema,item.Name,item.Definition)).ToArray();
        return new(new DatabaseCatalog(containerType.Name,schemas){XmlSchemaCollections=collections},exports,views,issues){Routines=Definitions(RoutineAttribute)};
    }

    // C# で書いた表は、コンテナからたどる名前の最後が表、その前がスキーマ。スキーマの無い表は dbo。
    private static (string Schema,string Name) SchemaAndName(string path,List<CompatibilityIssue> issues){
        var parts=path.Split('.');
        if(parts.Length==1){return ("dbo",parts[0]);}
        if(parts.Length==2){return (parts[0],parts[1]);}
        var schema=string.Join("_",parts[..^1]);
        issues.Add(new(CompatibilityObjectKind.Table,schema,parts[^1],null,"NESTED_SCHEMA",
            $"入れ子のスキーマ {string.Join(".",parts[..^1])} は SQL Server に無いので、スキーマ名を {schema} にする。",[]));
        return (schema,parts[^1]);
    }

    private static T Named<T>(CustomAttributeData data,string name){
        foreach(var argument in data.NamedArguments){if(argument.MemberName==name){return (T)argument.TypedValue.Value!;}}
        return default!;
    }

    private static string[] Strings(CustomAttributeTypedArgument argument)=>((IReadOnlyCollection<CustomAttributeTypedArgument>)argument.Value!).Select(static item=>(string)item.Value!).ToArray();

    // 取り込んだ列は、元の SQL Server の型名・長さ・照合順序・精度・小数部の桁から戻す。
    private static DatabaseType ImportedType(CustomAttributeData imported,Type clrType,bool nullable){
        var maximumLength=(int)imported.ConstructorArguments[1].Value!;
        var sqlTypeName=(string?)imported.ConstructorArguments[2].Value;
        var collation=(string?)imported.ConstructorArguments[3].Value;
        // 精度と小数部の桁は、残してあるときだけ名前付きの引数にある(無ければ -1 のまま)。
        byte? precision=imported.NamedArguments.Any(static argument=>argument.MemberName=="Precision")?(byte)Named<int>(imported,"Precision"):null;
        byte? scale=imported.NamedArguments.Any(static argument=>argument.MemberName=="Scale")?(byte)Named<int>(imported,"Scale"):null;
        var kind=NativeType(clrType,nullable)?.Kind??DatabaseTypeKind.Binary;
        // 型付きの xml の列は、従う XML スキーマコレクションと、文書か断片かも戻す。
        return new(kind,nullable,maximumLength<0?null:maximumLength,precision,scale,null,sqlTypeName,collation){
            XmlSchemaCollectionSchema=Named<string?>(imported,"XmlSchemaCollectionSchema"),
            XmlSchemaCollectionName=Named<string?>(imported,"XmlSchemaCollectionName"),
            IsXmlDocument=Named<bool>(imported,"IsXmlDocument"),
        };
    }

    // C# の型から、値を変えずに持てる SQL Server の型にする。持てない型は null。
    private static DatabaseType? NativeType(Type type,bool nullable){
        type=Nullable.GetUnderlyingType(type)??type;
        if(type.IsEnum){type=Enum.GetUnderlyingType(type);}
        if(type.FullName=="Seamless.Database.SqlServer.Types.Bit"){return new(DatabaseTypeKind.Byte,nullable,SqlTypeName:"bit");}
        if(type.IsGenericType&&type.GetGenericTypeDefinition().FullName=="Seamless.Collections.FixedText`1"){
            // 固定容量の文字列。容量は TextBuffer<N> の N。
            var buffer=type.GetGenericArguments()[0].Name;
            var digits=new string(buffer.Where(char.IsDigit).ToArray());
            return new(DatabaseTypeKind.String,nullable,int.Parse(digits,global::System.Globalization.CultureInfo.InvariantCulture),SqlTypeName:"nvarchar");
        }
        return Type.GetTypeCode(type) switch{
            TypeCode.Boolean=>new(DatabaseTypeKind.Boolean,nullable,SqlTypeName:"bit"),
            TypeCode.Byte=>new(DatabaseTypeKind.Byte,nullable,SqlTypeName:"tinyint"),
            TypeCode.SByte=>new(DatabaseTypeKind.Int16,nullable,SqlTypeName:"smallint"),
            TypeCode.Int16=>new(DatabaseTypeKind.Int16,nullable,SqlTypeName:"smallint"),
            TypeCode.UInt16=>new(DatabaseTypeKind.Int32,nullable,SqlTypeName:"int"),
            TypeCode.Int32=>new(DatabaseTypeKind.Int32,nullable,SqlTypeName:"int"),
            TypeCode.UInt32=>new(DatabaseTypeKind.Int64,nullable,SqlTypeName:"bigint"),
            TypeCode.Int64=>new(DatabaseTypeKind.Int64,nullable,SqlTypeName:"bigint"),
            TypeCode.UInt64=>new(DatabaseTypeKind.Decimal,nullable,Precision:20,Scale:0,SqlTypeName:"decimal"),
            TypeCode.Single=>new(DatabaseTypeKind.Single,nullable,SqlTypeName:"real"),
            TypeCode.Double=>new(DatabaseTypeKind.Double,nullable,SqlTypeName:"float"),
            TypeCode.Decimal=>new(DatabaseTypeKind.Decimal,nullable,Precision:38,Scale:10,SqlTypeName:"decimal"),
            TypeCode.String=>new(DatabaseTypeKind.String,nullable,SqlTypeName:"nvarchar"),
            TypeCode.Char=>new(DatabaseTypeKind.String,nullable,1,SqlTypeName:"nchar"),
            TypeCode.DateTime=>new(DatabaseTypeKind.DateTime,nullable,Scale:7,SqlTypeName:"datetime2"),
            _ when type==typeof(Guid)=>new(DatabaseTypeKind.Guid,nullable,SqlTypeName:"uniqueidentifier"),
            _ when type==typeof(byte[])=>new(DatabaseTypeKind.Binary,nullable,SqlTypeName:"varbinary"),
            _ when type==typeof(DateOnly)=>new(DatabaseTypeKind.Date,nullable,SqlTypeName:"date"),
            _ when type==typeof(TimeOnly)=>new(DatabaseTypeKind.Time,nullable,Scale:7,SqlTypeName:"time"),
            _ when type==typeof(DateTimeOffset)=>new(DatabaseTypeKind.DateTimeOffset,nullable,Scale:7,SqlTypeName:"datetimeoffset"),
            _ when type==typeof(TimeSpan)=>null,
            _=>null,
        };
    }

    private static IReadOnlyList<JsonElement> ParseFeatures(string? features){
        if(features is null){return [];}
        using var document=JsonDocument.Parse(features);
        return document.RootElement.EnumerateArray().Select(static element=>element.Clone()).ToArray();
    }

    private static IEnumerable<JsonElement> Kind(IReadOnlyList<JsonElement> features,string kind)=>features.Where(feature=>feature.GetProperty("Kind").GetString()==kind);

    private static string? String(JsonElement element,string name)=>element.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;
    private static bool Boolean(JsonElement element,string name)=>element.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.True;

    // IDENTITY・既定値・計算列は列に付ける。
    private static List<ColumnCatalog> ApplyColumnFeatures(List<ColumnCatalog> columns,IReadOnlyList<JsonElement> features){
        return columns.Select(column=>{
            var result=column;
            foreach(var feature in features){
                if(String(feature,"Name")!=column.Name){continue;}
                switch(feature.GetProperty("Kind").GetString()){
                    case "Identity":
                        result=result with{IsIdentity=true,IdentitySeed=feature.GetProperty("IdentitySeed").GetDecimal(),IdentityIncrement=feature.GetProperty("IdentityIncrement").GetDecimal()};
                        break;
                    case "Default":
                        result=result with{DefaultDefinition=String(feature,"DefaultDefinition"),DefaultConstraintName=String(feature,"DefaultConstraintName")};
                        break;
                    case "Computed":
                        result=result with{ComputedDefinition=String(feature,"ComputedDefinition"),IsComputedPersisted=Boolean(feature,"IsComputedPersisted")};
                        break;
                }
            }
            return result;
        }).ToList();
    }

    private static PrimaryKeyCatalog? PrimaryKey(IReadOnlyList<JsonElement> features){
        foreach(var feature in Kind(features,"PrimaryKey")){
            var definition=feature.GetProperty("Definition");
            byte? indexType=definition.TryGetProperty("IndexType",out var index)&&index.ValueKind==JsonValueKind.Number?index.GetByte():null;
            return new(String(definition,"Name")!,KeyColumns(definition),indexType){IsSystemNamed=Boolean(definition,"IsSystemNamed")};
        }
        return null;
    }

    private static IReadOnlyList<KeyColumnCatalog> KeyColumns(JsonElement definition)=>
        definition.GetProperty("Columns").EnumerateArray().Select(static column=>new KeyColumnCatalog(String(column,"Name")!,Boolean(column,"IsDescending"))).ToArray();

    private static IReadOnlyList<UniqueKeyCatalog> UniqueKeys(IReadOnlyList<JsonElement> features)=>
        Kind(features,"UniqueKey").Select(static feature=>feature.GetProperty("Definition")).Select(definition=>new UniqueKeyCatalog(String(definition,"Name")!,KeyColumns(definition)){IsSystemNamed=Boolean(definition,"IsSystemNamed")}).ToArray();

    private static IReadOnlyList<ForeignKeyCatalog> ForeignKeys(IReadOnlyList<JsonElement> features)=>
        Kind(features,"ForeignKey").Select(static feature=>feature.GetProperty("Definition")).Select(definition=>new ForeignKeyCatalog(
            String(definition,"Name")!,String(definition,"ReferencedSchema")!,String(definition,"ReferencedTable")!,
            definition.GetProperty("Columns").EnumerateArray().Select(static pair=>(String(pair,"Column")!,String(pair,"ReferencedColumn")!)).ToArray(),
            String(definition,"DeleteAction")!,String(definition,"UpdateAction")!){IsSystemNamed=Boolean(definition,"IsSystemNamed")}).ToArray();

    private static IReadOnlyList<CheckConstraintCatalog> Checks(IReadOnlyList<JsonElement> features)=>
        Kind(features,"Check").Select(static feature=>feature.GetProperty("Definition")).Select(definition=>new CheckConstraintCatalog(String(definition,"Name")!,String(definition,"Definition")!,Boolean(definition,"IsDisabled")){IsSystemNamed=Boolean(definition,"IsSystemNamed"),ParentColumn=String(definition,"ParentColumn")}).ToArray();

    private static IReadOnlyList<TriggerCatalog> Triggers(IReadOnlyList<JsonElement> features)=>
        Kind(features,"Trigger").Select(static feature=>feature.GetProperty("Definition")).Select(definition=>new TriggerCatalog(String(definition,"Name")!,String(definition,"Definition")!,Boolean(definition,"IsInsteadOf"),Boolean(definition,"IsDisabled"))).ToArray();
}
