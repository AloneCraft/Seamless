namespace Seamless.SourceGenerator.Linq.Fragments;

internal class MaxBy_keySelector(int 番号,Enumerable source,FuncFragment keySelector,string resultType):MinMaxByBase(番号,source,keySelector,resultType,"greater");
