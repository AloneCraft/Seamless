namespace Seamless.FLinq.Linq.Fragments;

internal class UnionBy(int 番号,Enumerable source,Enumerable second,FuncFragment keySelector):UnionBy_comparer(番号,source,second,keySelector,"null") { }

internal class UnionBy_comparer(int 番号,Enumerable source,Enumerable second,FuncFragment keySelector,string comparer):SetByBase(番号,source,second,keySelector,comparer,SetOperationStrategies.ByUnion) { }
