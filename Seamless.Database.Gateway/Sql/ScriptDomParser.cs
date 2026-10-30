using System.Globalization;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Seamless.Database.Gateway.Hosting;

namespace Seamless.Database.Gateway.Sql;

public enum SqlCommandKind{
    Select,
    Insert,
    Update,
    Delete,
    BeginTransaction,
    CommitTransaction,
    RollbackTransaction,
    Other
}

public sealed class ParsedCommand{
    public ParsedCommand(
        SqlCommandKind kind,
        string tableName,
        IReadOnlyList<string>? columns,
        IReadOnlyDictionary<string,object?>? values,
        Func<object,bool>? predicate){
        this.Kind=kind;
        this.TableName=tableName;
        this.Columns=columns;
        this.Values=values;
        this.Predicate=predicate;
    }

    public SqlCommandKind Kind{get;}
    public string TableName{get;}
    public IReadOnlyList<string>? Columns{get;}
    public IReadOnlyDictionary<string,object?>? Values{get;}
    public Func<object,bool>? Predicate{get;}
}

public static class ScriptDomParser{
    public static ParsedCommand Parse(string sql,IDatabaseAdapter adapter){
        var parser=new TSql160Parser(true);
        using var reader=new StringReader(sql);
        var fragment=parser.Parse(reader,out var errors);
        if(errors.Count>0){
            throw new InvalidOperationException($"SQL parse error: {errors[0].Message}");
        }else{
        }

        if(fragment is not TSqlScript script||script.Batches.Count==0||script.Batches[0].Statements.Count==0){
            return new ParsedCommand(SqlCommandKind.Other,string.Empty,null,null,null);
        }else{
        }

        var statement=script.Batches[0].Statements[0];
        if(statement is SelectStatement select){
            return ParseSelect(select,adapter);
        }else if(statement is InsertStatement insert){
            return ParseInsert(insert,adapter);
        }else if(statement is UpdateStatement update){
            return ParseUpdate(update,adapter);
        }else if(statement is DeleteStatement delete){
            return ParseDelete(delete,adapter);
        }else if(statement is BeginTransactionStatement){
            return new ParsedCommand(SqlCommandKind.BeginTransaction,string.Empty,null,null,null);
        }else if(statement is CommitTransactionStatement){
            return new ParsedCommand(SqlCommandKind.CommitTransaction,string.Empty,null,null,null);
        }else if(statement is RollbackTransactionStatement){
            return new ParsedCommand(SqlCommandKind.RollbackTransaction,string.Empty,null,null,null);
        }else{
            return new ParsedCommand(SqlCommandKind.Other,string.Empty,null,null,null);
        }
    }

    private static ParsedCommand ParseSelect(SelectStatement select,IDatabaseAdapter adapter){
        if(select.QueryExpression is not QuerySpecification qs){
            return new ParsedCommand(SqlCommandKind.Other,string.Empty,null,null,null);
        }else{
        }

        var tableName=string.Empty;
        if(qs.FromClause is not null&&qs.FromClause.TableReferences.Count>0){
            var tr=qs.FromClause.TableReferences[0];
            if(tr is NamedTableReference ntr){
                tableName=ntr.SchemaObject.BaseIdentifier.Value;
            }else{
            }
        }else{
        }

        var columns=new List<string>();
        foreach(var item in qs.SelectElements){
            if(item is SelectStarExpression){
                columns.Add("*");
            }else if(item is SelectScalarExpression sse){
                if(sse.Expression is ColumnReferenceExpression cre){
                    var lastIdent=cre.MultiPartIdentifier.Identifiers.Last();
                    columns.Add(lastIdent.Value);
                }else{
                    columns.Add("*");
                }
            }else{
            }
        }

        Func<object,bool>? predicate=null;
        if(qs.WhereClause is not null&&!string.IsNullOrEmpty(tableName)){
            predicate=BuildPredicate(qs.WhereClause.SearchCondition,tableName,adapter);
        }else{
        }

        return new ParsedCommand(SqlCommandKind.Select,tableName,columns,null,predicate);
    }

    private static ParsedCommand ParseInsert(InsertStatement insert,IDatabaseAdapter adapter){
        var target=insert.InsertSpecification.Target;
        var tableName=string.Empty;
        if(target is NamedTableReference ntr){
            tableName=ntr.SchemaObject.BaseIdentifier.Value;
        }else{
        }

        var colNames=new List<string>();
        if(insert.InsertSpecification.Columns is not null){
            foreach(var c in insert.InsertSpecification.Columns){
                var last=c.MultiPartIdentifier.Identifiers.Last();
                colNames.Add(last.Value);
            }
        }else{
        }

        var valuesDict=new Dictionary<string,object?>(StringComparer.OrdinalIgnoreCase);
        if(insert.InsertSpecification.InsertSource is ValuesInsertSource vis&&vis.RowValues.Count>0){
            var rowVals=vis.RowValues[0];
            if(colNames.Count>0){
                for(var i=0;i<colNames.Count&&i<rowVals.ColumnValues.Count;i++){
                    valuesDict[colNames[i]]=ExtractLiteralValue(rowVals.ColumnValues[i]);
                }
            }else{
                var tableMeta=adapter.FindTable(tableName);
                if(tableMeta is not null){
                    for(var i=0;i<tableMeta.Columns.Count&&i<rowVals.ColumnValues.Count;i++){
                        valuesDict[tableMeta.Columns[i].Name]=ExtractLiteralValue(rowVals.ColumnValues[i]);
                    }
                }else{
                }
            }
        }else{
        }

        return new ParsedCommand(SqlCommandKind.Insert,tableName,null,valuesDict,null);
    }

    private static ParsedCommand ParseUpdate(UpdateStatement update,IDatabaseAdapter adapter){
        var target=update.UpdateSpecification.Target;
        var tableName=string.Empty;
        if(target is NamedTableReference ntr){
            tableName=ntr.SchemaObject.BaseIdentifier.Value;
        }else{
        }

        var valuesDict=new Dictionary<string,object?>(StringComparer.OrdinalIgnoreCase);
        foreach(var setClause in update.UpdateSpecification.SetClauses){
            if(setClause is AssignmentSetClause asc&&asc.Column is not null){
                var colName=asc.Column.MultiPartIdentifier.Identifiers.Last().Value;
                valuesDict[colName]=ExtractLiteralValue(asc.NewValue);
            }else{
            }
        }

        Func<object,bool>? predicate=null;
        if(update.UpdateSpecification.WhereClause is not null&&!string.IsNullOrEmpty(tableName)){
            predicate=BuildPredicate(update.UpdateSpecification.WhereClause.SearchCondition,tableName,adapter);
        }else{
        }

        return new ParsedCommand(SqlCommandKind.Update,tableName,null,valuesDict,predicate);
    }

    private static ParsedCommand ParseDelete(DeleteStatement delete,IDatabaseAdapter adapter){
        var target=delete.DeleteSpecification.Target;
        var tableName=string.Empty;
        if(target is NamedTableReference ntr){
            tableName=ntr.SchemaObject.BaseIdentifier.Value;
        }else{
        }

        Func<object,bool>? predicate=null;
        if(delete.DeleteSpecification.WhereClause is not null&&!string.IsNullOrEmpty(tableName)){
            predicate=BuildPredicate(delete.DeleteSpecification.WhereClause.SearchCondition,tableName,adapter);
        }else{
        }

        return new ParsedCommand(SqlCommandKind.Delete,tableName,null,null,predicate);
    }

    private static Func<object,bool>? BuildPredicate(BooleanExpression expr,string tableName,IDatabaseAdapter adapter){
        if(expr is BooleanComparisonExpression bce){
            var left=bce.FirstExpression;
            var right=bce.SecondExpression;
            string? colName=null;
            object? val=null;

            if(left is ColumnReferenceExpression lcre){
                colName=lcre.MultiPartIdentifier.Identifiers.Last().Value;
                val=ExtractLiteralValue(right);
            }else if(right is ColumnReferenceExpression rcre){
                colName=rcre.MultiPartIdentifier.Identifiers.Last().Value;
                val=ExtractLiteralValue(left);
            }else{
            }

            if(colName is not null){
                var comparisonType=bce.ComparisonType;
                return (record)=>{
                    var prop=record.GetType().GetProperty(colName);
                    if(prop is null){
                        return true;
                    }else{
                    }
                    var actualVal=prop.GetValue(record);
                    if(actualVal is null||val is null){
                        if(comparisonType==BooleanComparisonType.Equals){
                            return actualVal is null&&val is null;
                        }else{
                            return false;
                        }
                    }else{
                    }

                    var converted=Convert.ChangeType(val,actualVal.GetType(),CultureInfo.InvariantCulture);
                    var comp=((IComparable)actualVal).CompareTo(converted);
                    if(comparisonType==BooleanComparisonType.Equals){
                        return comp==0;
                    }else if(comparisonType==BooleanComparisonType.GreaterThan){
                        return comp>0;
                    }else if(comparisonType==BooleanComparisonType.GreaterThanOrEqualTo){
                        return comp>=0;
                    }else if(comparisonType==BooleanComparisonType.LessThan){
                        return comp<0;
                    }else if(comparisonType==BooleanComparisonType.LessThanOrEqualTo){
                        return comp<=0;
                    }else if(comparisonType==BooleanComparisonType.NotEqualToBrackets||comparisonType==BooleanComparisonType.NotEqualToExclamation){
                        return comp!=0;
                    }else{
                        return true;
                    }
                };
            }else{
            }
        }else if(expr is BooleanBinaryExpression bbe){
            var p1=BuildPredicate(bbe.FirstExpression,tableName,adapter);
            var p2=BuildPredicate(bbe.SecondExpression,tableName,adapter);
            if(p1 is not null&&p2 is not null){
                if(bbe.BinaryExpressionType==BooleanBinaryExpressionType.And){
                    return (rec)=>p1(rec)&&p2(rec);
                }else{
                    return (rec)=>p1(rec)||p2(rec);
                }
            }else if(p1 is not null){
                return p1;
            }else if(p2 is not null){
                return p2;
            }else{
            }
        }else{
        }
        return null;
    }

    private static object? ExtractLiteralValue(ScalarExpression expr){
        if(expr is IntegerLiteral il){
            if(int.TryParse(il.Value,NumberStyles.Integer,CultureInfo.InvariantCulture,out var iv)){
                return iv;
            }else{
                return long.Parse(il.Value,CultureInfo.InvariantCulture);
            }
        }else if(expr is NumericLiteral nl){
            return double.Parse(nl.Value,CultureInfo.InvariantCulture);
        }else if(expr is StringLiteral sl){
            return sl.Value;
        }else if(expr is NullLiteral){
            return null;
        }else{
            return expr.ToString();
        }
    }
}
