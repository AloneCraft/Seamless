using System.Collections;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Transfer;

/// <summary>生成済み定義の適用と逐次データ転送。正しさの合否判定や元DBとの比較は行わない。</summary>
public sealed class DatabaseTransferAdapter{
    public async Task<int>ApplyToEmptyDatabaseAsync(DbConnection target,GeneratedVendorDatabase definition,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(definition);
        if(target.State!=ConnectionState.Open){throw new InvalidOperationException("Open the explicit target connection before applying definitions.");}
        if(definition.Diagnostics.Any(d=>d.Severity is NativeDiagnosticSeverity.Unsupported or NativeDiagnosticSeverity.CompilationError)){throw new InvalidOperationException("The selected definition has unresolved conversion diagnostics.");}
        await using(var check=target.CreateCommand()){
            if(definition.Vendor==SqlVendor.SqlServer){check.CommandText="SELECT COUNT_BIG(*) FROM sys.objects WHERE is_ms_shipped=0";}
            else{check.CommandText="SELECT (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE())+(SELECT COUNT(*) FROM information_schema.routines WHERE routine_schema=DATABASE())";}
            if(Convert.ToInt64(await check.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!=0){throw new InvalidOperationException("The target database is not empty. No existing objects are replaced.");}
        }
        var count=0;
        foreach(var statement in definition.Statements){
            await using var command=target.CreateCommand();command.CommandText=statement;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);count++;
        }
        return count;
    }

    public async Task<long>WriteRowsAsync(DbConnection target,SqlVendor vendor,NativeTable table,IAsyncEnumerable<object?[]>rows,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(target);ArgumentNullException.ThrowIfNull(table);ArgumentNullException.ThrowIfNull(rows);
        string Quote(string name){if(vendor==SqlVendor.SqlServer){return "["+name.Replace("]","]]",StringComparison.Ordinal)+"]";}return "`"+name.Replace("`","``",StringComparison.Ordinal)+"`";}
        var relation=Quote(table.Name);
        if(vendor==SqlVendor.SqlServer){relation=Quote(table.Schema)+"."+relation;}else if(table.Schema!="dbo"){relation=Quote(table.Schema+"__"+table.Name);}
        await using var transaction=await target.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var command=target.CreateCommand();command.Transaction=transaction;
        command.CommandText="INSERT INTO "+relation+" ("+string.Join(",",table.Columns.Select(c=>Quote(c.Name)))+") VALUES ("+string.Join(",",Enumerable.Range(0,table.Columns.Length).Select(i=>"@p"+i))+ ")";
        foreach(var column in table.Columns){var parameter=command.CreateParameter();parameter.ParameterName="@p"+command.Parameters.Count;parameter.DbType=DbTypeFor(column,vendor);command.Parameters.Add(parameter);}
        long count=0;
        await foreach(var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false)){
            if(row.Length!=table.Columns.Length){throw new InvalidDataException("Transfer row width does not match the schema.");}
            for(var index=0;index<row.Length;index++){
                var value=row[index];var column=table.Columns[index];
                if(value is null&&!column.IsNullable){throw new InvalidDataException("NULL for nonnullable column: "+column.Name);}
                if(value is global::Seamless.Collections.IFixedText fixedText){var characters=new char[fixedText.Length];fixedText.CopyTo(characters);value=new string(characters);}
                if(value is string text&&vendor==SqlVendor.MySql){_ = new UTF8Encoding(false,true).GetByteCount(text);}
                if(value is Guid guid&&vendor==SqlVendor.MySql){value=guid.ToByteArray();}
                command.Parameters[index].Value=value??DBNull.Value;
            }
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);count=checked(count+1);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return count;
    }

    /// <summary>呼出し側が所有する一貫したスナップショットから、行を一件ずつ読む。</summary>
    public static async IAsyncEnumerable<object?[]>ReadRowsAsync(object database,NativeTable table,[EnumeratorCancellation]CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(database);ArgumentNullException.ThrowIfNull(table);
        var candidates=new List<object>{database};
        foreach(var property in database.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance)){
            if(typeof(global::Seamless.Database.DatabaseSchema).IsAssignableFrom(property.PropertyType)&&property.GetIndexParameters().Length==0&&property.GetValue(database) is{}schema){candidates.Add(schema);}
        }
        object?source=null;
        foreach(var candidate in candidates){
            foreach(var property in candidate.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance)){
                if(property.GetIndexParameters().Length!=0){continue;}
                var origin=property.GetCustomAttribute<global::Seamless.Database.ImportedDatabaseObjectAttribute>();
                var matches=property.Name==table.Name&&candidate.GetType().FullName+"."+property.Name==table.PropertyName;
                if(origin is not null){matches=origin.Schema==table.Schema&&origin.Name==table.Name;}
                if(matches){if(source is not null){throw new InvalidOperationException("Ambiguous native table binding.");}source=property.GetValue(candidate);}
            }
        }
        if(source is not IEnumerable records){throw new InvalidOperationException("Native table was not found: "+table.Schema+"."+table.Name);}
        if(ExecutionContext.IsFlowSuppressed()){throw new InvalidOperationException("Native row transfer requires execution-context flow.");}
        var iterator=records.GetEnumerator();
        // async iteratorのyieldを跨ぐと、同期enumeratorが作成したAsyncLocalの読取scopeが呼出し側へ戻らない。
        // 各MoveNextとDisposeを同じ列挙用コンテキストで実行し、呼出し側の状態を書き換えない。
        var context=ExecutionContext.Capture()!;
        try{
            while(true){
                cancellationToken.ThrowIfCancellationRequested();bool next=false;object?record=null;
                ExecutionContext.Run(context,_=>{
                    next=iterator.MoveNext();if(next){record=iterator.Current;}
                    context=ExecutionContext.Capture()!;
                },null);
                if(!next){break;}
                cancellationToken.ThrowIfCancellationRequested();if(record is null){throw new InvalidDataException("A native table returned a null record.");}
                var row=new object?[table.Columns.Length];var type=record.GetType();
                for(var index=0;index<row.Length;index++){
                    var name=table.Columns[index].MemberName;
                    var property=type.GetProperty(name,BindingFlags.Public|BindingFlags.Instance);
                    if(property is not null){row[index]=property.GetValue(record);}
                    else{row[index]=(type.GetField(name,BindingFlags.Public|BindingFlags.Instance)??throw new MissingMemberException(type.FullName,name)).GetValue(record);}
                }
                yield return row;
            }
        }finally{ExecutionContext.Run(context,_=>(iterator as IDisposable)?.Dispose(),null);}
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static DbType DbTypeFor(NativeColumn column,SqlVendor vendor){
        if(column.TextCapacity is not null){return DbType.String;}
        if(column.ClrType=="System.Guid"&&vendor==SqlVendor.MySql){return DbType.Binary;}
        return column.ClrType switch{
            "System.Boolean"=>DbType.Boolean,"System.Byte"=>DbType.Byte,"System.SByte"=>DbType.SByte,"System.Int16"=>DbType.Int16,"System.UInt16"=>DbType.UInt16,
            "System.Int32"=>DbType.Int32,"System.UInt32"=>DbType.UInt32,"System.Int64"=>DbType.Int64,"System.UInt64"=>DbType.UInt64,
            "System.String" or "System.Char"=>DbType.String,"System.Guid"=>DbType.Guid,"System.Byte[]"=>DbType.Binary,
            _=>throw new NotSupportedException("Native transfer type is not supported: "+column.ClrType),
        };
    }
}
