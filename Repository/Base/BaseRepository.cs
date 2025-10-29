using Microsoft.EntityFrameworkCore;
using Repository.DBContext;
using Repository.Repositories;

namespace Repository.Base
{
    public abstract class BaseRepository
    {
        protected readonly AppDbContext _db;
        protected readonly ISnowflakeIdGenerator _ids;

        protected BaseRepository(AppDbContext db, ISnowflakeIdGenerator ids)
        {
            _db = db;
            _ids = ids;
        }

        protected ulong NewId() => _ids.NextId();

        protected static void EnsureId<T, TId>(T entity, string idProperty, Func<TId> idFactory)
        {
            var prop = typeof(T).GetProperty(idProperty);
            if (prop == null) throw new InvalidOperationException($"{typeof(T).Name} missing property '{idProperty}'");

            var current = prop.GetValue(entity);
            bool isDefault =
                current == null ||
                current.Equals(default(TId)) ||
                (current is ulong ul && ul == 0UL);

            if (isDefault)
            {
                var newId = idFactory();
                prop.SetValue(entity, newId);
            }
        }
    }
}
