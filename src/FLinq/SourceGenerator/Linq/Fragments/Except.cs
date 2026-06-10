namespace Tsavorite.SourceGenerator.Linq.Fragments;

public class Except(int 番号,Enumerable source,Enumerable second):Except_comparer(番号,source,second,"null");

public class Except_comparer(int 番号,Enumerable source,Enumerable second,string comparer):SetBase(番号,source,second,comparer,SetOperationStrategies.Except);
