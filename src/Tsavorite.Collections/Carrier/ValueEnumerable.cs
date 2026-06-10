namespace Tsavorite.Carrier;

[StructLayout(LayoutKind.Auto)]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
[DebuggerTypeProxy(typeof(ValueEnumerableDebugView<,>))]
public readonly struct ValueEnumerable<TEnumerator, T>(TEnumerator enumerator):IEnumerable<T> where TEnumerator : struct, IValueEnumerator<T> {
    // enumerator is struct so it always copied, no need to create new Enumerator.
    // internal operator should use this
    public readonly TEnumerator Enumerator = enumerator;
    // Cast and OfType are implemented as instance methods rather than extension methods to simplify type specification.
    public ValueEnumerable<Cast<TEnumerator,T,TResult>,TResult> Cast<TResult>() => new(new(this.Enumerator));
    public ValueEnumerable<OfType<TEnumerator,T,TResult>,TResult> OfType<TResult>() => new(new(this.Enumerator));
    public IEnumerator<T> GetEnumerator() => new ValueEnumerator<TEnumerator,T>(this.Enumerator);
    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    string DebuggerDisplay => ValueEnumerableDebuggerDisplayHelper.BuildDisplayText(typeof(TEnumerator));
}
// all implement types must be struct
public interface IValueEnumerator<T>:IDisposable {
    /// <summary>
    /// Equivalent of IEnumerator.MoveNext + Current.
    /// </summary>
    bool TryGetNext(out T current);
    // for optimization
    /// <summary>
    /// Returns the length when processing time is not necessary.
    /// Always returns true if TryGetSpan or TryCopyTo returns true.
    /// </summary>
    bool TryGetNonEnumeratedCount(out int count);
    /// <summary>
    /// Returns true if it can return a Span.
    /// Used for SIMD and loop processing optimization.
    /// If copying the entire value is acceptable, prioritize TryGetNonEnumeratedCount -> TryCopyTo instead.
    /// </summary>
    bool TryGetSpan(out ReadOnlySpan<T> span);
    /// <summary>
    /// Unlike the semantics of normal CopyTo, this allows the destination to be smaller than the source.
    /// This serves as a TryGet function as well, e.g. single-span and ^1 is TryGetLast.
    /// </summary>
    bool TryCopyTo(scoped Span<T> destination,Index offset);
}
// generic implementation of enumerator
[StructLayout(LayoutKind.Auto)]
public struct ValueEnumerator<TEnumerator, T>(TEnumerator enumerator):IEnumerator<T>
    where TEnumerator : struct, IValueEnumerator<T> {
    TEnumerator enumerator = enumerator;
    T current = default!;
    public T Current {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.current;
    }
    object IEnumerator.Current => this.current!;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext() {
        if(this.enumerator.TryGetNext(out this.current)) {
            return true;
        }
        else {
            this.current=default!;
            return false;
        }
    }

    public void Reset() => throw new NotSupportedException();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => this.enumerator.Dispose();
}

public static partial class ValueEnumerableExtensions {
    // for foreach
    public static ValueEnumerator<TEnumerator,T> GetEnumerator<TEnumerator, T>(in this ValueEnumerable<TEnumerator,T> valueEnumerable)
        where TEnumerator : struct, IValueEnumerator<T>
        => new(valueEnumerable.Enumerator);
}
// for custom collection
public interface IValueEnumerable<TEnumerator, T> where TEnumerator : struct, IValueEnumerator<T> {
    // GetValueEnumerator + extension method AsValueEnumerable causes issues
    // with type inference, or conflicts with IEnumerable<T>'s AsValueEnumerable.
    // Since there's no compiler support for foreach,
    // it's more logical to include AsValueEnumerable in the interface.
    ValueEnumerable<TEnumerator,T> AsValueEnumerable();
}

internal static class ValueEnumerableDebuggerDisplayHelper // avoid <T> for assembly size
{
#if NET8_0_OR_GREATER
    [UnconditionalSuppressMessage("ReflectionAnalysis","IL2070",Justification = "Only call from debugger.")]
#endif
    public static string BuildDisplayText(Type type) {
        var sb = new StringBuilder();
        BuildCore(sb,type);
        var returnType = type.GetInterface("IValueEnumerator`1")?.GetGenericArguments()[0];
        if(returnType!=null) {
            sb.Append(" => "); // like TypeScript definition
            sb.Append(returnType.Name);
        }
        return sb.ToString();
    }

    static void BuildCore(StringBuilder sb,Type type) {
        if(type.IsGenericType) {
            var parent = type.GenericTypeArguments[0];
            if(parent.IsGenericType||(parent.Name is "FromRange" or "FromRange2" or "FromRangeDateTime" or "FromRangeDateTimeTo")) {
                BuildCore(sb,parent);
                sb.Append(".");
            }
            var genericsStart = type.Name.IndexOf('`');
            if(genericsStart!=-1) {
                var name = type.Name.Substring(0,genericsStart);
                sb.Append(name);
            }
            else {
                sb.Append(type.Name);
            }
            if(!parent.IsGenericType&&
               !(parent.Name is "FromRange" or "FromRange2" or "FromRangeDateTime" or "FromRangeDateTimeTo")) {
                sb.Append("<");
                sb.Append(parent.Name);
                sb.Append(">");
            }
            return;
        }
        if(type.Name is "FromRange" or "FromRange2" or "FromRangeDateTime" or "FromRangeDateTimeTo") {
            sb.Append(type.Name);
        }
        else {
            sb.Append("<");
            sb.Append(type.Name);
            sb.Append(">");
        }
    }
}

internal struct ValueEnumerableDebugView<TEnumerator, T>(ValueEnumerable<TEnumerator,T> source)
    where TEnumerator : struct, IValueEnumerator<T> {
    readonly ValueEnumerable<TEnumerator,T> source = source;
    T[]? items;
    // avoiding side-effects so only run when opened.
    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public T[] Items {
        get {
            if(this.items==null) {
                this.items=this.source.Take(100000).ToArray(); // max 100000 to avoid infinite call
            }
            return this.items;
        }
    }
}
