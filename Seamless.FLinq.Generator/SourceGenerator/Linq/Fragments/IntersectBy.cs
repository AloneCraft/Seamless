namespace Seamless.FLinq.Generator;

internal class IntersectBy(int 番号,Enumerable source,Enumerable second,FuncFragment keySelector):IntersectBy_comparer(番号,source,second,keySelector,"null") { }

internal class IntersectBy_comparer(int 番号,Enumerable source,Enumerable second,FuncFragment keySelector,string comparer):SetByBase(番号,source,second,keySelector,comparer,SetOperationStrategies.ByIntersect) { }
