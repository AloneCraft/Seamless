using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using SqlParser;
using SqlParser.Ast;
using SqlParser.Dialects;
using AstExpression=SqlParser.Ast.Expression;
using AstQuery=SqlParser.Ast.Query;

namespace Seamless.Database.Query;

/// <summary>SqlParserCS 0.6.5だけを受け持つ境界。外部ASTはここで所有構文へ写す。</summary>
internal sealed class SqlParserCsAdapter:ISqlSyntaxParser{
    public SqlDocumentSyntax Parse(string sql,SqlDialect dialect){
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        Dialect parserDialect=dialect switch{SqlDialect.SqlServer=>new MsSqlDialect(),SqlDialect.MySql=>new MySqlDialect(),SqlDialect.PostgreSql=>new PostgreSqlDialect(),_=>throw new SqlQueryException("Unsupported SQL dialect.",40515)};
        try{
            var parsed=new SqlQueryParser().Parse(sql.AsSpan(),parserDialect);
            var mapper=new Mapper(dialect);
            return new(dialect,sql,parsed.Select(mapper.Statement).ToArray());
        }catch(ParserException exception){throw new SqlQueryException(exception.Message,102,checked((int)exception.Line));}
    }

    internal SqlSelectSyntax ParseViewDefinition(string sql,SqlObjectName expected){
        if(sql.Length>1048576){throw new SqlQueryException("View definition exceeds the SQL limit.",103);}
        try{
            var parsed=new SqlQueryParser().Parse(sql.AsSpan(),new MsSqlDialect());
            if(parsed.Count!=1){throw new SqlQueryException("Exactly one view definition is required.",102);}
            var mapper=new Mapper(SqlDialect.SqlServer);
            if(parsed[0] is Statement.Select select){Guard(select,"Query");return mapper.Query(select.Query);}
            if(parsed[0] is not Statement.CreateView view){throw Unsupported(parsed[0]);}
            Guard(view,"Name","Query","Columns","Options");
            if(view.Options is not(null or CreateTableOptions.None)){throw Unsupported(view.Options);}
            var name=mapper.Name(view.Name);
            if(name.Parts.Count is <1 or >2||!string.Equals(name.Parts[^1].Value,expected.Name,StringComparison.OrdinalIgnoreCase)||name.Parts.Count==2&&!string.Equals(name.Parts[0].Value,expected.Schema,StringComparison.OrdinalIgnoreCase)){throw new SqlQueryException("View definition identity differs from the catalog.",40515);}
            Guard(view.Query,"Query");var query=mapper.Query(view.Query.Query);
            if(view.Columns is{Count:>0}columns){
                if(columns.Count!=query.Projection.Count||query.Projection.Any(item=>item.IsStar)){throw new SqlQueryException("Explicit view columns require a matching explicit projection.",4502);}
                var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var projection=new SqlProjectionSyntax[columns.Count];
                for(var index=0;index<columns.Count;index++){var column=columns[index];Guard(column,"Name");var alias=mapper.Identifier(column.Name);if(!names.Add(alias.Value)){throw new SqlQueryException("A view column is repeated.",4506);}projection[index]=query.Projection[index] with{Alias=alias};}
                query=query with{Projection=projection};
            }
            return query;
        }catch(ParserException exception){throw new SqlQueryException(exception.Message,102,checked((int)exception.Line));}
    }

    private sealed class Mapper(SqlDialect dialect){
        private int anonymousParameter;
        internal SqlStatementSyntax Statement(Statement statement){
            Guard(statement,"Query");
            if(statement is not Statement.Select select){throw Unsupported(statement);}
            return new SqlSelectStatementSyntax(this.Query(select.Query));
        }
        internal SqlSelectSyntax Query(AstQuery query){
            Guard(query,"Body","OrderBy","Limit");
            if(query.Body is not SetExpression.SelectExpression body){throw Unsupported(query.Body);}
            Guard(body,"Select");
            var select=body.Select;
            Guard(select,"Projection","From","Selection","GroupBy","Having","Distinct","Top","TopBeforeDistinct");
            if(select.Top is not null&&dialect!=SqlDialect.SqlServer){throw Unsupported(select.Top);}
            if(query.Limit is not null&&dialect==SqlDialect.SqlServer){throw Unsupported(query.Limit);}
            if(select.Top is not null&&query.Limit is not null){throw new SqlQueryException("TOP and LIMIT cannot be combined.",40515);}
            // Mapping order preserves lexical order for anonymous MySQL placeholders.
            SqlExpressionSyntax?limit=null;
            if(select.Top is{}top){
                Guard(top,"Quantity");
                limit=top.Quantity switch{
                    TopQuantity.Constant count=>new SqlLiteralSyntax(SqlLiteralKind.Integer,count.Quantity.ToString(CultureInfo.InvariantCulture)),
                    TopQuantity.TopExpression count=>this.Expression(count.Expression),
                    _=>throw Unsupported(top)
                };
            }
            var projection=select.Projection.Select(this.Projection).ToArray();
            SqlSourceSyntax?source=null;
            if(select.From is{Count:>0}from){if(from.Count!=1){throw Unsupported(select);}source=this.Source(from[0]);}
            var where=this.Optional(select.Selection);
            SqlExpressionSyntax[]groups=[];
            if(select.GroupBy is{}group){
                if(group is not GroupByExpression.Expressions expressions){throw Unsupported(group);}
                Guard(expressions,"ColumnNames");groups=expressions.ColumnNames.Select(this.Expression).ToArray();
            }
            var having=this.Optional(select.Having);
            SqlOrderSyntax[]order=[];
            if(query.OrderBy is{}orderBy){
                Guard(orderBy,"Expressions");
                order=(orderBy.Expressions??throw Unsupported(orderBy)).Select(item=>{
                    Guard(item,"Expression","Asc","NullsFirst");
                    if(item.NullsFirst is not null&&dialect==SqlDialect.SqlServer){throw Unsupported(item);}
                    return new SqlOrderSyntax(this.Expression(item.Expression),item.Asc==false,item.NullsFirst);
                }).ToArray();
            }
            if(query.Limit is not null){limit=this.Expression(query.Limit);}
            if(select.Distinct is not null&&select.Distinct is not DistinctFilter.Distinct){throw Unsupported(select.Distinct);}
            if(select.Distinct is{}distinct){Guard(distinct);}
            return new(projection,source,where,groups,having,order,select.Distinct is not null,limit);
        }
        private SqlProjectionSyntax Projection(SelectItem item){
            switch(item){
                case SelectItem.UnnamedExpression plain:Guard(plain,"Expression");return new(this.Expression(plain.Expression));
                case SelectItem.ExpressionWithAlias alias:Guard(alias,"Expression","Alias");return new(this.Expression(alias.Expression),this.Identifier(alias.Alias));
                case SelectItem.Wildcard star:Guard(star,"WildcardAdditionalOptions");Guard(star.WildcardAdditionalOptions);return new(null);
                case SelectItem.QualifiedWildcard star:Guard(star,"Name","WildcardAdditionalOptions");Guard(star.WildcardAdditionalOptions);return new(null,null,this.Name(star.Name));
                default:throw Unsupported(item);
            }
        }
        private SqlSourceSyntax Source(TableWithJoins source){
            Guard(source,"Relation","Joins");
            var result=this.Table(source.Relation??throw Unsupported(source));
            if(source.Joins is null){return result;}
            foreach(var join in source.Joins){
                Guard(join,"Relation","JoinOperator");
                var(kind,constraint)=join.JoinOperator switch{
                    JoinOperator.Inner inner=>(SqlSyntaxJoinKind.Inner,inner.JoinConstraint),
                    JoinOperator.LeftOuter left=>(SqlSyntaxJoinKind.Left,left.JoinConstraint),
                    _=>throw Unsupported(join)
                };
                Guard(join.JoinOperator,"JoinConstraint");
                if(constraint is not JoinConstraint.On on){throw Unsupported(constraint);}
                Guard(on,"Expression");
                result=new SqlJoinSyntax(result,this.Table(join.Relation??throw Unsupported(join)),kind,this.Expression(on.Expression));
            }
            return result;
        }
        private SqlSourceSyntax Table(TableFactor factor){
            if(factor is not TableFactor.Table table){throw Unsupported(factor);}
            Guard(table,"Name","Alias");
            SqlIdentifierSyntax?alias=null;
            if(table.Alias is{}tableAlias){Guard(tableAlias,"Name");alias=this.Identifier(tableAlias.Name);}
            return new SqlTableSyntax(this.Name(table.Name),alias);
        }
        private SqlExpressionSyntax?Optional(AstExpression?expression){if(expression is null){return null;}return this.Expression(expression);}
        private SqlExpressionSyntax Expression(AstExpression expression){
            switch(expression){
                case AstExpression.LiteralValue literal:Guard(literal,"Value");return this.Literal(literal.Value);
                case AstExpression.Identifier identifier:{
                    Guard(identifier,"Ident");
                    if(identifier.Ident.QuoteStyle is null&&identifier.Ident.Value.StartsWith('@')){return this.Parameter(identifier.Ident.Value);}
                    return new SqlColumnSyntax(new([this.Identifier(identifier.Ident)]));
                }
                case AstExpression.CompoundIdentifier identifier:Guard(identifier,"Idents");return new SqlColumnSyntax(new(identifier.Idents.Select(this.Identifier).ToArray()));
                case AstExpression.Nested nested:Guard(nested,"Expression");return this.Expression(nested.Expression);
                case AstExpression.BinaryOp binary:{
                    Guard(binary,"Left","Op","Right");
                    var operation=binary.Op switch{
                        BinaryOperator.Plus=>SqlSyntaxBinaryOperator.Add,BinaryOperator.Minus=>SqlSyntaxBinaryOperator.Subtract,
                        BinaryOperator.Multiply=>SqlSyntaxBinaryOperator.Multiply,BinaryOperator.Divide=>SqlSyntaxBinaryOperator.Divide,BinaryOperator.Modulo=>SqlSyntaxBinaryOperator.Modulo,
                        BinaryOperator.Eq=>SqlSyntaxBinaryOperator.Equal,BinaryOperator.NotEq=>SqlSyntaxBinaryOperator.NotEqual,
                        BinaryOperator.Gt=>SqlSyntaxBinaryOperator.Greater,BinaryOperator.Lt=>SqlSyntaxBinaryOperator.Less,
                        BinaryOperator.GtEq=>SqlSyntaxBinaryOperator.GreaterOrEqual,BinaryOperator.LtEq=>SqlSyntaxBinaryOperator.LessOrEqual,
                        BinaryOperator.And=>SqlSyntaxBinaryOperator.And,BinaryOperator.Or=>SqlSyntaxBinaryOperator.Or,
                        _=>throw Unsupported(binary)
                    };
                    return new SqlBinarySyntax(this.Expression(binary.Left),operation,this.Expression(binary.Right));
                }
                case AstExpression.UnaryOp unary:{
                    Guard(unary,"Op","Expression");
                    var operation=unary.Op switch{UnaryOperator.Plus=>SqlSyntaxUnaryOperator.Positive,UnaryOperator.Minus=>SqlSyntaxUnaryOperator.Negative,UnaryOperator.Not=>SqlSyntaxUnaryOperator.Not,_=>throw Unsupported(unary)};
                    return new SqlUnarySyntax(operation,this.Expression(unary.Expression));
                }
                case AstExpression.IsNull nullTest:Guard(nullTest,"Expression");return new SqlIsNullSyntax(this.Expression(nullTest.Expression),false);
                case AstExpression.IsNotNull nullTest:Guard(nullTest,"Expression");return new SqlIsNullSyntax(this.Expression(nullTest.Expression),true);
                case AstExpression.Case conditional:{
                    Guard(conditional,"Conditions","Results","Operand","ElseResult");
                    if(conditional.Conditions.Count!=conditional.Results.Count){throw Unsupported(conditional);}
                    var operand=this.Optional(conditional.Operand);
                    var arms=conditional.Conditions.Select((condition,index)=>new SqlCaseArmSyntax(this.Expression(condition),this.Expression(conditional.Results[index]))).ToArray();
                    return new SqlCaseSyntax(operand,arms,this.Optional(conditional.ElseResult));
                }
                case AstExpression.Cast cast:{
                    Guard(cast,"Expression","DataType","Kind");
                    if(cast.Kind.ToString()!="Cast"&&(cast.Kind.ToString()!="DoubleColon"||dialect!=SqlDialect.PostgreSql)){throw Unsupported(cast);}
                    return new SqlCastSyntax(this.Expression(cast.Expression),MapType(cast.DataType));
                }
                case AstExpression.Function function:return this.Function(function);
                default:throw Unsupported(expression);
            }
        }
        private SqlExpressionSyntax Function(AstExpression.Function function){
            Guard(function,"Name","Args","Parameters");
            if(function.Parameters is not null&&function.Parameters is not FunctionArguments.None){throw Unsupported(function);}
            var name=this.Name(function.Name);
            if(name.Parts.Count!=1){throw Unsupported(function);}
            var functionName=name.Parts[0].Value.ToUpperInvariant();
            if(functionName is not("COUNT" or "COUNT_BIG" or "SUM" or "AVG" or "MIN" or "MAX" or "ABS" or "COALESCE" or "NULLIF" or "ISNULL")){throw Unsupported(function);}
            if(function.Args is not FunctionArguments.List list){throw Unsupported(function);}
            Guard(list,"ArgumentList");var arguments=list.ArgumentList;Guard(arguments,"Args","DuplicateTreatment");
            var duplicate=arguments.DuplicateTreatment?.ToString();
            if(duplicate is not(null or "All" or "Distinct")){throw Unsupported(arguments);}
            var values=new List<SqlExpressionSyntax>();var star=false;
            foreach(var argument in arguments.Args??throw Unsupported(arguments)){
                if(argument is not FunctionArg.Unnamed unnamed){throw Unsupported(argument);}
                Guard(unnamed,"FunctionArgExpression");
                switch(unnamed.FunctionArgExpression){
                    case FunctionArgExpression.FunctionExpression value:Guard(value,"Expression");values.Add(this.Expression(value.Expression));break;
                    case FunctionArgExpression.Wildcard wildcard:Guard(wildcard);star=true;break;
                    default:throw Unsupported(unnamed);
                }
            }
            if(star&&(arguments.Args.Count!=1||functionName is not("COUNT" or "COUNT_BIG")||duplicate=="Distinct")){throw Unsupported(function);}
            return new SqlFunctionSyntax(functionName,values,duplicate=="Distinct",star);
        }
        private SqlExpressionSyntax Literal(Value value){
            switch(value){
                case Value.Number number:
                    Guard(number,"Value");
                    if(!Regex.IsMatch(number.Value,@"^(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$",RegexOptions.CultureInvariant)){throw Unsupported(value);}
                    var kind=SqlLiteralKind.Integer;if(number.Value.Contains('e')||number.Value.Contains('E')){kind=SqlLiteralKind.Real;}else if(number.Value.Contains('.')){kind=SqlLiteralKind.Decimal;}
                    return new SqlLiteralSyntax(kind,number.Value);
                case Value.SingleQuotedString text:Guard(text,"Value");return new SqlLiteralSyntax(SqlLiteralKind.String,text.Value);
                case Value.NationalStringLiteral text:Guard(text,"Value");return new SqlLiteralSyntax(SqlLiteralKind.NationalString,text.Value);
                case Value.Boolean boolean:{Guard(boolean,"Value");var text="false";if(boolean.Value){text="true";}return new SqlLiteralSyntax(SqlLiteralKind.Boolean,text);}
                case Value.Null empty:Guard(empty);return new SqlLiteralSyntax(SqlLiteralKind.Null,null);
                case Value.Placeholder parameter:Guard(parameter,"Value");return this.Parameter(parameter.Value);
                default:throw Unsupported(value);
            }
        }
        private SqlParameterSyntax Parameter(string value){
            if(dialect==SqlDialect.SqlServer&&Regex.IsMatch(value,@"^@[A-Za-z_][A-Za-z_0-9]*$",RegexOptions.CultureInvariant)){return new(value,SqlParameterStyle.Named);}
            if(dialect==SqlDialect.MySql&&value=="?"){return new("?",SqlParameterStyle.QuestionMark,++this.anonymousParameter);}
            if(dialect==SqlDialect.PostgreSql&&Regex.IsMatch(value,@"^\$[1-9][0-9]*$",RegexOptions.CultureInvariant)&&int.TryParse(value[1..],out var number)){return new(value,SqlParameterStyle.DollarNumber,number);}
            throw new SqlQueryException("Unsupported parameter marker for "+dialect+": "+value,40515);
        }
        internal SqlNameSyntax Name(ObjectName name){Guard(name,"Values");return new(name.Values.Select(this.Identifier).ToArray());}
        internal SqlIdentifierSyntax Identifier(Ident identifier){
            Guard(identifier,"Value","QuoteStyle");
            var quote=identifier.QuoteStyle;
            if(quote is not null&&!(quote=='"'&&dialect is SqlDialect.SqlServer or SqlDialect.PostgreSql||quote=='['&&dialect==SqlDialect.SqlServer||quote=='`'&&dialect==SqlDialect.MySql)){throw Unsupported(identifier);}
            return new(identifier.Value,quote);
        }
    }

    // Vendor AST options are inspected at this boundary; only owned syntax reaches the binder.
    private static SqlTypeSyntax MapType(DataType type){
        try{return MapTypeCore(type);}catch(Exception exception)when(exception is OverflowException or FormatException or InvalidCastException){throw new SqlQueryException("Invalid basic SQL datatype: "+exception.Message,40515);}
    }
    private static SqlTypeSyntax MapTypeCore(DataType type){
        if(type is DataType.Custom custom){
            Guard(custom,"Name");Guard(custom.Name,"Values");
            if(custom.Name.Values.Count!=1||custom.Name.Values[0].QuoteStyle is not null||!string.Equals(custom.Name.Values[0].Value,"bit",StringComparison.OrdinalIgnoreCase)){throw Unsupported(custom);}
            return new("bit");
        }
        var name=type.GetType().Name;
        switch(name){
            case "TinyInt":case "SmallInt":case "Int":case "Integer":case "BigInt":case "Float":
                Guard(type);return new(name.ToLowerInvariant());
            case "Bool":case "Boolean":case "Real":case "Double":case "Date":case "Text":case "Uuid":
                Guard(type);return new(name.ToLowerInvariant());
            case "Decimal":case "Numeric":{
                Guard(type,"ExactNumberInfo");var info=Read(type,"ExactNumberInfo");
                if(info is null||info.GetType().Name=="None"){return new(name.ToLowerInvariant());}
                Guard(info,"Length","Scale");
                var precision=checked((byte)Convert.ToInt32(Read(info,"Length"),CultureInfo.InvariantCulture));
                byte scale=0;
                if(info.GetType().Name=="PrecisionAndScale"){scale=checked((byte)Convert.ToInt32(Read(info,"Scale"),CultureInfo.InvariantCulture));}
                else if(info.GetType().Name!="Precision"){throw Unsupported(info);}
                return new(name.ToLowerInvariant(),null,precision,scale);
            }
            case "Char":case "Varchar":case "Nvarchar":{
                Guard(type,"CharacterLength","Length");
                var size=Read(type,"CharacterLength");int?length=null;
                if(size is not null){
                    if(size.GetType().Name=="IntegerLength"){Guard(size,"Length","Unit");var unit=Read(size,"Unit");if(unit is not null&&unit.ToString()!="None"){throw Unsupported(size);}size=Read(size,"Length");}
                    if(size is not IConvertible){throw Unsupported(size!);}
                    length=checked(Convert.ToInt32(size,CultureInfo.InvariantCulture));
                }
                return new(name.ToLowerInvariant(),length);
            }
            default:throw Unsupported(type);
        }
    }
    private static object?Read(object value,string property){
        var member=value.GetType().GetProperty(property,BindingFlags.Public|BindingFlags.Instance)??throw new SqlQueryException("Unexpected SqlParserCS datatype shape: "+value.GetType().Name+"."+property,40515);
        return member.GetValue(value);
    }
    private static void Guard(object?value,params string[]consumed){
        if(value is null){return;}
        foreach(var property in value.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance)){
            if(property.GetIndexParameters().Length!=0||!property.CanRead||consumed.Contains(property.Name,StringComparer.Ordinal)){continue;}
            var memberValue=property.GetValue(value);
            if(memberValue is not null&&(Nullable.GetUnderlyingType(property.PropertyType)is not null||Active(memberValue))){throw new SqlQueryException("Unsupported SQL clause: "+value.GetType().Name+"."+property.Name,40515);}
        }
    }
    private static bool Active(object?value){
        if(value is null){return false;}
        if(value is bool flag){return flag;}
        if(value is Enum enumeration){return Convert.ToInt64(enumeration,CultureInfo.InvariantCulture)!=0;}
        if(value is string text){return text.Length>0;}
        if(value is IEnumerable items){var iterator=items.GetEnumerator();try{return iterator.MoveNext();}finally{(iterator as IDisposable)?.Dispose();}}
        if(value.GetType().IsValueType){return !value.Equals(Activator.CreateInstance(value.GetType()));}
        return true;
    }
    private static SqlQueryException Unsupported(object value)=>new("Unsupported basic SQL syntax: "+value.GetType().Name,40515);
}
