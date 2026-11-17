using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Seamless.FLinq.Generator;

internal interface DirectExecutionFragment {
    string GetDirectExecutionStatement(string actualVariableName,string actualTypeName);
}
