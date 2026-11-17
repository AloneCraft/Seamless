using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Seamless.FLinq.Generator;

internal interface ConsumeDirectExecutionFragment {
    string GetConsumeExecutionStatement(string consumerName);
}
