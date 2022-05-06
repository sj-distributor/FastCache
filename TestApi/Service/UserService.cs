using EasyCache.Core.Attributes;
using TestApi.DB;
using TestApi.Entity;
using TestApi.Utils;

namespace TestApi.Service;

public class UserService : IService
{
    private readonly MemoryDbContext _dbContext;

    public UserService(MemoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public virtual User Add(User user)
    {
        var entityEntry = _dbContext.Set<User>().Add(user);
        _dbContext.SaveChanges();
        return entityEntry.Entity;
    }
    
    [Cacheable("user-single", "{id}", 60 * 30)]
    public virtual User Single(string id)
    {
        Thread.Sleep(TimeSpan.FromSeconds(1));
        return _dbContext.Set<User>().Single(x => x.Id == id);
    }

    [Cacheable("user-single", "{user:id}", 60 * 30)]
    [Evictable(new[] { "user-single", "users" }, "{user:id}")]
    public virtual User Update(User user)
    {
        var entityEntry = _dbContext.Set<User>().Update(user);
        _dbContext.SaveChanges();
        return entityEntry.Entity;
    }

    [Evictable(new[] { "user-single", "users" }, "{id}")]
    public virtual bool Delete(string id)
    {
        var user = _dbContext.Set<User>().FirstOrDefault(x => x.Id == id);
        if (user == null) return false;
        _dbContext.Set<User>().Remove(user);
        _dbContext.SaveChanges();
        return true;
    }

    [Cacheable("users", "{page}", 60)]
    public virtual IEnumerable<User> List(string page)
    {
        Thread.Sleep(TimeSpan.FromSeconds(1));
        return _dbContext.Set<User>().ToList();
    }
    
    [Cacheable("users", "{number}")]
    public virtual IEnumerable<User> Performance(string number)
    {
        return DataUtils.GetData();
    }
}