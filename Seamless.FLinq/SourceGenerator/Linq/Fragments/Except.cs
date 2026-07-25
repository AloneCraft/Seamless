namespace Seamless.FLinq.Linq.Fragments;

internal class Except(int 番号,Enumerable source,Enumerable second):Except_comparer(番号,source,second,"null");

internal class Except_comparer(int 番号,Enumerable source,Enumerable second,string comparer):SetBase(番号,source,second,comparer,SetOperationStrategies.Except);
