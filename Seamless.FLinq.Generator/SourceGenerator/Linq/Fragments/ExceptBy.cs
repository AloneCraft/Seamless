namespace Seamless.FLinq;

internal class ExceptBy(int 番号,Enumerable source,Enumerable second,FuncFragment keySelector):ExceptBy_comparer(番号,source,second,keySelector,"null");

internal class ExceptBy_comparer(int 番号,Enumerable source,Enumerable second,FuncFragment keySelector,string comparer):SetByBase(番号,source,second,keySelector,comparer,SetOperationStrategies.ByExcept);
