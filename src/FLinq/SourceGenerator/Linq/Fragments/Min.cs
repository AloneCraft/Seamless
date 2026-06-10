namespace Tsavorite.SourceGenerator.Linq.Fragments;

public class Min(int 番号, Enumerable source, string resultType) : MinMaxBase(番号, source, resultType, "less") { }
public class Min_selector(int 番号, Enumerable source, FuncFragment selector, string resultType) : MinMaxSelectorBase(番号, source, selector, resultType, "less") { }
