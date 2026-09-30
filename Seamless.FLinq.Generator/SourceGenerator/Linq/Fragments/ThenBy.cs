namespace Seamless.FLinq.Generator;

internal class ThenBy(int 番号,Enumerable source,FuncFragment keySelector):ThenBy_comparer(番号,source,keySelector,"null") { }
internal class ThenByDescending(int 番号,Enumerable source,FuncFragment keySelector):ThenByDescending_comparer(番号,source,keySelector,"null") { }
internal class ThenBy_comparer(int 番号,Enumerable source,FuncFragment keySelector,string comparer):OrderingBase(番号,source,keySelector,comparer,descending: false,isThenBy: true) { }
internal class ThenByDescending_comparer(int 番号,Enumerable source,FuncFragment keySelector,string comparer):OrderingBase(番号,source,keySelector,comparer,descending: true,isThenBy: true) { }
