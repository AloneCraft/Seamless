using MultiProjectTemplate.Database;
using MultiProjectTemplate.Database.Tables;
using Seamless.Database.Client;

namespace MultiProjectTemplate.Server {
    public sealed class ItemQueriesImplementation:IItemQueries {
        private readonly TemplateDatabase database;

        public ItemQueriesImplementation(TemplateDatabase database) {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public ItemQueriesImplementation(INativeQueryContext context) {
            ArgumentNullException.ThrowIfNull(context);
            this.database = (TemplateDatabase)context.Database;
        }

        public ItemDto FindById(FindItemRequest request) {
            var key = new MultiProjectTemplate.Database.Keys.Items(request.Id);
            if (this.database.Items.TryGetValue(key, out var row)) {
                return new ItemDto(row.Id, row.Name, row.Price, row.Version);
            }
            throw new InvalidOperationException($"Item with Id {request.Id} not found.");
        }

        public ItemDto[] SearchByPrice(SearchByPriceRequest request) {
            var list = new List<ItemDto>();
            foreach (var item in this.database.Items) {
                if (item.Price >= request.MinPrice && item.Price <= request.MaxPrice) {
                    list.Add(new ItemDto(item.Id, item.Name, item.Price, item.Version));
                }
            }
            list.Sort((a, b) => a.Price.CompareTo(b.Price));
            return list.ToArray();
        }

        public int CountAll(CountAllRequest request) {
            return this.database.Items.Count;
        }
    }
}
