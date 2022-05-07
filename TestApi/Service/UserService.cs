using EasyCache.Core.Attributes;
using TestApi.DB;
using TestApi.Entity;

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
        _dbContext.Set<User>().Add(user);
        _dbContext.SaveChanges();
        return user;
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
        var first = _dbContext.Set<User>().First(x => x.Id == user.Id);
        first.Name = user.Name;
        _dbContext.Set<User>().Update(first);
        _dbContext.SaveChanges();
        return first;
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

    [Cacheable("users", "{page}", 60 * 10)]
    public virtual IEnumerable<User> List(string page)
    {
        Thread.Sleep(TimeSpan.FromSeconds(1));
        return _dbContext.Set<User>().ToList();
    }
}