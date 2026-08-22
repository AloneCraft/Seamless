namespace Seamless.FLinq.Generator;

internal interface IProviderNativeBoundArguments {
    int Number { get; }
    Enumerable Source { get; }
    FuncFragment? FirstFunction { get; }
    IReadOnlyList<object?> ProviderValues { get; }
}

internal sealed class Aggregate_seed_func_resultSelectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Aggregate_seed_func_resultSelectorArguments(int number, Enumerable value0, string value1, FuncFragment value2, FuncFragment value3) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
        this.Value3=value3;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal FuncFragment Value3 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        2 => this.Value2,
        3 => this.Value3,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value2;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2, this.Value3 };

    internal static Aggregate_seed_func_resultSelectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=4) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        if(!arguments.TryGetFunction(3,out var value3)) return null;
        return new Aggregate_seed_func_resultSelectorArguments(arguments.Number, value0, value1, value2, value3);
    }
}

internal sealed class Aggregate_seed_funcArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Aggregate_seed_funcArguments(int number, Enumerable value0, string value1, FuncFragment value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value2;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static Aggregate_seed_funcArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        return new Aggregate_seed_funcArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class Aggregate_funcArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Aggregate_funcArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static Aggregate_funcArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new Aggregate_funcArguments(arguments.Number, value0, value1);
    }
}

internal sealed class AggregateByArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal AggregateByArguments(int number, Enumerable value0, FuncFragment value1, string value2, FuncFragment value3, string value4) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
        this.Value3=value3;
        this.Value4=value4;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal FuncFragment Value3 { get; }
    internal string Value4 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        3 => this.Value3,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        4 => this.Value4,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2, this.Value3, this.Value4 };

    internal static AggregateByArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=5) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        if(!arguments.TryGetFunction(3,out var value3)) return null;
        if(!arguments.TryGetExpression(4,out var value4)) return null;
        return new AggregateByArguments(arguments.Number, value0, value1, value2, value3, value4);
    }
}

internal sealed class All_predicateArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal All_predicateArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static All_predicateArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new All_predicateArguments(arguments.Number, value0, value1);
    }
}

internal sealed class AnonymousExitToListArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal AnonymousExitToListArguments(int number, Enumerable value0) {
        this.Number=number;
        this.Value0=value0;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0 };

    internal static AnonymousExitToListArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=1) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        return new AnonymousExitToListArguments(arguments.Number, value0);
    }
}

internal sealed class Any_indexPredicateArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Any_indexPredicateArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static Any_indexPredicateArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new Any_indexPredicateArguments(arguments.Number, value0, value1);
    }
}

internal sealed class Any_predicateArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Any_predicateArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static Any_predicateArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new Any_predicateArguments(arguments.Number, value0, value1);
    }
}

internal sealed class AnyArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal AnyArguments(int number, Enumerable value0) {
        this.Number=number;
        this.Value0=value0;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0 };

    internal static AnyArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=1) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        return new AnyArguments(arguments.Number, value0);
    }
}

internal sealed class AppendArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal AppendArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static AppendArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new AppendArguments(arguments.Number, value0, value1);
    }
}

internal sealed class AsEnumerableArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal AsEnumerableArguments(int number, Enumerable value0) {
        this.Number=number;
        this.Value0=value0;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0 };

    internal static AsEnumerableArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=1) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        return new AsEnumerableArguments(arguments.Number, value0);
    }
}

internal sealed class Average_selectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Average_selectorArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static Average_selectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new Average_selectorArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class AverageArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal AverageArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static AverageArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new AverageArguments(arguments.Number, value0, value1);
    }
}

internal sealed class CastArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal CastArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static CastArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new CastArguments(arguments.Number, value0, value1);
    }
}

internal sealed class ChunkArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ChunkArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static ChunkArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new ChunkArguments(arguments.Number, value0, value1);
    }
}

internal sealed class ConcatArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ConcatArguments(int number, Enumerable value0, Enumerable value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static ConcatArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        return new ConcatArguments(arguments.Number, value0, value1);
    }
}

internal sealed class Contains_comparerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Contains_comparerArguments(int number, Enumerable value0, string value1, string value2, string value3) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
        this.Value3=value3;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal string Value2 { get; }
    internal string Value3 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        2 => this.Value2,
        3 => this.Value3,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2, this.Value3 };

    internal static Contains_comparerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=4) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        if(!arguments.TryGetExpression(3,out var value3)) return null;
        return new Contains_comparerArguments(arguments.Number, value0, value1, value2, value3);
    }
}

internal sealed class ContainsArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ContainsArguments(int number, Enumerable value0, string value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static ContainsArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new ContainsArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class Count_predicateArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Count_predicateArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static Count_predicateArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new Count_predicateArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class CountArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal CountArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static CountArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new CountArguments(arguments.Number, value0, value1);
    }
}

internal sealed class CountByArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal CountByArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static CountByArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new CountByArguments(arguments.Number, value0, value1);
    }
}

internal sealed class DefaultIfEmpty_defaultValueArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal DefaultIfEmpty_defaultValueArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static DefaultIfEmpty_defaultValueArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new DefaultIfEmpty_defaultValueArguments(arguments.Number, value0, value1);
    }
}

internal sealed class DefaultIfEmptyArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal DefaultIfEmptyArguments(int number, Enumerable value0) {
        this.Number=number;
        this.Value0=value0;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0 };

    internal static DefaultIfEmptyArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=1) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        return new DefaultIfEmptyArguments(arguments.Number, value0);
    }
}

internal sealed class Distinct_comparerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Distinct_comparerArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static Distinct_comparerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new Distinct_comparerArguments(arguments.Number, value0, value1);
    }
}

internal sealed class DistinctArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal DistinctArguments(int number, Enumerable value0) {
        this.Number=number;
        this.Value0=value0;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0 };

    internal static DistinctArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=1) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        return new DistinctArguments(arguments.Number, value0);
    }
}

internal sealed class DistinctBy_comparerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal DistinctBy_comparerArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static DistinctBy_comparerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new DistinctBy_comparerArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class DistinctByArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal DistinctByArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static DistinctByArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new DistinctByArguments(arguments.Number, value0, value1);
    }
}

internal sealed class ElementAtArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ElementAtArguments(int number, Enumerable value0, string value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static ElementAtArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new ElementAtArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class ElementAt_indexArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ElementAt_indexArguments(int number, Enumerable value0, string value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static ElementAt_indexArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new ElementAt_indexArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class ElementAtOrDefaultArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ElementAtOrDefaultArguments(int number, Enumerable value0, string value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static ElementAtOrDefaultArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new ElementAtOrDefaultArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class ElementAtOrDefault_indexArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ElementAtOrDefault_indexArguments(int number, Enumerable value0, string value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static ElementAtOrDefault_indexArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new ElementAtOrDefault_indexArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class Except_comparerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Except_comparerArguments(int number, Enumerable value0, Enumerable value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static Except_comparerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new Except_comparerArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class ExceptArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ExceptArguments(int number, Enumerable value0, Enumerable value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static ExceptArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        return new ExceptArguments(arguments.Number, value0, value1);
    }
}

internal sealed class ExceptBy_comparerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ExceptBy_comparerArguments(int number, Enumerable value0, Enumerable value1, FuncFragment value2, string value3) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
        this.Value3=value3;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal string Value3 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        3 => this.Value3,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value2;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2, this.Value3 };

    internal static ExceptBy_comparerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=4) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        if(!arguments.TryGetExpression(3,out var value3)) return null;
        return new ExceptBy_comparerArguments(arguments.Number, value0, value1, value2, value3);
    }
}

internal sealed class ExceptByArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ExceptByArguments(int number, Enumerable value0, Enumerable value1, FuncFragment value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value2;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static ExceptByArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        return new ExceptByArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class First_predicateArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal First_predicateArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static First_predicateArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new First_predicateArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class FirstArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal FirstArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static FirstArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new FirstArguments(arguments.Number, value0, value1);
    }
}

internal sealed class FirstOrDefault_defaultValueArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal FirstOrDefault_defaultValueArguments(int number, Enumerable value0, string value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static FirstOrDefault_defaultValueArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new FirstOrDefault_defaultValueArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class FirstOrDefault_predicateArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal FirstOrDefault_predicateArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static FirstOrDefault_predicateArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new FirstOrDefault_predicateArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class FirstOrDefaultArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal FirstOrDefaultArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static FirstOrDefaultArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new FirstOrDefaultArguments(arguments.Number, value0, value1);
    }
}

internal sealed class GroupBy_keySelector_elementSelector_resultSelectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal GroupBy_keySelector_elementSelector_resultSelectorArguments(int number, Enumerable value0, FuncFragment value1, FuncFragment value2, FuncFragment value3) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
        this.Value3=value3;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal FuncFragment Value3 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        2 => this.Value2,
        3 => this.Value3,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2, this.Value3 };

    internal static GroupBy_keySelector_elementSelector_resultSelectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=4) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        if(!arguments.TryGetFunction(3,out var value3)) return null;
        return new GroupBy_keySelector_elementSelector_resultSelectorArguments(arguments.Number, value0, value1, value2, value3);
    }
}

internal sealed class GroupBy_keySelector_elementSelectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal GroupBy_keySelector_elementSelectorArguments(int number, Enumerable value0, FuncFragment value1, FuncFragment value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static GroupBy_keySelector_elementSelectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        return new GroupBy_keySelector_elementSelectorArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class GroupBy_keySelector_resultSelectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal GroupBy_keySelector_resultSelectorArguments(int number, Enumerable value0, FuncFragment value1, FuncFragment value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static GroupBy_keySelector_resultSelectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        return new GroupBy_keySelector_resultSelectorArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class GroupBy_keySelectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal GroupBy_keySelectorArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static GroupBy_keySelectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new GroupBy_keySelectorArguments(arguments.Number, value0, value1);
    }
}

internal sealed class GroupJoinArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal GroupJoinArguments(int number, Enumerable value0, Enumerable value1, FuncFragment value2, FuncFragment value3, FuncFragment value4, string value5) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
        this.Value3=value3;
        this.Value4=value4;
        this.Value5=value5;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal FuncFragment Value3 { get; }
    internal FuncFragment Value4 { get; }
    internal string Value5 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        2 => this.Value2,
        3 => this.Value3,
        4 => this.Value4,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        5 => this.Value5,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value2;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2, this.Value3, this.Value4, this.Value5 };

    internal static GroupJoinArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=6) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        if(!arguments.TryGetFunction(3,out var value3)) return null;
        if(!arguments.TryGetFunction(4,out var value4)) return null;
        if(!arguments.TryGetExpression(5,out var value5)) return null;
        return new GroupJoinArguments(arguments.Number, value0, value1, value2, value3, value4, value5);
    }
}

internal sealed class IndexArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal IndexArguments(int number, Enumerable value0) {
        this.Number=number;
        this.Value0=value0;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0 };

    internal static IndexArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=1) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        return new IndexArguments(arguments.Number, value0);
    }
}

internal sealed class Intersect_comparerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Intersect_comparerArguments(int number, Enumerable value0, Enumerable value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static Intersect_comparerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new Intersect_comparerArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class IntersectArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal IntersectArguments(int number, Enumerable value0, Enumerable value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static IntersectArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        return new IntersectArguments(arguments.Number, value0, value1);
    }
}

internal sealed class IntersectBy_comparerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal IntersectBy_comparerArguments(int number, Enumerable value0, Enumerable value1, FuncFragment value2, string value3) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
        this.Value3=value3;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal string Value3 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        3 => this.Value3,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value2;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2, this.Value3 };

    internal static IntersectBy_comparerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=4) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        if(!arguments.TryGetExpression(3,out var value3)) return null;
        return new IntersectBy_comparerArguments(arguments.Number, value0, value1, value2, value3);
    }
}

internal sealed class IntersectByArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal IntersectByArguments(int number, Enumerable value0, Enumerable value1, FuncFragment value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value2;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static IntersectByArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        return new IntersectByArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class JoinArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal JoinArguments(int number, Enumerable value0, Enumerable value1, FuncFragment value2, FuncFragment value3, FuncFragment value4, string value5) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
        this.Value3=value3;
        this.Value4=value4;
        this.Value5=value5;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal FuncFragment Value3 { get; }
    internal FuncFragment Value4 { get; }
    internal string Value5 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        2 => this.Value2,
        3 => this.Value3,
        4 => this.Value4,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        5 => this.Value5,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value2;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2, this.Value3, this.Value4, this.Value5 };

    internal static JoinArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=6) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        if(!arguments.TryGetFunction(3,out var value3)) return null;
        if(!arguments.TryGetFunction(4,out var value4)) return null;
        if(!arguments.TryGetExpression(5,out var value5)) return null;
        return new JoinArguments(arguments.Number, value0, value1, value2, value3, value4, value5);
    }
}

internal sealed class Last_predicateArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Last_predicateArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static Last_predicateArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new Last_predicateArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class LastArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal LastArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static LastArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new LastArguments(arguments.Number, value0, value1);
    }
}

internal sealed class LastOrDefault_defaultValueArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal LastOrDefault_defaultValueArguments(int number, Enumerable value0, string value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static LastOrDefault_defaultValueArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new LastOrDefault_defaultValueArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class LastOrDefault_predicateArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal LastOrDefault_predicateArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static LastOrDefault_predicateArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new LastOrDefault_predicateArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class LastOrDefaultArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal LastOrDefaultArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static LastOrDefaultArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new LastOrDefaultArguments(arguments.Number, value0, value1);
    }
}

internal sealed class LeftJoinArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal LeftJoinArguments(int number, Enumerable value0, Enumerable value1, FuncFragment value2, FuncFragment value3, FuncFragment value4, string value5) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
        this.Value3=value3;
        this.Value4=value4;
        this.Value5=value5;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal FuncFragment Value3 { get; }
    internal FuncFragment Value4 { get; }
    internal string Value5 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        2 => this.Value2,
        3 => this.Value3,
        4 => this.Value4,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        5 => this.Value5,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value2;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2, this.Value3, this.Value4, this.Value5 };

    internal static LeftJoinArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=6) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        if(!arguments.TryGetFunction(3,out var value3)) return null;
        if(!arguments.TryGetFunction(4,out var value4)) return null;
        if(!arguments.TryGetExpression(5,out var value5)) return null;
        return new LeftJoinArguments(arguments.Number, value0, value1, value2, value3, value4, value5);
    }
}

internal sealed class LongCount_predicateArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal LongCount_predicateArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static LongCount_predicateArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new LongCount_predicateArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class LongCountArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal LongCountArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static LongCountArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new LongCountArguments(arguments.Number, value0, value1);
    }
}

internal sealed class Max_selectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Max_selectorArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static Max_selectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new Max_selectorArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class MaxArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal MaxArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static MaxArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new MaxArguments(arguments.Number, value0, value1);
    }
}

internal sealed class MaxBy_keySelectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal MaxBy_keySelectorArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static MaxBy_keySelectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new MaxBy_keySelectorArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class Min_selectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Min_selectorArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static Min_selectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new Min_selectorArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class MinArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal MinArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static MinArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new MinArguments(arguments.Number, value0, value1);
    }
}

internal sealed class MinBy_keySelectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal MinBy_keySelectorArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static MinBy_keySelectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new MinBy_keySelectorArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class OfTypeArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal OfTypeArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static OfTypeArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new OfTypeArguments(arguments.Number, value0, value1);
    }
}

internal sealed class OrderBy_comparerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal OrderBy_comparerArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static OrderBy_comparerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new OrderBy_comparerArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class OrderByArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal OrderByArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static OrderByArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new OrderByArguments(arguments.Number, value0, value1);
    }
}

internal sealed class OrderByDescending_comparerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal OrderByDescending_comparerArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static OrderByDescending_comparerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new OrderByDescending_comparerArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class OrderByDescendingArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal OrderByDescendingArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static OrderByDescendingArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new OrderByDescendingArguments(arguments.Number, value0, value1);
    }
}

internal sealed class PrependArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal PrependArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static PrependArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new PrependArguments(arguments.Number, value0, value1);
    }
}

internal sealed class ReverseArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ReverseArguments(int number, Enumerable value0) {
        this.Number=number;
        this.Value0=value0;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0 };

    internal static ReverseArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=1) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        return new ReverseArguments(arguments.Number, value0);
    }
}

internal sealed class RightJoinArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal RightJoinArguments(int number, Enumerable value0, Enumerable value1, FuncFragment value2, FuncFragment value3, FuncFragment value4, string value5) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
        this.Value3=value3;
        this.Value4=value4;
        this.Value5=value5;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal FuncFragment Value3 { get; }
    internal FuncFragment Value4 { get; }
    internal string Value5 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        2 => this.Value2,
        3 => this.Value3,
        4 => this.Value4,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        5 => this.Value5,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value2;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2, this.Value3, this.Value4, this.Value5 };

    internal static RightJoinArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=6) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        if(!arguments.TryGetFunction(3,out var value3)) return null;
        if(!arguments.TryGetFunction(4,out var value4)) return null;
        if(!arguments.TryGetExpression(5,out var value5)) return null;
        return new RightJoinArguments(arguments.Number, value0, value1, value2, value3, value4, value5);
    }
}

internal sealed class Select_indexSelectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Select_indexSelectorArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static Select_indexSelectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new Select_indexSelectorArguments(arguments.Number, value0, value1);
    }
}

internal sealed class Select_selectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Select_selectorArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static Select_selectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new Select_selectorArguments(arguments.Number, value0, value1);
    }
}

internal sealed class SelectMany_indexSelector_resultSelectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal SelectMany_indexSelector_resultSelectorArguments(int number, Enumerable value0, FuncFragment value1, FuncFragment value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static SelectMany_indexSelector_resultSelectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        return new SelectMany_indexSelector_resultSelectorArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class SelectMany_selector_resultSelectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal SelectMany_selector_resultSelectorArguments(int number, Enumerable value0, FuncFragment value1, FuncFragment value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static SelectMany_selector_resultSelectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        return new SelectMany_selector_resultSelectorArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class SelectMany_WithInlinedInnerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal SelectMany_WithInlinedInnerArguments(int number, Enumerable value0, Enumerable value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static SelectMany_WithInlinedInnerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new SelectMany_WithInlinedInnerArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class SelectMany_indexSelectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal SelectMany_indexSelectorArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static SelectMany_indexSelectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new SelectMany_indexSelectorArguments(arguments.Number, value0, value1);
    }
}

internal sealed class SelectMany_selectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal SelectMany_selectorArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static SelectMany_selectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new SelectMany_selectorArguments(arguments.Number, value0, value1);
    }
}

internal sealed class SequenceEqual_comparerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal SequenceEqual_comparerArguments(int number, Enumerable value0, Enumerable value1, string value2, string value3) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
        this.Value3=value3;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal string Value2 { get; }
    internal string Value3 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        3 => this.Value3,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2, this.Value3 };

    internal static SequenceEqual_comparerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=4) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        if(!arguments.TryGetExpression(3,out var value3)) return null;
        return new SequenceEqual_comparerArguments(arguments.Number, value0, value1, value2, value3);
    }
}

internal sealed class SequenceEqualArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal SequenceEqualArguments(int number, Enumerable value0, Enumerable value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static SequenceEqualArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new SequenceEqualArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class ShuffleArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ShuffleArguments(int number, Enumerable value0) {
        this.Number=number;
        this.Value0=value0;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0 };

    internal static ShuffleArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=1) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        return new ShuffleArguments(arguments.Number, value0);
    }
}

internal sealed class Single_predicateArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Single_predicateArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static Single_predicateArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new Single_predicateArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class SingleArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal SingleArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static SingleArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new SingleArguments(arguments.Number, value0, value1);
    }
}

internal sealed class SingleOrDefault_defaultValueArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal SingleOrDefault_defaultValueArguments(int number, Enumerable value0, string value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static SingleOrDefault_defaultValueArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new SingleOrDefault_defaultValueArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class SingleOrDefault_predicateArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal SingleOrDefault_predicateArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static SingleOrDefault_predicateArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new SingleOrDefault_predicateArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class SingleOrDefaultArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal SingleOrDefaultArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static SingleOrDefaultArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new SingleOrDefaultArguments(arguments.Number, value0, value1);
    }
}

internal sealed class SkipArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal SkipArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static SkipArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new SkipArguments(arguments.Number, value0, value1);
    }
}

internal sealed class SkipLastArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal SkipLastArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static SkipLastArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new SkipLastArguments(arguments.Number, value0, value1);
    }
}

internal sealed class SkipWhile_indexPredicateArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal SkipWhile_indexPredicateArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static SkipWhile_indexPredicateArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new SkipWhile_indexPredicateArguments(arguments.Number, value0, value1);
    }
}

internal sealed class SkipWhile_predicateArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal SkipWhile_predicateArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static SkipWhile_predicateArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new SkipWhile_predicateArguments(arguments.Number, value0, value1);
    }
}

internal sealed class Sum_selectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Sum_selectorArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static Sum_selectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new Sum_selectorArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class SumArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal SumArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static SumArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new SumArguments(arguments.Number, value0, value1);
    }
}

internal sealed class TakeArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal TakeArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static TakeArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new TakeArguments(arguments.Number, value0, value1);
    }
}

internal sealed class TakeLastArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal TakeLastArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static TakeLastArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new TakeLastArguments(arguments.Number, value0, value1);
    }
}

internal sealed class TakeWhile_indexPredicateArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal TakeWhile_indexPredicateArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static TakeWhile_indexPredicateArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new TakeWhile_indexPredicateArguments(arguments.Number, value0, value1);
    }
}

internal sealed class TakeWhile_predicateArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal TakeWhile_predicateArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static TakeWhile_predicateArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new TakeWhile_predicateArguments(arguments.Number, value0, value1);
    }
}

internal sealed class ThenBy_comparerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ThenBy_comparerArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static ThenBy_comparerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new ThenBy_comparerArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class ThenByArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ThenByArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static ThenByArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new ThenByArguments(arguments.Number, value0, value1);
    }
}

internal sealed class ThenByDescending_comparerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ThenByDescending_comparerArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static ThenByDescending_comparerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new ThenByDescending_comparerArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class ThenByDescendingArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ThenByDescendingArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static ThenByDescendingArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new ThenByDescendingArguments(arguments.Number, value0, value1);
    }
}

internal sealed class ToArrayArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ToArrayArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static ToArrayArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new ToArrayArguments(arguments.Number, value0, value1);
    }
}

internal sealed class ToDictionary_keySelector_elementSelector_comparerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ToDictionary_keySelector_elementSelector_comparerArguments(int number, Enumerable value0, FuncFragment value1, FuncFragment value2, string value3, string value4) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
        this.Value3=value3;
        this.Value4=value4;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal string Value3 { get; }
    internal string Value4 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        3 => this.Value3,
        4 => this.Value4,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2, this.Value3, this.Value4 };

    internal static ToDictionary_keySelector_elementSelector_comparerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=5) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        if(!arguments.TryGetExpression(3,out var value3)) return null;
        if(!arguments.TryGetExpression(4,out var value4)) return null;
        return new ToDictionary_keySelector_elementSelector_comparerArguments(arguments.Number, value0, value1, value2, value3, value4);
    }
}

internal sealed class ToDictionary_keySelector_comparerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ToDictionary_keySelector_comparerArguments(int number, Enumerable value0, FuncFragment value1, string value2, string value3) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
        this.Value3=value3;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal string Value3 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        3 => this.Value3,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2, this.Value3 };

    internal static ToDictionary_keySelector_comparerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=4) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        if(!arguments.TryGetExpression(3,out var value3)) return null;
        return new ToDictionary_keySelector_comparerArguments(arguments.Number, value0, value1, value2, value3);
    }
}

internal sealed class ToDictionary_keySelector_elementSelectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ToDictionary_keySelector_elementSelectorArguments(int number, Enumerable value0, FuncFragment value1, FuncFragment value2, string value3) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
        this.Value3=value3;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal string Value3 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        3 => this.Value3,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2, this.Value3 };

    internal static ToDictionary_keySelector_elementSelectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=4) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        if(!arguments.TryGetExpression(3,out var value3)) return null;
        return new ToDictionary_keySelector_elementSelectorArguments(arguments.Number, value0, value1, value2, value3);
    }
}

internal sealed class ToDictionary_keySelectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ToDictionary_keySelectorArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static ToDictionary_keySelectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new ToDictionary_keySelectorArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class ToHashSet_comparerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ToHashSet_comparerArguments(int number, Enumerable value0, string value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static ToHashSet_comparerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new ToHashSet_comparerArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class ToHashSetArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ToHashSetArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static ToHashSetArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new ToHashSetArguments(arguments.Number, value0, value1);
    }
}

internal sealed class ToListArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ToListArguments(int number, Enumerable value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static ToListArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new ToListArguments(arguments.Number, value0, value1);
    }
}

internal sealed class ToLookup_keySelector_elementSelector_comparerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ToLookup_keySelector_elementSelector_comparerArguments(int number, Enumerable value0, FuncFragment value1, FuncFragment value2, string value3, string value4) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
        this.Value3=value3;
        this.Value4=value4;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal string Value3 { get; }
    internal string Value4 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        3 => this.Value3,
        4 => this.Value4,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2, this.Value3, this.Value4 };

    internal static ToLookup_keySelector_elementSelector_comparerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=5) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        if(!arguments.TryGetExpression(3,out var value3)) return null;
        if(!arguments.TryGetExpression(4,out var value4)) return null;
        return new ToLookup_keySelector_elementSelector_comparerArguments(arguments.Number, value0, value1, value2, value3, value4);
    }
}

internal sealed class ToLookup_keySelector_comparerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ToLookup_keySelector_comparerArguments(int number, Enumerable value0, FuncFragment value1, string value2, string value3) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
        this.Value3=value3;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal string Value3 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        3 => this.Value3,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2, this.Value3 };

    internal static ToLookup_keySelector_comparerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=4) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        if(!arguments.TryGetExpression(3,out var value3)) return null;
        return new ToLookup_keySelector_comparerArguments(arguments.Number, value0, value1, value2, value3);
    }
}

internal sealed class ToLookup_keySelector_elementSelectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ToLookup_keySelector_elementSelectorArguments(int number, Enumerable value0, FuncFragment value1, FuncFragment value2, string value3) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
        this.Value3=value3;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal string Value3 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        3 => this.Value3,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2, this.Value3 };

    internal static ToLookup_keySelector_elementSelectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=4) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        if(!arguments.TryGetExpression(3,out var value3)) return null;
        return new ToLookup_keySelector_elementSelectorArguments(arguments.Number, value0, value1, value2, value3);
    }
}

internal sealed class ToLookup_keySelectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ToLookup_keySelectorArguments(int number, Enumerable value0, FuncFragment value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static ToLookup_keySelectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new ToLookup_keySelectorArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class Union_comparerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Union_comparerArguments(int number, Enumerable value0, Enumerable value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static Union_comparerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new Union_comparerArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class UnionArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal UnionArguments(int number, Enumerable value0, Enumerable value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static UnionArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        return new UnionArguments(arguments.Number, value0, value1);
    }
}

internal sealed class UnionBy_comparerArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal UnionBy_comparerArguments(int number, Enumerable value0, Enumerable value1, FuncFragment value2, string value3) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
        this.Value3=value3;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal string Value3 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        3 => this.Value3,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value2;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2, this.Value3 };

    internal static UnionBy_comparerArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=4) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        if(!arguments.TryGetExpression(3,out var value3)) return null;
        return new UnionBy_comparerArguments(arguments.Number, value0, value1, value2, value3);
    }
}

internal sealed class UnionByArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal UnionByArguments(int number, Enumerable value0, Enumerable value1, FuncFragment value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value2;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static UnionByArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        return new UnionByArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class Where_indexPredicateArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Where_indexPredicateArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static Where_indexPredicateArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new Where_indexPredicateArguments(arguments.Number, value0, value1);
    }
}

internal sealed class Where_predicateArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Where_predicateArguments(int number, Enumerable value0, FuncFragment value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal FuncFragment Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value1;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static Where_predicateArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetFunction(1,out var value1)) return null;
        return new Where_predicateArguments(arguments.Number, value0, value1);
    }
}

internal sealed class Zip_resultSelectorArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Zip_resultSelectorArguments(int number, Enumerable value0, Enumerable value1, FuncFragment value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal FuncFragment Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => this.Value2;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static Zip_resultSelectorArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        if(!arguments.TryGetFunction(2,out var value2)) return null;
        return new Zip_resultSelectorArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class Zip3Arguments : FragmentArguments, IProviderNativeBoundArguments {
    internal Zip3Arguments(int number, Enumerable value0, Enumerable value1, Enumerable value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal Enumerable Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1, this.Value2 };

    internal static Zip3Arguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        if(!arguments.TryGetEnumerable(2,out var value2)) return null;
        return new Zip3Arguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class ZipArguments : FragmentArguments, IProviderNativeBoundArguments {
    internal ZipArguments(int number, Enumerable value0, Enumerable value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal Enumerable Value0 { get; }
    internal Enumerable Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    public Enumerable Source => this.Value0;
    public FuncFragment? FirstFunction => null;
    public IReadOnlyList<object?> ProviderValues => new object?[] { this.Value0, this.Value1 };

    internal static ZipArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetEnumerable(0,out var value0)) return null;
        if(!arguments.TryGetEnumerable(1,out var value1)) return null;
        return new ZipArguments(arguments.Number, value0, value1);
    }
}

internal sealed class EmptyRootArguments : FragmentArguments {
    internal EmptyRootArguments(int number, string value0) {
        this.Number=number;
        this.Value0=value0;
    }

    public int Number { get; }
    internal string Value0 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        0 => this.Value0,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    internal static EmptyRootArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=1) return null;
        if(!arguments.TryGetExpression(0,out var value0)) return null;
        return new EmptyRootArguments(arguments.Number, value0);
    }
}

internal sealed class InfiniteSequenceRootArguments : FragmentArguments {
    internal InfiniteSequenceRootArguments(int number, string value0, string value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal string Value0 { get; }
    internal string Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    internal static InfiniteSequenceRootArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetExpression(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new InfiniteSequenceRootArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class RangeRootArguments : FragmentArguments {
    internal RangeRootArguments(int number, string value0, string value1) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
    }

    public int Number { get; }
    internal string Value0 { get; }
    internal string Value1 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    internal static RangeRootArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=2) return null;
        if(!arguments.TryGetExpression(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        return new RangeRootArguments(arguments.Number, value0, value1);
    }
}

internal sealed class RepeatRootArguments : FragmentArguments {
    internal RepeatRootArguments(int number, string value0, string value1, string value2) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
    }

    public int Number { get; }
    internal string Value0 { get; }
    internal string Value1 { get; }
    internal string Value2 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        2 => this.Value2,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    internal static RepeatRootArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=3) return null;
        if(!arguments.TryGetExpression(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        return new RepeatRootArguments(arguments.Number, value0, value1, value2);
    }
}

internal sealed class SequenceRootArguments : FragmentArguments {
    internal SequenceRootArguments(int number, string value0, string value1, string value2, string value3) {
        this.Number=number;
        this.Value0=value0;
        this.Value1=value1;
        this.Value2=value2;
        this.Value3=value3;
    }

    public int Number { get; }
    internal string Value0 { get; }
    internal string Value1 { get; }
    internal string Value2 { get; }
    internal string Value3 { get; }
    internal Enumerable GetEnumerable(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal FuncFragment GetFunction(int index) => index switch {
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };
    internal string GetExpression(int index) => index switch {
        0 => this.Value0,
        1 => this.Value1,
        2 => this.Value2,
        3 => this.Value3,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    internal static SequenceRootArguments? FromDirect(DirectFragmentArguments arguments) {
        if(arguments.Count!=4) return null;
        if(!arguments.TryGetExpression(0,out var value0)) return null;
        if(!arguments.TryGetExpression(1,out var value1)) return null;
        if(!arguments.TryGetExpression(2,out var value2)) return null;
        if(!arguments.TryGetExpression(3,out var value3)) return null;
        return new SequenceRootArguments(arguments.Number, value0, value1, value2, value3);
    }
}

