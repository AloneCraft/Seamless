namespace Seamless.Database.Query;

public enum SqlDialect{SqlServer,MySql,PostgreSql}

internal enum SqlStatementKind{Select}
internal enum SqlLiteralKind{Null,Integer,Decimal,Real,String,NationalString,Boolean}
internal enum SqlSyntaxBinaryOperator{Add,Subtract,Multiply,Divide,Modulo,Equal,NotEqual,Greater,Less,GreaterOrEqual,LessOrEqual,And,Or}
internal enum SqlSyntaxUnaryOperator{Positive,Negative,Not}
internal enum SqlSyntaxJoinKind{Inner,Left}
internal enum SqlParameterStyle{Named,QuestionMark,DollarNumber}

internal readonly record struct SqlSourceSpan(int Start,int Length,int Line,int Column);
internal sealed record SqlIdentifierSyntax(string Value,char?Quote);
internal sealed record SqlNameSyntax(IReadOnlyList<SqlIdentifierSyntax>Parts);
internal sealed record SqlTypeSyntax(string Name,int?Length=null,byte?Precision=null,byte?Scale=null);
internal sealed record SqlDocumentSyntax(SqlDialect Dialect,string Source,IReadOnlyList<SqlStatementSyntax>Statements);
internal abstract record SqlStatementSyntax(SqlStatementKind Kind){internal SqlSourceSpan?Span{get;init;}}
internal sealed record SqlSelectStatementSyntax(SqlSelectSyntax Query):SqlStatementSyntax(SqlStatementKind.Select);
internal sealed record SqlSelectSyntax(
    IReadOnlyList<SqlProjectionSyntax>Projection,
    SqlSourceSyntax?From,
    SqlExpressionSyntax?Where,
    IReadOnlyList<SqlExpressionSyntax>GroupBy,
    SqlExpressionSyntax?Having,
    IReadOnlyList<SqlOrderSyntax>OrderBy,
    bool Distinct,
    SqlExpressionSyntax?Limit);
internal sealed record SqlProjectionSyntax(SqlExpressionSyntax?Expression,SqlIdentifierSyntax?Alias=null,SqlNameSyntax?StarQualifier=null){internal bool IsStar=>this.Expression is null;}
internal sealed record SqlOrderSyntax(SqlExpressionSyntax Expression,bool Descending,bool?NullsFirst);
internal abstract record SqlSourceSyntax;
internal sealed record SqlTableSyntax(SqlNameSyntax Name,SqlIdentifierSyntax?Alias):SqlSourceSyntax;
internal sealed record SqlJoinSyntax(SqlSourceSyntax Left,SqlSourceSyntax Right,SqlSyntaxJoinKind Kind,SqlExpressionSyntax Condition):SqlSourceSyntax;
internal abstract record SqlExpressionSyntax;
internal sealed record SqlLiteralSyntax(SqlLiteralKind Kind,string?Text):SqlExpressionSyntax;
internal sealed record SqlColumnSyntax(SqlNameSyntax Name):SqlExpressionSyntax;
internal sealed record SqlParameterSyntax(string Name,SqlParameterStyle Style,int?Position=null):SqlExpressionSyntax;
internal sealed record SqlBinarySyntax(SqlExpressionSyntax Left,SqlSyntaxBinaryOperator Operator,SqlExpressionSyntax Right):SqlExpressionSyntax;
internal sealed record SqlUnarySyntax(SqlSyntaxUnaryOperator Operator,SqlExpressionSyntax Value):SqlExpressionSyntax;
internal sealed record SqlIsNullSyntax(SqlExpressionSyntax Value,bool Negate):SqlExpressionSyntax;
internal sealed record SqlCaseArmSyntax(SqlExpressionSyntax When,SqlExpressionSyntax Then);
internal sealed record SqlCaseSyntax(SqlExpressionSyntax?Operand,IReadOnlyList<SqlCaseArmSyntax>Arms,SqlExpressionSyntax?Else):SqlExpressionSyntax;
internal sealed record SqlCastSyntax(SqlExpressionSyntax Value,SqlTypeSyntax Type):SqlExpressionSyntax;
internal sealed record SqlFunctionSyntax(string Name,IReadOnlyList<SqlExpressionSyntax>Arguments,bool Distinct,bool Star):SqlExpressionSyntax;

internal interface ISqlSyntaxParser{
    SqlDocumentSyntax Parse(string sql,SqlDialect dialect);
}
