using System.Collections;
using System.IO;
using System.Data;
using System.Data.Common;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Seamless.Database.Transfer;

namespace Seamless.Database.SqlServer;

/// <summary>
/// SQL Server への書き出しの指定。<see cref="DatabaseName"/> の DB に書き出す(元と違う名前にできる)。
/// 同じ名前の DB があるときは、<see cref="ReplaceExisting"/> が true のときだけ消して作り直す。
/// <see cref="BackupFile"/> を指定すると、消す前にその場所(SQL Server の機械の上のパス)へバックアップを取る。
/// </summary>
public sealed record SqlServerExportOptions(string ConnectionString,string DatabaseName){
    public bool ReplaceExisting{get;init;}
    public string? BackupFile{get;init;}
    /// <summary>書き出さない表(「スキーマ.表」、SQL の名前)。書き出せない表や、利用者が外すと選んだ表。</summary>
    public IReadOnlyCollection<string> ExcludedTables{get;init;}=[];
    /// <summary>作らないビュー(「スキーマ.ビュー」)。</summary>
    public IReadOnlyCollection<string> ExcludedViews{get;init;}=[];
    /// <summary>新しい DB のファイル(データと記録)を置くフォルダー(SQL Server の機械の上のパス)。無ければ SQL Server の既定の場所。</summary>
    public string? DataDirectory{get;init;}
}

/// <summary>書き出しの結果。表ごとの行数と、作れなかったビュー・トリガーと理由。</summary>
public sealed record SqlServerExportResult(string DatabaseName,IReadOnlyDictionary<string,long> Rows,IReadOnlyList<CompatibilityIssue> Issues,string? Backup);

/// <summary>
/// Seamless の DB を SQL Server へ書き出す。DB を用意し(無ければ作る・あれば確認の上で作り直す)、表と制約を作り、
/// 行を一括で入れ(SqlBulkCopy、IDENTITY の値は保つ)、件数を照合し、外部キー・CHECK・ビュー・トリガーを作る。
/// </summary>
public static class SqlServerDatabaseExporter{
    /// <summary>書き出す。<paramref name="database"/> はコンテナ(生成した DB)。</summary>
    public static async Task<SqlServerExportResult> ExportAsync(DatabaseContainer database,SeamlessCatalog catalog,SqlServerExportOptions options,IProgress<GeneratedTableLoadProgress>? progress=null,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(database);ArgumentNullException.ThrowIfNull(catalog);ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DatabaseName);
        var excludedTables=new HashSet<string>(options.ExcludedTables,StringComparer.OrdinalIgnoreCase);
        var blocked=catalog.Tables.Where(table=>table.Blocked&&!excludedTables.Contains(table.Schema+"."+table.Name)).Select(static table=>table.Schema+"."+table.Name).ToArray();
        if(blocked.Length>0){throw new InvalidOperationException("書き出せない表があります。回避策を選ぶか、外してください: "+string.Join(", ",blocked));}
        var tables=catalog.Tables.Where(table=>!excludedTables.Contains(table.Schema+"."+table.Name)).ToArray();
        var target=Filter(catalog.Catalog,excludedTables);
        var script=SqlServerSchemaScript.Create(target);
        var backup=await PrepareDatabaseAsync(options,cancellationToken).ConfigureAwait(false);
        var builder=new SqlConnectionStringBuilder(options.ConnectionString){InitialCatalog=options.DatabaseName};
        await using var connection=new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        foreach(var statement in script.Schemas){await ExecuteAsync(connection,statement,cancellationToken).ConfigureAwait(false);}
        foreach(var statement in script.XmlSchemaCollections){await ExecuteAsync(connection,statement,cancellationToken).ConfigureAwait(false);}
        // 関数を表より先に作る(計算列・既定値・CHECK が使う)。表を使う関数はここでは作れないので、残りは後で作る。
        var excludedViews=new HashSet<string>(options.ExcludedViews,StringComparer.OrdinalIgnoreCase);
        var objects=catalog.Routines.Select(static routine=>(Definition:routine,Kind:RoutineKind(routine.Definition)))
            .Concat(catalog.Views.Where(view=>!excludedViews.Contains(view.Schema+"."+view.Name)).Select(static view=>(Definition:view,Kind:CompatibilityObjectKind.View))).ToList();
        var failures=new Dictionary<ExportSourceDefinition,string>();
        var remaining=await CreateObjectsAsync(connection,objects.Where(static item=>item.Kind!=CompatibilityObjectKind.View).ToList(),failures,cancellationToken).ConfigureAwait(false);
        foreach(var statement in script.Tables){await ExecuteAsync(connection,statement,cancellationToken).ConfigureAwait(false);}
        // 行は DB の読み取りの入口(BeginRead)から、同じ時点の中身を読む。
        var rows=new Dictionary<string,long>(StringComparer.Ordinal);
        var read=database.GetType().GetMethod("BeginRead",[typeof(CancellationToken)])??throw new InvalidOperationException("DB に読み取りの入口(BeginRead)がありません。");
        using(var reader=(IDisposable)read.Invoke(database,[cancellationToken])!){
            foreach(var table in tables){
                cancellationToken.ThrowIfCancellationRequested();
                var catalogTable=target.Schemas.Single(schema=>schema.Name==table.Schema).Tables.Single(item=>item.Name==table.Name);
                var source=(IEnumerable)Path(reader,table.ObjectName);
                // 計算列と rowversion は SQL Server が値を作るので入れない。
                var columns=table.Columns.Where(column=>catalogTable.Columns.Single(item=>item.Name==column.Name) is{ComputedDefinition:null} item&&item.Type.SqlTypeName is not ("timestamp" or "rowversion")).ToArray();
                using var bulk=new SqlBulkCopy(connection,SqlBulkCopyOptions.KeepIdentity|SqlBulkCopyOptions.KeepNulls|SqlBulkCopyOptions.TableLock,null){
                    DestinationTableName=SqlServerSchemaScript.Quote(table.Schema)+"."+SqlServerSchemaScript.Quote(table.Name),
                    BulkCopyTimeout=0,
                    BatchSize=10_000,
                };
                foreach(var column in columns){bulk.ColumnMappings.Add(column.Name,column.Name);}
                using var data=new RowReader(source,columns,progress,table.Schema+"."+table.Name);
                await bulk.WriteToServerAsync(data,cancellationToken).ConfigureAwait(false);
                rows[table.Schema+"."+table.Name]=data.Count;
                progress?.Report(new(table.Schema+"."+table.Name,data.Count,true));
            }
        }
        // 件数を照合する。
        foreach(var (name,count) in rows){
            var parts=name.Split('.',2);
            await using var command=new SqlCommand($"SELECT COUNT_BIG(*) FROM {SqlServerSchemaScript.Quote(parts[0])}.{SqlServerSchemaScript.Quote(parts[1])};",connection);
            var actual=(long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
            if(actual!=count){throw new InvalidDataException($"表 {name} の件数が合いません。書いた {count} 件、SQL Server {actual} 件。");}
        }
        foreach(var statement in script.Constraints){await ExecuteAsync(connection,statement,cancellationToken).ConfigureAwait(false);}
        // 残りの関数・手続きとビューを作る(互いに使い合うので、作れたものがある限り繰り返す)。
        remaining=await CreateObjectsAsync(connection,remaining.Concat(objects.Where(static item=>item.Kind==CompatibilityObjectKind.View)).ToList(),failures,cancellationToken).ConfigureAwait(false);
        // 最後まで作れなかったものは、理由を報告に入れる(書き出しそのものは止めない。表と行はそろっている)。
        var issues=remaining.Select(item=>new CompatibilityIssue(item.Kind,item.Definition.Schema,item.Definition.Name,null,"OBJECT_CREATE_FAILED",$"元の定義で作れなかった: {failures[item.Definition]}",
            [new("FixDefinition","SQL Server で定義を直して作る。")])).ToList();
        foreach(var statement in script.Triggers){await ExecuteAsync(connection,statement,cancellationToken).ConfigureAwait(false);}
        return new(options.DatabaseName,rows,issues,backup);
    }

    // 書き出さない表を目録から外す。外した表を参照する外部キーは作れないので、それも外す。
    private static DatabaseCatalog Filter(DatabaseCatalog catalog,HashSet<string> excluded){
        var schemas=catalog.Schemas.Select(schema=>new SchemaCatalog(schema.Name,schema.Tables.Where(table=>!excluded.Contains(schema.Name+"."+table.Name)).Select(table=>
            new TableCatalog(table.Name,table.Columns,table.PrimaryKey,table.UniqueKeys,table.ForeignKeys.Where(key=>!excluded.Contains(key.ReferencedSchema+"."+key.ReferencedTable)).ToArray(),table.CheckConstraints,table.Triggers)).ToArray()))
            .Where(static schema=>schema.Tables.Count>0).ToArray();
        return new(catalog.Name,schemas,catalog.DefaultCollation){XmlSchemaCollections=catalog.XmlSchemaCollections};
    }

    // DB を用意する。無ければ作る。あれば、作り直す指定のときだけ、(指定があれば)バックアップを取ってから消して作る。
    private static async Task<string?> PrepareDatabaseAsync(SqlServerExportOptions options,CancellationToken cancellationToken){
        var builder=new SqlConnectionStringBuilder(options.ConnectionString){InitialCatalog="master"};
        await using var master=new SqlConnection(builder.ConnectionString);
        await master.OpenAsync(cancellationToken).ConfigureAwait(false);
        var name=SqlServerSchemaScript.Quote(options.DatabaseName);
        await using(var exists=new SqlCommand("SELECT DB_ID(@name);",master)){
            exists.Parameters.AddWithValue("@name",options.DatabaseName);
            var id=await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            string? backup=null;
            if(id is not DBNull and not null){
                if(!options.ReplaceExisting){throw new InvalidOperationException($"SQL Server に DB '{options.DatabaseName}' が既にあります。置き換えるか、別の名前を指定してください。");}
                if(options.BackupFile is{} file){
                    await ExecuteAsync(master,$"BACKUP DATABASE {name} TO DISK=N'{file.Replace("'","''",StringComparison.Ordinal)}' WITH COPY_ONLY,INIT;",cancellationToken).ConfigureAwait(false);
                    backup=file;
                }
                await ExecuteAsync(master,$"ALTER DATABASE {name} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {name};",cancellationToken).ConfigureAwait(false);
            }
            var create=$"CREATE DATABASE {name}";
            if(options.DataDirectory is{} directory){
                static string Literal(string text)=>"N'"+text.Replace("'","''",StringComparison.Ordinal)+"'";
                var data=global::System.IO.Path.Combine(directory,options.DatabaseName+".mdf");var log=global::System.IO.Path.Combine(directory,options.DatabaseName+"_log.ldf");
                create+=$" ON PRIMARY (NAME={Literal(options.DatabaseName+"_data")},FILENAME={Literal(data)}) LOG ON (NAME={Literal(options.DatabaseName+"_log")},FILENAME={Literal(log)})";
            }
            await ExecuteAsync(master,create+";",cancellationToken).ConfigureAwait(false);
            return backup;
        }
    }

    // 元の定義のまま作る。ほかのオブジェクトを使うものがあるので、作れたものがある限り、残りを繰り返し試す。作れなかったものを返す。
    private static async Task<List<(ExportSourceDefinition Definition,CompatibilityObjectKind Kind)>> CreateObjectsAsync(SqlConnection connection,List<(ExportSourceDefinition Definition,CompatibilityObjectKind Kind)> objects,Dictionary<ExportSourceDefinition,string> failures,CancellationToken cancellationToken){
        var remaining=objects;
        while(remaining.Count>0){
            var failed=new List<(ExportSourceDefinition Definition,CompatibilityObjectKind Kind)>();
            foreach(var item in remaining){
                try{await ExecuteAsync(connection,item.Definition.Definition,cancellationToken).ConfigureAwait(false);failures.Remove(item.Definition);}
                catch(SqlException error){failed.Add(item);failures[item.Definition]=error.Message;}
            }
            if(failed.Count==remaining.Count){return failed;}
            remaining=failed;
        }
        return remaining;
    }

    // 関数・手続きの種類を、元の定義の文から見分ける(報告の分類に使う)。
    private static CompatibilityObjectKind RoutineKind(string definition){
        if(global::System.Text.RegularExpressions.Regex.IsMatch(definition,@"\bCREATE\s+(OR\s+ALTER\s+)?PROC(EDURE)?\b",global::System.Text.RegularExpressions.RegexOptions.IgnoreCase)){return CompatibilityObjectKind.StoredProcedure;}
        if(global::System.Text.RegularExpressions.Regex.IsMatch(definition,@"\bRETURNS\s+(@\w+\s+)?TABLE\b",global::System.Text.RegularExpressions.RegexOptions.IgnoreCase)){return CompatibilityObjectKind.TableFunction;}
        return CompatibilityObjectKind.ScalarFunction;
    }

    private static object Path(object root,string path){
        var current=root;
        foreach(var segment in path.Split('.')){
            var property=current.GetType().GetProperty(segment,BindingFlags.Public|BindingFlags.Instance)??throw new MissingMemberException(current.GetType().FullName,segment);
            current=property.GetValue(current)??throw new InvalidDataException($"表 '{path}' をたどる途中の '{segment}' が null です。");
        }
        return current;
    }

    private static async Task ExecuteAsync(SqlConnection connection,string sql,CancellationToken cancellationToken){
        await using var command=new SqlCommand(sql,connection){CommandTimeout=0};
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // 行の型の値を、SqlBulkCopy が読める値(SQL の型に合う .NET の型)にして渡す。
    private sealed class RowReader(IEnumerable source,IReadOnlyList<ExportColumn> columns,IProgress<GeneratedTableLoadProgress>? progress,string name):DbDataReader{
        private readonly IEnumerator rows=source.GetEnumerator();
        private readonly object?[] values=new object?[columns.Count];
        private PropertyInfo[]? properties;
        public long Count{get;private set;}
        public override bool Read(){
            if(!this.rows.MoveNext()){return false;}
            var row=this.rows.Current!;
            this.properties??=columns.Select(column=>row.GetType().GetProperty(column.PropertyName,BindingFlags.Public|BindingFlags.Instance)!).ToArray();
            for(var index=0;index<this.values.Length;index++){this.values[index]=Value(this.properties[index].GetValue(row));}
            this.Count++;
            if(progress is not null&&this.Count%10_000==0){progress.Report(new(name,this.Count,false));}
            return true;
        }
        private static object? Value(object? value)=>value switch{
            null=>DBNull.Value,
            DateOnly date=>date.ToDateTime(TimeOnly.MinValue),
            TimeOnly time=>time.ToTimeSpan(),
            ulong number=>(decimal)number,
            uint number=>(long)number,
            ushort number=>(int)number,
            sbyte number=>(short)number,
            char character=>character.ToString(),
            Enum item=>Convert.ChangeType(item,Enum.GetUnderlyingType(item.GetType()),global::System.Globalization.CultureInfo.InvariantCulture),
            // bit(Seamless.Database.SqlServer.Types.Bit)と固定容量の文字列は、SQL の値へ直す。
            Types.Bit bit=>(bool)bit,
            global::Seamless.Collections.IFixedText text=>text.ToString(),
            _=>value,
        };
        public override int FieldCount=>columns.Count;
        public override object GetValue(int ordinal)=>this.values[ordinal]!;
        public override string GetName(int ordinal)=>columns[ordinal].Name;
        public override int GetOrdinal(string name){for(var index=0;index<columns.Count;index++){if(columns[index].Name==name){return index;}}throw new IndexOutOfRangeException(name);}
        public override bool IsDBNull(int ordinal)=>this.values[ordinal] is DBNull;
        public override int GetValues(object[] values){Array.Copy(this.values,values,this.values.Length);return this.values.Length;}
        public override object this[int ordinal]=>this.GetValue(ordinal);
        public override object this[string name]=>this.GetValue(this.GetOrdinal(name));
        public override bool HasRows=>true;
        public override bool IsClosed=>false;
        public override int RecordsAffected=>-1;
        public override int Depth=>0;
        public override bool NextResult()=>false;
        public override Type GetFieldType(int ordinal)=>this.values[ordinal]?.GetType()??typeof(object);
        public override string GetDataTypeName(int ordinal)=>this.GetFieldType(ordinal).Name;
        public override bool GetBoolean(int ordinal)=>(bool)this.values[ordinal]!;
        public override byte GetByte(int ordinal)=>(byte)this.values[ordinal]!;
        // 少しずつ読む形(バッファが null なら全体の長さを返す)。値は1行分を持っているので、そこから写す。
        public override long GetBytes(int ordinal,long dataOffset,byte[]? buffer,int bufferOffset,int length)=>Copy((byte[])this.values[ordinal]!,dataOffset,buffer,bufferOffset,length);
        public override char GetChar(int ordinal)=>(char)this.values[ordinal]!;
        public override long GetChars(int ordinal,long dataOffset,char[]? buffer,int bufferOffset,int length)=>Copy(((string)this.values[ordinal]!).ToCharArray(),dataOffset,buffer,bufferOffset,length);
        private static long Copy<T>(T[] source,long offset,T[]? buffer,int bufferOffset,int length){
            if(buffer is null){return source.Length;}
            var count=(int)Math.Max(0,Math.Min(length,source.Length-offset));
            Array.Copy(source,offset,buffer,bufferOffset,count);
            return count;
        }
        public override DateTime GetDateTime(int ordinal)=>(DateTime)this.values[ordinal]!;
        public override decimal GetDecimal(int ordinal)=>(decimal)this.values[ordinal]!;
        public override double GetDouble(int ordinal)=>(double)this.values[ordinal]!;
        public override float GetFloat(int ordinal)=>(float)this.values[ordinal]!;
        public override Guid GetGuid(int ordinal)=>(Guid)this.values[ordinal]!;
        public override short GetInt16(int ordinal)=>(short)this.values[ordinal]!;
        public override int GetInt32(int ordinal)=>(int)this.values[ordinal]!;
        public override long GetInt64(int ordinal)=>(long)this.values[ordinal]!;
        public override string GetString(int ordinal)=>(string)this.values[ordinal]!;
        public override IEnumerator GetEnumerator()=>throw new NotSupportedException();
        protected override void Dispose(bool disposing){if(disposing){(this.rows as IDisposable)?.Dispose();}base.Dispose(disposing);}
    }
}
