using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SteamProject.DAL.Abstract;

namespace SteamProject.DAL.Concrete
{
    /// <summary>
    /// Base class repository that implements common and CRUD operations.  Meant to be like an abstract 
    /// base class, but not actually made abstract because it is often useful to have a repository 
    /// that only supports these common operations.
    /// 
    /// This is only a minimal version. There is quite a lot we could add to this, including:
    ///    - add better error checking/recovery (i.e. throw exceptions when something goes wrong; write a 
    ///      custom exception class to handle errors)
    ///    - Write a base model class for the parameterized type, i.e. require TEntity : ModelBase, 
    ///      and have ModelBase define important things like the PK name and type so we can enforce that in
    ///      FindById for example.
    ///    - Async versions
    /// </summary>
    /// <typeparam name="TEntity">This is the entity for which we're making a repository</typeparam>
    public class Repository<TEntity> : IRepository<TEntity> where TEntity : class, new()
    {
        // The context is private to enforce full separation, preventing a subclass from accessing
        // entities other than this one.  If you need other entities, write a separate "service" or "provider" class
        // that has access to all repositories needed
        private readonly DbContext _context;
        // This one is OK to use in a subclass because it only represents the entity for this type of repository
        // and it can be mocked
        protected readonly DbSet<TEntity> _dbSet;

        public Repository(DbContext ctx)
        {
            _context = ctx;
            _dbSet = _context.Set<TEntity>();   // must do it this way because we don't have a "navigation property" to use
        }

        // Find by ID assuming it's the PK and is an int
        public virtual TEntity FindById(int id)
        {
            TEntity entity = _dbSet.Find(id);
            return entity;  // null if not found
        }

        public virtual bool Exists(int id)
        {
            return FindById(id) != null;
        }

        public virtual IQueryable<TEntity> GetAll()
        {
            // note, no includes here and we're returning it as an IQueryable, NOT a DbSet, on purpose
            // so the caller cannot do other things (which should go here or in a subclass)
            return _dbSet;
        }

        public IQueryable<TEntity> GetAll(params Expression<Func<TEntity, object>>[] includes)
        {
            // Apply includes one by one
            IQueryable<TEntity> dbs = _dbSet;
            foreach (var item in includes)
            {
                dbs = dbs.Include(item);
            }
            return dbs;
        }

        public IQueryable<TEntity> GetAll(Expression<Func<TEntity, bool>> predicate)
        {
            IQueryable<TEntity> dbs = _dbSet;
            return dbs.Where(predicate);
        }

        public virtual TEntity AddOrUpdate(TEntity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            _context.Update(entity);
            _context.SaveChanges();
            return entity;
        }

        // A save seam for subclasses that stage several changes and want to persist them in a
        // single round-trip (one SaveChanges) instead of one per entity, as AddOrUpdate does.
        // Virtual so a subclass (or a test) can observe or override the one-and-only save.
        protected virtual void SaveChanges()
        {
            _context.SaveChanges();
        }

        public virtual void Delete(TEntity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            _dbSet.Remove(entity);
            _context.SaveChanges();
        }

        public virtual void DeleteById(int id)
        {
            var entity = FindById(id);
            if (entity == null) return;
            Delete(entity);
        }

        public virtual async Task<TEntity?> FindByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);
        }

        public virtual async Task<TEntity> AddOrUpdateAsync(TEntity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            _context.Update(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public virtual async Task DeleteAsync(TEntity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
        }

        public virtual async Task DeleteByIdAsync(int id)
        {
            var entity = await FindByIdAsync(id);
            if (entity == null) return;
            await DeleteAsync(entity);
        }
    }
}
