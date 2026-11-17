using Seamless.Collections;

namespace Seamless.Database.Rdbms;

public static partial class CSharpDatabaseGenerator{
    private static string?PreservedTableFeatures(TableCatalog table){
        var features=new List<object>();
        foreach(var column in table.Columns){
            if(column.IsIdentity){features.Add(new{Kind="Identity",column.Name,column.IdentitySeed,column.IdentityIncrement,column.InitialIdentityValue});}
            if(column.DefaultDefinition is not null){features.Add(new{Kind="Default",column.Name,column.DefaultDefinition,column.DefaultConstraintName});}
            if(column.ComputedDefinition is not null){features.Add(new{Kind="Computed",column.Name,column.ComputedDefinition,column.IsComputedPersisted});}
        }
        // 主キーの名前・列の順と向き・索引の種類も、書き出しで元の形に戻すために残す。
        if(table.PrimaryKey is{} primaryKey&&!primaryKey.Columns.Any(key=>table.Columns.Any(column=>column.Name==key.Name&&column.IsSynthetic))){features.Add(new{Kind="PrimaryKey",Definition=primaryKey});}
        foreach(var key in table.UniqueKeys){features.Add(new{Kind="UniqueKey",Definition=key});}
        // 外部キーの列の組はタプルで持つが、JSON はタプルの中身(フィールド)を書かず [{}] になるので、名前付きで書く。
        foreach(var key in table.ForeignKeys){features.Add(new{Kind="ForeignKey",Definition=new{key.Name,key.ReferencedSchema,key.ReferencedTable,Columns=key.Columns.Select(static pair=>new{pair.Column,pair.ReferencedColumn}).ToArray(),key.DeleteAction,key.UpdateAction,key.IsSystemNamed}});}
        foreach(var check in table.CheckConstraints){features.Add(new{Kind="Check",Definition=check});}
        foreach(var trigger in table.Triggers){features.Add(new{Kind="Trigger",Definition=trigger});}
        if(features.Count==0){return null;}
        return global::System.Text.Json.JsonSerializer.Serialize(features);
    }

    /// <summary>新世代向けのネイティブ保存型を選択する。既存世代の保存形式は変更しない。</summary>
    public static GeneratedDatabase GenerateNative(DatabaseCatalog catalog,NativeGenerationOptions?options=null){
        ArgumentNullException.ThrowIfNull(catalog);options??=new();
        if(options.InlineTextByteLimit<0||options.InlineTextByteLimit>512||options.InlineRecordByteLimit<0){throw new ArgumentOutOfRangeException(nameof(options));}
        var schemas=catalog.Schemas.Select(schema=>new SchemaCatalog(schema.Name,schema.Tables.Select(table=>{
            long bytes=table.Columns.Sum(c=>NativeSize(c.Type));
            var columns=table.Columns.Select(column=>{
                var type=column.Type;
                if(type.Kind!=DatabaseTypeKind.String||type.RuntimeType is not null||type.MaximumLength is not >0){return column;}
                var capacity=Buffer(type.MaximumLength.Value);
                if(capacity is null){return column;}
                var bufferLength=capacity.Value.Length;
                var size=bufferLength*2+4;
                if(type.IsNullable){size+=4;}
                var next=bytes-IntPtr.Size+size;
                if(bufferLength*2>options.InlineTextByteLimit||next>options.InlineRecordByteLimit){return column;}
                bytes=next;
                return column with{Type=type with{RuntimeType=capacity.Value.Type}};
            }).ToArray();
            return new TableCatalog(table.Name,columns,table.PrimaryKey,table.UniqueKeys,table.ForeignKeys,table.CheckConstraints,table.Triggers){CreateDate=table.CreateDate};
        }).ToArray(),schema.Views,schema.Routines)).ToArray();
        var native=new DatabaseCatalog(catalog.Name,schemas,catalog.DefaultCollation);
        return Generate(native,preserveSqlRows:true,executableViews:false,runtimeSql:false,collectible:false);
    }

    private static (int Length,Type Type)?Buffer(int length){
        if(length<=16){return(16,typeof(FixedText<TextBuffer16>));}
        if(length<=32){return(32,typeof(FixedText<TextBuffer32>));}
        if(length<=64){return(64,typeof(FixedText<TextBuffer64>));}
        if(length<=128){return(128,typeof(FixedText<TextBuffer128>));}
        if(length<=256){return(256,typeof(FixedText<TextBuffer256>));}
        return null;
    }

    private static long NativeSize(DatabaseType type){
        if(type.Kind is DatabaseTypeKind.String or DatabaseTypeKind.Xml or DatabaseTypeKind.Binary or DatabaseTypeKind.Object){return IntPtr.Size;}
        return 16;
    }

    private static string RuntimeTypeName(Type type){
        if(!type.IsGenericType){return "global::"+type.FullName!.Replace('+','.');}
        var name=type.GetGenericTypeDefinition().FullName!;name=name[..name.IndexOf('`')].Replace('+','.');
        return "global::"+name+"<"+string.Join(",",type.GetGenericArguments().Select(RuntimeTypeName))+">";
    }
}
