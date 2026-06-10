namespace Tsavorite.SourceGenerator.Linq.Fragments;

public class Max(int 番号, Enumerable source, string resultType) : MinMaxBase(番号, source, resultType, "greater") { }
public class Max_selector(int 番号, Enumerable source, FuncFragment selector, string resultType) : MinMaxSelectorBase(番号, source, selector, resultType, "greater") { }
