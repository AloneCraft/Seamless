using MemoryPack;
using Seamless.Database;

namespace MultiProjectTemplate.Database {
    internal static class DatabaseGenerator {
        public const string ClientContract = "MultiProjectTemplate.Database.IItemQueries";
        public const string ClientModule = "ItemQueries";
        public const string ClientDatabase = "TemplateDatabase";
    }

    public sealed partial class TemplateDatabase:DatabaseContainer {
        public TemplateDatabase(DatabaseContainerOptions options):base(options){}
    }

    [MemoryPackable]
    public readonly partial record struct FindItemRequest(int Id);

    [MemoryPackable]
    public readonly partial record struct SearchByPriceRequest(decimal MinPrice,decimal MaxPrice);

    [MemoryPackable]
    public readonly partial record struct CountAllRequest();

    [MemoryPackable]
    public readonly partial record struct ItemDto(int Id,string Name,decimal Price,long Version);

    public interface IItemQueries {
        ItemDto FindById(FindItemRequest request);
        ItemDto[] SearchByPrice(SearchByPriceRequest request);
        int CountAll(CountAllRequest request);
    }
}

namespace MultiProjectTemplate.Database.Tables {
    using Seamless.Database;

    public partial struct Items {
        [Key]
        public partial int Id{get;init;}
        public partial string Name{get;init;}
        public partial decimal Price{get;init;}
        public partial long Version{get;init;}
    }
}
