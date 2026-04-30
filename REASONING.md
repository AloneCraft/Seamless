# Enumerable implementation notes

- Scope: `Seamless.Linq.Enumerable` public static methods.
- Goal: make the current API surface behave like `System.Linq.Enumerable` for supported target frameworks.
- Chosen approach: keep the public `Seamless.Linq.Enumerable` API surface and place the straightforward `foreach`/`for` loops,`List<T>`,`Dictionary<TKey,TValue>`,`HashSet<T>`,and private helper types directly inside the `Enumerable` class instead of direct `System.Linq.Enumerable` calls.
- Sequence-returning methods validate arguments in the public method and delegate to private static helpers that allocate `List<T>`, append with `Add`, and return the accumulated list instead of using `yield return`.
- Lookup and ordered enumerable helpers also materialize list-backed enumerators so the shared LINQ implementation no longer depends on compiler-generated iterator state machines.
- Single-use local iterator functions are avoided; buffering methods such as grouping,ordering,reverse,and range-based take use simple list/lookup helpers to preserve expected ordering and shape.
- Tests compare `Seamless.Linq.Enumerable` and `System.Linq.Enumerable` side by side across scalar,sequence,set,grouping,joining,ordering,conversion,and numeric overload families.
- Test data includes duplicate and non-duplicate inputs,custom comparers,nullable values,group/join shapes,and all numeric types covered by the exposed `Average`/`Sum`/`Min`/`Max` overloads.

# Source-generator scalar fusion notes

- Scope: `Seamless.SourceGenerator` direct call-site interceptors for scalar LINQ terminals such as `Sum` and `Average`.
- Goal: keep the public method body readable while emitting an interceptor method that fuses supported intermediate operations into direct loops without generating an optimized subclass.
- Supported intermediate operations: `Where`, `Select`, `Cast`, `OfType`, `Take`, `TakeWhile`, `Skip`, `SkipWhile`, `Distinct`, and `SelectMany`.
- Chosen approach: parse the semantic method chain from the original invocation, keep existing call-through translation as the fallback, and emit block-bodied interceptor methods only when the whole pipeline can be represented safely.
- Loop approach: a scalar terminal owns the accumulator and optional count, intermediate operations become `if`/`goto continue` checks or nested `foreach` loops, and `Take`/`TakeWhile` can stop the outer pipeline without allocating intermediate lists.
- Numeric approach: generated `Average` follows LINQ accumulator widening rules, including casting the `long` accumulator before division for integer sources.
- Test approach: shared container methods exercise fused `Where.Select.Sum`, `Skip.Take.Average`, `SelectMany.Where.Sum`, `Distinct.SkipWhile.TakeWhile.Sum`, `Cast.Sum`, and `OfType.Sum`; reflection calls provide the original baseline while normal direct calls exercise the generated interceptors.

# LINQ materialization and sequence fusion notes

- Scope: `Seamless.Linq.Enumerable` sequence-returning operations and source-generator container-method interceptors that return `IEnumerable<T>`.
- Goal: keep default `Seamless.Linq.Enumerable` behavior close to `System.Linq`-style in-memory materialization, while providing explicit StorageList-backed entry points for callers that choose the space/time trade-off.
- Overload approach: use optional `ManagedTag` / `UnmanagedTag` parameters plus constraints for hot sequence operations such as `Select`, `Where`, `SelectMany`, and `Join`; `ManagedEnumerable` uses `ManagedList<T>`, while `UnmanagedEnumerable` requires unmanaged result types and uses `UnmanagedList<T>`.
- HashSet approach: `ManagedHashSetEnumerable` and `UnmanagedHashSetEnumerable` reuse the same core loops as the enumerable variants so the implementation is comparable and call sites can choose the storage policy explicitly.
- Inlining approach: storage-backed wrappers are thin `AggressiveInlining` methods over a shared generic core and also expose static-strategy overloads for `Select` and `Where`, avoiding delegate calls when the selector/predicate is known as a struct strategy.
- Lifetime approach: return a small enumerable wrapper that owns the temporary list and deletes its work directory on disposal/finalization, preserving normal `IEnumerable<T>` consumption while avoiding permanent work files.
- Source-generator boundary: generated sequence-returning interceptors no longer choose `ManagedList<T>` or `UnmanagedList<T>` from result type. StorageList use is an explicit API choice, while scalar fusion still emits direct loops.
- Nested translation approach: lambda bodies and nested method chains are recursively translated to `global::Seamless.Linq.Enumerable.*` so generated code does not depend on `using System.Linq` extension lookup.
- Scope boundary: `ToList()` must keep returning `System.Collections.Generic.List<T>` for BCL compatibility; grouping/ordering internals may still use in-memory helper state until they have equivalent persistent data structures.

# Build fix notes

- Scope: `Test.Seamless.Unit.SessionFunctions.AddTests<TKey,TValue>` build failure in `Test.Seamless2`.
- Cause: unqualified `IDictionary<TKey,TValue>` was ambiguous between `System.Collections.Generic.IDictionary<TKey,TValue>` and `Seamless.Collections.IDictionary<TKey,TValue>` after both namespaces were in scope.
- Chosen fix: make the abstract base signatures explicitly use `global::System.Collections.Generic.IDictionary<TKey,TValue>` so the derived `ManagedAdd` and `UnmanagedAdd` overrides keep matching their existing signatures.
- Follow-up: `DictionaryBenchmark` had relative `Seamless.Collections.IDictionary<TKey,TValue>` casts inside the `Test.Seamless` namespace, so those were changed to `global::Seamless.Collections.IDictionary<TKey,TValue>`; the one standard dictionary cast was changed to `global::System.Collections.Generic.IDictionary<TKey,TValue>`.

# HashSet and interceptor strategy notes

- Scope: `Seamless.Linq.HashSet`, source-generator query/method-chain translation, and LINQ interceptor validation tests.
- Goal: keep `Seamless.Collections.IHashSet<T>` constrained to `T:IEquatable<T>` and make LINQ wrappers compile without pretending that lazy `IEnumerable<T>` results are materialized `IHashSet<T>` instances.
- Chosen HashSet approach: scalar operations keep scalar return types, while sequence-producing wrappers return `IEnumerable<T>`/`IOrderedEnumerable<T>`/conversion results matching the underlying `Seamless.Linq.Enumerable` implementation. Parameters that are `IHashSet<T>` keep explicit `IEquatable<T>` constraints.
- Source-generator approach: query-expression members and simple method-chain members on database containers are translated into generated extension methods with fully-qualified `global::Seamless.Linq.Enumerable` calls, avoiding default `System.Linq` extension lookup.
- Interceptor approach: focused tests call the generated extension methods and compare them with the original container members so pre/post translation values stay aligned. A second test file covers two independent method chains in one expression.
- Strategy approach: add static-strategy overloads beside the existing delegate overloads. The strategy overloads accept structs implementing static abstract interfaces, so callers can avoid delegate allocation when the selector/predicate/accumulator is known as a type.

# Tsavorite test sharing notes

- Scope: Tsavorite-specific tests used by `Test.Seamless1` and `Test.Seamless2`.
- Approach: project-specific `tests/Test.Seamless1/Tsavorite` and `tests/Test.Seamless2/Tsavorite` files are moved into shared `tests/Test.Seamless/Tsavorite`; version-specific API code is guarded with `TSAVORITE1_1_1` or `TSAVORITE2_0BETA3`.
- Result: both test projects reference the same shared file set and class names, while the compiler includes only the implementation that matches the Tsavorite package version.

# Overflow mutable strategy notes

- Scope: `Seamless.Stores.OverflowMutableStrategy`, `HashStore` storage configuration, and shared store tests.
- Goal: make `WouldOverflowMutable` deterministic for both memory-backed and storage-backed stores so a full mutable/storage region throws before Tsavorite blocks on an unwritable append.
- Memory approach: keep the mutable byte limit as `MemorySize*MutableFraction`, fall back to `MemorySize` when the fraction gives no positive mutable bytes, and compare `tailAddress-beginAddress` plus record reservation against that limit at exact boundary values.
- Storage approach: make `StorageOverflowMutableStrategy` carry log-device capacity plus two safety limits: a capacity fraction and reserved free bytes. The effective append limit is the smaller of `capacity*fraction` and `capacity-reserveBytes`.
- API approach: add optional storage limit parameters to `HashStore.Create` and pass them only through storage-backed store constructors; existing callers keep the default full-capacity/no-reserve behavior.
- Test approach: add white-box boundary tests for memory, storage fraction, storage reserve bytes, combined storage limits, and unbounded storage capacity.

# LINQ chain interceptor notes

- Scope: source-generator container-method interceptors for sequence-returning LINQ chains and void methods that pass a sequence chain into another method.
- Goal: cover two-method chains such as `Where.Select` / `Select.Where` without adding a separate generator branch for every possible chain length, and keep the structure extensible for later three-method chains.
- Chosen approach: parse a chain into a linear operation pipeline, currently supporting `Where` and `Select`, then emit one materializing loop that can be reused for sequence-returning methods and for sequence arguments passed to another call.
- Pattern approach: when a method body contains `Call(source.Where(...))`, the interceptor materializes the chain into a local `List<T>` and calls `Call(result)`, so the original nested iterator call is not left in the generated method body.
- Test approach: add focused `Enumerable2`, `HashSet2`, `System.Linq.Enumerable2`, and `EnumerablePattern` samples under the existing `DatabaseGenerator` marker namespaces, and verify generated interceptors exist while smoke tests confirm the translated calls preserve results.
