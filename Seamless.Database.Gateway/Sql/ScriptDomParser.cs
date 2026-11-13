using System.Globalization;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Sql;

internal sealed class SqlDefaultValue{internal static SqlDefaultValue Instance{get;}=new();private SqlDefaultValue(){}}

public enum SqlCommandKind{Select,Insert,Update,Delete,BeginTransaction,CommitTransaction,RollbackTransaction,Other}
public sealed class ParsedCommand(SqlCommandKind kind,string tableName,IReadOnlyList<string>? columns,IReadOnlyDictionary<string,object?>? values,Func<object,bool>? predicate){
    public SqlCommandKind Kind{get;}=kind;public string TableName{get;}=tableName;public IReadOnlyList<string>? Columns{get;}=columns;public IReadOnlyDictionary<string,object?>? Values{get;}=values;public Func<object,bool>? Predicate{get;}=predicate;
    public IReadOnlyList<IReadOnlyDictionary<string,object?>> InsertRows{get;init;}=[];
    public IReadOnlyDictionary<string,Func<object,object?>> UpdateValues{get;init;}=new Dictionary<string,Func<object,object?>>();
}
/// <summary>変更対象を事前に完全検査する。未解釈の条件を無条件更新へ変換しない。</summary>
public static class ScriptDomParser{
    public static ParsedCommand Parse(string sql,IDatabaseAdapter adapter,IReadOnlyDictionary<string,object?>? parameters=null)=>Parse(sql,adapter,parameters,null);
    internal static ParsedCommand Parse(string sql,IDatabaseAdapter adapter,IReadOnlyDictionary<string,object?>? parameters,string? database,Func<ScalarExpression,TableMetadata?,bool,Func<object,object?>>? scalarCompiler=null,Func<BooleanExpression,TableMetadata,Func<object,bool?>>? predicateCompiler=null,Action<TableMetadata>? demand=null,SqlSchemaResolution resolution=SqlSchemaResolution.SqlServer){
        var script=new TSql160Parser(true).Parse(new StringReader(sql),out var errors) as TSqlScript;if(errors.Count>0){throw new SqlQueryException(errors[0].Message,102,errors[0].Line);}
        if(script is null||script.Batches.Sum(b=>b.Statements.Count)!=1){throw new SqlQueryException("One statement is required.");}
        var statement=script.Batches.SelectMany(b=>b.Statements).Single();var binder=new Binder(adapter,parameters,database,scalarCompiler,predicateCompiler,demand,resolution);
        if(statement is SelectStatement select&&select.QueryExpression is QuerySpecification query&&query.FromClause?.TableReferences.Count==1){var table=binder.BindTable(query.FromClause.TableReferences[0]);var names=new List<string>();foreach(var element in query.SelectElements){if(element is SelectStarExpression){names.Add("*");}else if(element is SelectScalarExpression scalar&&scalar.Expression is ColumnReferenceExpression column){names.Add(binder.Column(column).Name);}else{throw Unsupported(element);}}Func<object,bool>? predicate=null;if(query.WhereClause is not null){var condition=binder.Boolean(query.WhereClause.SearchCondition);predicate=row=>condition(row)==true;}return new(SqlCommandKind.Select,table.FullName,names,null,predicate);}
        if(statement is InsertStatement insert){
            var spec=insert.InsertSpecification;if(spec.TopRowFilter is not null||spec.OutputClause is not null||spec.OutputIntoClause is not null||spec.InsertSource is not ValuesInsertSource source){throw Unsupported(statement);}
            var table=binder.BindTable(spec.Target);var names=spec.Columns.Select(c=>binder.Column(c).Name).ToArray();if(names.Length==0){names=table.Columns.Where(static column=>!column.IsIdentity&&!column.IsRowVersion&&column.ComputedDefinition is null).Select(c=>c.Name).ToArray();}if(names.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=names.Length){throw new SqlQueryException("An INSERT column is repeated.",264);}
            var rows=new List<IReadOnlyDictionary<string,object?>>();foreach(var row in source.RowValues){if(row.ColumnValues.Count!=names.Length){throw new SqlQueryException("INSERT column and value counts differ.",213);}var values=new Dictionary<string,object?>(StringComparer.OrdinalIgnoreCase);for(var i=0;i<names.Length;i++){if(row.ColumnValues[i] is DefaultLiteral){continue;}var value=binder.Scalar(row.ColumnValues[i],false)(null!);ValidateNull(table.Columns.Single(c=>c.Name.Equals(names[i],StringComparison.OrdinalIgnoreCase)),value);values.Add(names[i],value);}foreach(var column in table.Columns){if(!column.IsNullable&&!column.HasInsertDefault&&!values.ContainsKey(column.Name)){throw new SqlQueryException($"Required column '{column.Name}' has no value.",515);}}rows.Add(values);}
            if(source.IsDefaultValues){if(table.Columns.Any(static column=>!column.IsNullable&&!column.HasInsertDefault)){throw new SqlQueryException("A required column has no DEFAULT.",515);}rows.Add(new Dictionary<string,object?>());}
            return new(SqlCommandKind.Insert,binder.TargetName(table),null,rows.FirstOrDefault(),null){InsertRows=rows};
        }
        if(statement is UpdateStatement update){
            var spec=update.UpdateSpecification;if(spec.FromClause is not null||spec.TopRowFilter is not null||spec.OutputClause is not null||spec.OutputIntoClause is not null){throw Unsupported(statement);}var table=binder.BindTable(spec.Target);var values=new Dictionary<string,Func<object,object?>>(StringComparer.OrdinalIgnoreCase);
            if(spec.SetClauses.OfType<AssignmentSetClause>().Any(assignment=>assignment.Column is not null&&binder.Column(assignment.Column).IsRowVersion)){throw new SqlQueryException("A timestamp column cannot be updated explicitly.",272);}
            foreach(var clause in spec.SetClauses){if(clause is not AssignmentSetClause assignment||assignment.AssignmentKind!=AssignmentKind.Equals||assignment.Column is null){throw Unsupported(clause);}var column=binder.Column(assignment.Column);if(column.IsIdentity){throw new SqlQueryException("An identity column cannot be updated.",8102);}if(column.ComputedDefinition is not null){throw new SqlQueryException("A computed column cannot be updated.",271);}if(column.IsPrimaryKey&&adapter is not InProcessDatabaseAdapter{GatewayTransactions:{TypedDdlEnabled:true}}){throw new SqlQueryException("Updating a primary key is not supported by this adapter.");}var expression=binder.Scalar(assignment.NewValue,true);values.Add(column.Name,row=>{var value=expression(row);ValidateNull(column,value);return value;});}
            Func<object,bool>? predicate=null;if(spec.WhereClause is not null){var condition=binder.Boolean(spec.WhereClause.SearchCondition);predicate=row=>condition(row)==true;}return new(SqlCommandKind.Update,binder.TargetName(table),null,null,predicate){UpdateValues=values};
        }
        if(statement is DeleteStatement delete){var spec=delete.DeleteSpecification;if(spec.FromClause is not null||spec.TopRowFilter is not null||spec.OutputClause is not null||spec.OutputIntoClause is not null){throw Unsupported(statement);}var table=binder.BindTable(spec.Target);Func<object,bool>? predicate=null;if(spec.WhereClause is not null){var condition=binder.Boolean(spec.WhereClause.SearchCondition);predicate=row=>condition(row)==true;}return new(SqlCommandKind.Delete,binder.TargetName(table),null,null,predicate);}
        if(statement is BeginTransactionStatement){return new(SqlCommandKind.BeginTransaction,"",null,null,null);}if(statement is CommitTransactionStatement){return new(SqlCommandKind.CommitTransaction,"",null,null,null);}if(statement is RollbackTransactionStatement){return new(SqlCommandKind.RollbackTransaction,"",null,null,null);}
        throw Unsupported(statement);
    }
    private static SqlQueryException Unsupported(TSqlFragment fragment)=>new($"Unsupported DML syntax: {fragment.GetType().Name}.",40515);
    private static void ValidateNull(ColumnMetadata column,object? value){if(value is null&&!column.IsNullable){throw new SqlQueryException($"Column '{column.Name}' does not allow NULL.",515);}}
    private sealed class Binder(IDatabaseAdapter adapter,IReadOnlyDictionary<string,object?>? parameters,string? database,Func<ScalarExpression,TableMetadata?,bool,Func<object,object?>>? scalarCompiler,Func<BooleanExpression,TableMetadata,Func<object,bool?>>? predicateCompiler,Action<TableMetadata>? demand,SqlSchemaResolution resolution){
        private TableMetadata? table;
        public string TargetName(TableMetadata metadata)=>demand is not null&&adapter is InProcessDatabaseAdapter?metadata.IdentifierKey:metadata.FullName;
        public TableMetadata BindTable(TableReference target){
            if(target is not NamedTableReference named||named.SchemaObject.ServerIdentifier is not null||named.SchemaObject.DatabaseIdentifier is{} owner&&!string.Equals(owner.Value,database,StringComparison.OrdinalIgnoreCase)){throw Unsupported(target);}
            var written=named.SchemaObject.SchemaIdentifier?.Value;var name=named.SchemaObject.BaseIdentifier.Value;
            // [名前] スキーマを書かなければ、方言の探し方(SQL Server なら dbo→根)の順に探す。
            string? missing=null;
            foreach(var schema in written is not null?[written]:resolution.LookupSchemas()){
                var metadata=new TableMetadata(schema,name,[]);
                var key=demand is not null&&adapter is InProcessDatabaseAdapter?metadata.IdentifierKey:metadata.FullName;
                missing??=key;this.table=adapter.FindTable(key);if(this.table is not null){break;}
            }
            if(this.table is null){throw new SqlQueryException($"Invalid object name '{missing}'.",208);}
            demand?.Invoke(this.table);return this.table;
        }
        public ColumnMetadata Column(ColumnReferenceExpression reference){if(this.table is null){throw Unsupported(reference);}var name=reference.MultiPartIdentifier.Identifiers.Last().Value;return this.table.Columns.SingleOrDefault(c=>c.Name.Equals(name,StringComparison.OrdinalIgnoreCase))??throw new SqlQueryException($"Invalid column '{name}'.",207);}
        public Func<object,object?> Scalar(ScalarExpression expression,bool allowColumn){
            if(expression is DefaultLiteral){return _=>SqlDefaultValue.Instance;}
            if(scalarCompiler is not null){return scalarCompiler(expression,this.table,allowColumn);}
            if(expression is NullLiteral){return _=>null;}if(expression is StringLiteral text){return _=>text.Value;}if(expression is IntegerLiteral integer){var number=long.Parse(integer.Value,CultureInfo.InvariantCulture);if(number>=int.MinValue&&number<=int.MaxValue){return _=>(int)number;}return _=>number;}
            if(expression is NumericLiteral numeric){var number=decimal.Parse(numeric.Value,CultureInfo.InvariantCulture);return _=>number;}if(expression is RealLiteral real){var number=double.Parse(real.Value,CultureInfo.InvariantCulture);return _=>number;}
            if(expression is BinaryLiteral binary){var bytes=Convert.FromHexString(binary.Value[2..]);return _=>bytes;}
            if(expression is VariableReference variable){if(parameters is null||!parameters.TryGetValue(variable.Name,out var value)){throw new SqlQueryException($"Parameter '{variable.Name}' is missing.",137);}return _=>value;}
            if(expression is ColumnReferenceExpression column&&allowColumn){var metadata=this.Column(column);return row=>adapter.GetRecordValue(this.table!.FullName,metadata.Name,row);}
            if(expression is ParenthesisExpression parenthesis){return this.Scalar(parenthesis.Expression,allowColumn);}
            if(expression is UnaryExpression unary){var operand=this.Scalar(unary.Expression,allowColumn);if(unary.UnaryExpressionType is not (UnaryExpressionType.Positive or UnaryExpressionType.Negative)){throw Unsupported(unary);}return row=>{var value=operand(row);if(value is null){return null;}var number=Convert.ToDecimal(value,CultureInfo.InvariantCulture);if(unary.UnaryExpressionType==UnaryExpressionType.Negative){return -number;}return number;};}
            if(expression is BinaryExpression binaryExpression){var left=this.Scalar(binaryExpression.FirstExpression,allowColumn);var right=this.Scalar(binaryExpression.SecondExpression,allowColumn);if(binaryExpression.BinaryExpressionType is not (BinaryExpressionType.Add or BinaryExpressionType.Subtract or BinaryExpressionType.Multiply or BinaryExpressionType.Divide or BinaryExpressionType.Modulo)){throw Unsupported(expression);}return row=>{var a=left(row);var b=right(row);if(a is null||b is null){return null;}if(a is string sa&&b is string sb&&binaryExpression.BinaryExpressionType==BinaryExpressionType.Add){return sa+sb;}var x=Convert.ToDecimal(a,CultureInfo.InvariantCulture);var y=Convert.ToDecimal(b,CultureInfo.InvariantCulture);return binaryExpression.BinaryExpressionType switch{BinaryExpressionType.Add=>checked(x+y),BinaryExpressionType.Subtract=>checked(x-y),BinaryExpressionType.Multiply=>checked(x*y),BinaryExpressionType.Divide=>x/y,BinaryExpressionType.Modulo=>x%y,_=>throw Unsupported(expression)};};}
            if(expression is CastCall cast&&cast.DataType is SqlDataTypeReference type){var operand=this.Scalar(cast.Parameter,allowColumn);return row=>{var value=operand(row);if(value is null){return null;}return type.SqlDataTypeOption switch{SqlDataTypeOption.Int=>Convert.ToInt32(value,CultureInfo.InvariantCulture),SqlDataTypeOption.BigInt=>Convert.ToInt64(value,CultureInfo.InvariantCulture),SqlDataTypeOption.Decimal or SqlDataTypeOption.Numeric=>Convert.ToDecimal(value,CultureInfo.InvariantCulture),SqlDataTypeOption.Bit=>Convert.ToBoolean(value,CultureInfo.InvariantCulture),_=>throw Unsupported(expression)};};}
            throw Unsupported(expression);
        }
        public Func<object,bool?> Boolean(BooleanExpression expression){
            if(predicateCompiler is not null&&this.table is not null){return predicateCompiler(expression,this.table);}
            if(expression is BooleanComparisonExpression comparison){var left=this.Scalar(comparison.FirstExpression,true);var right=this.Scalar(comparison.SecondExpression,true);if(comparison.ComparisonType is not (BooleanComparisonType.Equals or BooleanComparisonType.NotEqualToBrackets or BooleanComparisonType.NotEqualToExclamation or BooleanComparisonType.GreaterThan or BooleanComparisonType.GreaterThanOrEqualTo or BooleanComparisonType.LessThan or BooleanComparisonType.LessThanOrEqualTo)){throw Unsupported(expression);}return row=>{var a=left(row);var b=right(row);if(a is null||b is null){return null;}var value=Compare(a,b);return comparison.ComparisonType switch{BooleanComparisonType.Equals=>value==0,BooleanComparisonType.NotEqualToBrackets or BooleanComparisonType.NotEqualToExclamation=>value!=0,BooleanComparisonType.GreaterThan=>value>0,BooleanComparisonType.GreaterThanOrEqualTo=>value>=0,BooleanComparisonType.LessThan=>value<0,BooleanComparisonType.LessThanOrEqualTo=>value<=0,_=>throw Unsupported(expression)};};}
            if(expression is BooleanIsNullExpression isNull){var operand=this.Scalar(isNull.Expression,true);return row=>(operand(row)is null)!=isNull.IsNot;}
            if(expression is BooleanParenthesisExpression parenthesis){return this.Boolean(parenthesis.Expression);}
            if(expression is BooleanNotExpression not){var operand=this.Boolean(not.Expression);return row=>!operand(row);}
            if(expression is BooleanBinaryExpression binary){var left=this.Boolean(binary.FirstExpression);var right=this.Boolean(binary.SecondExpression);return row=>{var a=left(row);var b=right(row);if(binary.BinaryExpressionType==BooleanBinaryExpressionType.And){return a&b;}return a|b;};}
            throw Unsupported(expression);
        }
        private static int Compare(object a,object b){if(a is string x&&b is string y){return StringComparer.Ordinal.Compare(x.TrimEnd(' '),y.TrimEnd(' '));}if(a is byte[] ax&&b is byte[] bx){return ax.AsSpan().SequenceCompareTo(bx);}if(a is DateTime date){return date.CompareTo(Convert.ToDateTime(b,CultureInfo.InvariantCulture));}if(a is Guid guid){return guid.CompareTo(Guid.Parse(b.ToString()!));}return Convert.ToDecimal(a,CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(b,CultureInfo.InvariantCulture));}
    }
}
