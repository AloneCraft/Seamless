namespace Seamless.FLinq.Generator;

internal class LongCount(int 番号,Enumerable source,string resultType):Count(番号,source,resultType);

internal class LongCount_predicate(int 番号,Enumerable source,FuncFragment predicate,string resultType):Count_predicate(番号,source,predicate,resultType);
