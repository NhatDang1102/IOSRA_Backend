using Yitter.IdGenerator;

namespace Repository.Repositories
{
    public interface ISnowflakeIdGenerator
    {
        ulong NextId();
    }
    public class YitterSnowflakeIdGenerator : ISnowflakeIdGenerator
    {
        public ulong NextId()
        {
            var id = YitIdHelper.NextId();
            unchecked
            {
                return (ulong)id;
            }
        }
    }
}