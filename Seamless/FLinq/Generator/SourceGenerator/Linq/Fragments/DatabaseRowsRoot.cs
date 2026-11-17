using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Seamless.FLinq.Generator;

internal sealed class DatabaseRowsRoot(int number,string sourceExpression,string elementType,string enumeratorTypeName)
    :TransactionRowsRoot(number,sourceExpression,elementType,enumeratorTypeName){
    public override string ProviderName=>"DatabaseRows";
    public override string SourceVariablePrefix=>"__databaseRowsSource";
    public override string? ScanCountMethodName=>"GetLongCount";
    internal override TransactionRowsRoot WithSource(int number,string expression,string elementType,string enumeratorTypeName)=>new DatabaseRowsRoot(number,expression,elementType,enumeratorTypeName);
}
