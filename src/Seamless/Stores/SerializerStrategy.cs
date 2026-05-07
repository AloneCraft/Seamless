namespace Seamless.Stores;

public interface ISerializerStrategy {
    static abstract bool UsesMemoryPackSerializer { get; }
}

public readonly struct BlittableStrategy:ISerializerStrategy {
    public static bool UsesMemoryPackSerializer => false;
}

public readonly struct MemoryPackSerializerStrategy:ISerializerStrategy {
    public static bool UsesMemoryPackSerializer => true;
}
