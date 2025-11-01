using Repository.DBContext;

namespace Repository.Base
{
    public abstract class BaseRepository
    {
        protected readonly AppDbContext _db;

        protected BaseRepository(AppDbContext db)
        {
            _db = db;
        }

        protected Guid NewId() => Guid.NewGuid();

        protected static void EnsureId<T>(T entity, string idProperty, Func<Guid>? idFactory = null)
        {
            var property = typeof(T).GetProperty(idProperty)
                           ?? throw new InvalidOperationException($"{typeof(T).Name} missing property '{idProperty}'");

            var current = property.GetValue(entity);
            var needsId = current switch
            {
                null => true,
                Guid guid => guid == Guid.Empty,
                Guid? guid => !guid.HasValue || guid.Value == Guid.Empty,
                _ => false
            };

            if (!needsId)
            {
                return;
            }

            var newId = idFactory?.Invoke() ?? Guid.NewGuid();
            property.SetValue(entity, newId);
        }
    }
}
