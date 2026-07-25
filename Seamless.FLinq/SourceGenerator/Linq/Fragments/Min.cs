namespace Seamless.FLinq.Linq.Fragments;

internal class Min(int 番号,Enumerable source,string resultType):MinMaxBase(番号,source,resultType,"less") { }
internal class Min_selector(int 番号,Enumerable source,FuncFragment selector,string resultType):MinMaxSelectorBase(番号,source,selector,resultType,"less") { }
