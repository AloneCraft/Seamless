using Seamless.Database.Client;

namespace Seamless.Application.Schema;

public interface IApplicationDatabase{
    NativeTable<Keys.Item,Records.Item> Items{get;}
}
