namespace Seamless.FLinq.Linq.Fragments;

public class ThenBy(int 番号, Enumerable source, FuncFragment keySelector) : OrderBy_comparer(番号, source, keySelector, "null") { }
public class ThenByDescending(int 番号, Enumerable source, FuncFragment keySelector) : OrderByDescending_comparer(番号, source, keySelector, "null") { }
public class ThenBy_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : OrderBy_comparer(番号, source, keySelector, comparer) { }
public class ThenByDescending_comparer(int 番号, Enumerable source, FuncFragment keySelector, string comparer) : OrderByDescending_comparer(番号, source, keySelector, comparer) { }
