using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Seamless.FLinq.Generator;

internal class MaxBy_keySelector(int 番号,Enumerable source,FuncFragment keySelector,string resultType):MinMaxByBase(番号,source,keySelector,resultType,"greater");
