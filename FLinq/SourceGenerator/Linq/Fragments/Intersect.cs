namespace Seamless.FLinq.Linq.Fragments;

public class Intersect(int 番号,Enumerable source,Enumerable second):Intersect_comparer(番号,source,second,"null");

public class Intersect_comparer(int 番号,Enumerable source,Enumerable second,string comparer):SetBase(番号,source,second,comparer,SetOperationStrategies.Intersect);
