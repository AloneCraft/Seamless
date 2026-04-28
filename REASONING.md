# Enumerable implementation notes

- Scope: `Seamless.Linq.Enumerable` public static methods.
- Goal: make the current API surface behave like `System.Linq.Enumerable` for supported target frameworks.
- Chosen approach: keep the public `Seamless.Linq.Enumerable` API surface and place the straightforward `foreach`/`for` loops,`List<T>`,`Dictionary<TKey,TValue>`,`HashSet<T>`,and private helper types directly inside the `Enumerable` class instead of direct `System.Linq.Enumerable` calls.
- Iterator-returning methods validate arguments in the public method and delegate lazy enumeration to private static iterator helpers in the same `Enumerable` class.
- Single-use local iterator functions are avoided; buffering methods such as grouping,ordering,reverse,and range-based take use simple list/lookup helpers to preserve expected ordering and shape.
- Tests compare `Seamless.Linq.Enumerable` and `System.Linq.Enumerable` side by side across scalar,sequence,set,grouping,joining,ordering,conversion,and numeric overload families.
- Test data includes duplicate and non-duplicate inputs,custom comparers,nullable values,group/join shapes,and all numeric types covered by the exposed `Average`/`Sum`/`Min`/`Max` overloads.

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
