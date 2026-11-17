using System.Linq.Expressions;
using Seamless.Database.Query;
namespace Seamless.Database.Gateway.Hosting;

public sealed partial class InProcessDatabaseAdapter{
    internal bool HasNativeIntegerPrimaryKey(string name)=>this.tables.TryGetValue(name,out var table)&&table.IntegerPrimaryKey is not null;
    internal bool TryExecutePointMutation(string name,string column,object key,IReadOnlyDictionary<string,Func<object,object?>>? updates,Func<object,bool>? predicate,SessionContext session,out int count){
        count=0;if(!this.tables.TryGetValue(name,out var table)||table.IntegerPrimaryKey is not{} primary||!primary.Name.Equals(column,StringComparison.OrdinalIgnoreCase)||key is not(byte or short or int or long)){return false;}
        var integer=Convert.ToInt64(key,global::System.Globalization.CultureInfo.InvariantCulture);if(primary.ClrType==typeof(byte)&&integer is>=byte.MinValue and<=byte.MaxValue){key=(byte)integer;}else if(primary.ClrType==typeof(short)&&integer is>=short.MinValue and<=short.MaxValue){key=(short)integer;}else if(primary.ClrType==typeof(int)&&integer is>=int.MinValue and<=int.MaxValue){key=(int)integer;}else if(primary.ClrType==typeof(long)){key=integer;}else{return false;}
        this.PrepareGatewayWrite(session);var found=table.ReadIntegerPoint(this.database,key);if(!found.Found||predicate is not null&&!predicate(found.Row!)){return true;}
        if(updates is null){table.RemoveRecord(this.database,found.Row!);}else{IReadOnlyDictionary<string,object?> values=updates.ToDictionary(static pair=>pair.Key,pair=>pair.Value(found.Row!),StringComparer.OrdinalIgnoreCase);if(this.GatewayTransactions is{} owner){values=owner.CompleteUpdate(session,this,table.Metadata,values);}var updated=table.UpdateRecord(found.Row!,table.MapValues(values));table.SetOrUpdateRecord(this.database,found.Row!,updated);}count=1;return true;
    }
    private sealed partial class TableBinding{
        internal ColumnMetadata? IntegerPrimaryKey{get;private set;}
        private Func<DatabaseContainer,object,(bool Found,object? Row)>? integerPoint;
        private void BindIntegerPrimaryKey(Type tableType){
            if(this.Metadata.PrimaryKey?.Columns.Count!=1||this.keyProperties?.Length!=1||this.keyConstructor?.GetParameters().Length!=1||this.keyType is null){return;}
            var column=this.Metadata.Columns.SingleOrDefault(column=>column.Name.Equals(this.Metadata.PrimaryKey.Columns[0].Name,StringComparison.OrdinalIgnoreCase));if(column is null||column.IsNullable||column.ComputedDefinition is not null||column.ClrType!=typeof(byte)&&column.ClrType!=typeof(short)&&column.ClrType!=typeof(int)&&column.ClrType!=typeof(long)||this.keyConstructor.GetParameters()[0].ParameterType!=column.ClrType||this.keyProperties[0].PropertyType!=column.ClrType){return;}
            var index=this.Metadata.Columns.ToList().IndexOf(column);if(this.propertyNames[index]!=this.keyProperties[0].Name){return;}var lookup=tableType.GetMethod("TryGetValue",[this.keyType,this.RecordType.MakeByRefType()]);if(lookup is null){return;}
            var database=Expression.Parameter(typeof(DatabaseContainer));var key=Expression.Parameter(typeof(object));var row=Expression.Variable(this.RecordType);var target=Expression.Convert(Expression.Invoke(Expression.Constant(this.getTable),database),tableType);var call=Expression.Call(target,lookup,Expression.Convert(key,this.keyType),row);var tuple=typeof(ValueTuple<bool,object>).GetConstructor([typeof(bool),typeof(object)])!;this.integerPoint=Expression.Lambda<Func<DatabaseContainer,object,(bool,object?)>>(Expression.Block([row],Expression.New(tuple,call,Expression.Convert(row,typeof(object)))),database,key).Compile();this.IntegerPrimaryKey=column;
        }
        internal (bool Found,object? Row) ReadIntegerPoint(DatabaseContainer database,object key)=>this.integerPoint!(database,this.keyConstructor!.Invoke([key]));
    }
}
