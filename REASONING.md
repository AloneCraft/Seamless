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

- Scope: `Seamless.SourceGenerator` optimized container overrides for scalar LINQ terminals such as `Sum` and `Average`.
- Goal: keep the public method body readable while emitting an optimized override that fuses supported intermediate operations into direct loops.
- Supported intermediate operations: `Where`, `Select`, `Cast`, `OfType`, `Take`, `TakeWhile`, `Skip`, `SkipWhile`, `Distinct`, and `SelectMany`.
- Chosen approach: parse the semantic method chain from the original invocation, keep existing call-through translation as the fallback, and emit block-bodied overrides only when the whole pipeline can be represented safely.
- Loop approach: a scalar terminal owns the accumulator and optional count, intermediate operations become `if`/`goto continue` checks or nested `foreach` loops, and `Take`/`TakeWhile` can stop the outer pipeline without allocating intermediate lists.
- Numeric approach: generated `Average` follows LINQ accumulator widening rules, including casting the `long` accumulator before division for integer sources.
- Test approach: shared container methods exercise fused `Where.Select.Sum`, `Skip.Take.Average`, `SelectMany.Where.Sum`, `Distinct.SkipWhile.TakeWhile.Sum`, `Cast.Sum`, and `OfType.Sum`, with reflection tests comparing original and optimized containers in both test projects.

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

# Overflow mutable strategy notes

- Scope: `Seamless.Stores.OverflowMutableStrategy`, `HashStore` storage configuration, and shared store tests.
- Goal: make `WouldOverflowMutable` deterministic for both memory-backed and storage-backed stores so a full mutable/storage region throws before Tsavorite blocks on an unwritable append.
- Memory approach: keep the mutable byte limit as `MemorySize*MutableFraction`, fall back to `MemorySize` when the fraction gives no positive mutable bytes, and compare `tailAddress-beginAddress` plus record reservation against that limit at exact boundary values.
- Storage approach: make `StorageOverflowMutableStrategy` carry log-device capacity plus two safety limits: a capacity fraction and reserved free bytes. The effective append limit is the smaller of `capacity*fraction` and `capacity-reserveBytes`.
- API approach: add optional storage limit parameters to `HashStore.Create` and pass them only through storage-backed store constructors; existing callers keep the default full-capacity/no-reserve behavior.
- Test approach: add white-box boundary tests for memory, storage fraction, storage reserve bytes, combined storage limits, and unbounded storage capacity.
