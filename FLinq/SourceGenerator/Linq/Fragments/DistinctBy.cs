namespace Seamless.FLinq.Linq.Fragments;

internal class DistinctBy(int 番号,Enumerable source,FuncFragment keySelector):DistinctBy_comparer(番号,source,keySelector,"null");

internal class DistinctBy_comparer(int 番号,Enumerable source,FuncFragment keySelector,string comparer):DistinctByBase(番号,source,keySelector,comparer);
