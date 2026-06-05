namespace Seamless.SourceGenerator.Linq.Fragments;

public class Distinct(int 番号,Enumerable source):Distinct_comparer(番号,source,"null");

public class Distinct_comparer(int 番号,Enumerable source,string comparer):DistinctBase(番号,source,comparer);
