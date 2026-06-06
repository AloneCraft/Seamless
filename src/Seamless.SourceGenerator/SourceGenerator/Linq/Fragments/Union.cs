namespace Seamless.SourceGenerator.Linq.Fragments;

public class Union(int 番号,Enumerable source,Enumerable second):Union_comparer(番号,source,second,"null");

public class Union_comparer(int 番号,Enumerable source,Enumerable second,string comparer):SetBase(番号,source,second,comparer,SetOperationStrategies.Union);
