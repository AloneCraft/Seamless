namespace Shared.Linq.IdealSources;

// 行の突き合わせは具象型の直接の基底型名(Array`1 など)で行うため、FLinq/ZLinq と同じ名前の基底を置く。
// System.Collections.Generic.IEnumerable<T> と名前が衝突しないよう、演算子本体とは別の名前空間にする。
#if SYSTEM_IENUMERABLE
public abstract class IEnumerable<T>(int count):global::Shared.Linq.Ideal.IdealBase<T>(count) where T : notnull;
#elif SYSTEM_LIST
public abstract class List<T>(int count):global::Shared.Linq.Ideal.IdealBase<T>(count) where T : notnull;
#else
public abstract class Array<T>(int count):global::Shared.Linq.Ideal.IdealBase<T>(count) where T : notnull;
#endif
