using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Seamless.Database.Rdbms;

public enum NativeDiagnosticSeverity{Warning,Unsupported,CompilationError}
public sealed record NativeSourceLocation(string Path,int Start,int Length,int Line,int Column);
public sealed record NativeDiagnostic(string Code,string Message,NativeDiagnosticSeverity Severity,string?ObjectName,NativeSourceLocation?Location);
public sealed record NativeColumn(string Name,string ClrType,bool IsNullable,int?TextCapacity=null){
    public string MemberName{get;init;}=Name;
    public int?LogicalMaximumLength{get;init;}
    public string?SourceSqlTypeName{get;init;}
    public string?SourceCollation{get;init;}
}
public sealed record NativeTable(string Schema,string Name,string PropertyName,ImmutableArray<NativeColumn>Columns,ImmutableArray<string>PrimaryKey){
    public string?SourceFeatures{get;init;}
}
public sealed record NativeParameter(string Name,string ClrType,bool IsNullable);
public sealed record NativeDatabaseDefinition(string Name,string SourceFingerprint,ImmutableArray<NativeTable>Tables,ImmutableArray<NativeQueryDefinition>Queries);
public sealed record NativeAnalysisResult(NativeDatabaseDefinition Database,ImmutableArray<NativeDiagnostic>Diagnostics);
public sealed record NativeQueryDefinition(string Name,string Schema,ImmutableArray<NativeParameter>Parameters,NativeQuery Query,NativeSourceLocation Source);

[JsonPolymorphic(TypeDiscriminatorPropertyName="Kind")]
[JsonDerivedType(typeof(NativeColumnExpression),"Column")]
[JsonDerivedType(typeof(NativeParameterExpression),"Parameter")]
[JsonDerivedType(typeof(NativeConstantExpression),"Constant")]
[JsonDerivedType(typeof(NativeBinaryExpression),"Binary")]
[JsonDerivedType(typeof(NativeUnaryExpression),"Unary")]
public abstract record NativeExpression;
public sealed record NativeColumnExpression(string Name):NativeExpression;
public sealed record NativeParameterExpression(string Name):NativeExpression;
public sealed record NativeConstantExpression(object?Value):NativeExpression;
public sealed record NativeBinaryExpression(string Operator,NativeExpression Left,NativeExpression Right):NativeExpression{
    public bool OrdinalText{get;init;}
}
public sealed record NativeUnaryExpression(string Operator,NativeExpression Operand):NativeExpression;
public sealed record NativeProjection(string Name,NativeExpression Expression);
public sealed record NativeOrdering(NativeExpression Expression,bool Descending);
public sealed record NativeQuery(NativeTable Table,ImmutableArray<NativeQueryStage>Stages);
[JsonPolymorphic(TypeDiscriminatorPropertyName="Kind")]
[JsonDerivedType(typeof(NativeFilter),"Filter")]
[JsonDerivedType(typeof(NativeSelect),"Select")]
[JsonDerivedType(typeof(NativeOrder),"Order")]
[JsonDerivedType(typeof(NativeTake),"Take")]
[JsonDerivedType(typeof(NativeDistinct),"Distinct")]
[JsonDerivedType(typeof(NativeAggregate),"Aggregate")]
public abstract record NativeQueryStage;
public sealed record NativeFilter(NativeExpression Predicate):NativeQueryStage;
public sealed record NativeSelect(ImmutableArray<NativeProjection>Columns):NativeQueryStage;
public sealed record NativeOrder(NativeOrdering Ordering,bool ThenBy):NativeQueryStage;
public sealed record NativeTake(int Count):NativeQueryStage;
public sealed record NativeDistinct:NativeQueryStage;
public sealed record NativeAggregate(string Operator,NativeExpression?Expression):NativeQueryStage;
public enum SqlVendor{SqlServer,MySql}
public sealed record GeneratedVendorQuery(string Name,string Sql,ImmutableArray<NativeParameter>Parameters);
public sealed record GeneratedVendorDatabase(SqlVendor Vendor,string SourceFingerprint,string SchemaSql,ImmutableArray<GeneratedVendorQuery>Queries,ImmutableArray<NativeDiagnostic>Diagnostics){
    public ImmutableArray<string>Statements{get;init;}=[];
}
