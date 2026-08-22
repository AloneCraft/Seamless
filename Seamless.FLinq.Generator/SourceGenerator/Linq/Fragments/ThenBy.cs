namespace Seamless.FLinq.Generator;

internal class ThenBy(int 番号,Enumerable source,FuncFragment keySelector):OrderBy_comparer(番号,source,keySelector,"null") { }
internal class ThenByDescending(int 番号,Enumerable source,FuncFragment keySelector):OrderByDescending_comparer(番号,source,keySelector,"null") { }
internal class ThenBy_comparer(int 番号,Enumerable source,FuncFragment keySelector,string comparer):OrderBy_comparer(番号,source,keySelector,comparer) { }
internal class ThenByDescending_comparer(int 番号,Enumerable source,FuncFragment keySelector,string comparer):OrderByDescending_comparer(番号,source,keySelector,comparer) { }
