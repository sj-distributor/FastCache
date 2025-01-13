using FastCache.Core.Attributes;
using Microsoft.EntityFrameworkCore;
using TestApi.DB;
using TestApi.Entity;

namespace TestApi.Service;

public class UserService : IService, IUserService
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
    
    [Evictable(new[] {"user-single"}, "id")]
    public virtual async Task<User> Single(string id, string name)
    {
        Thread.Sleep(TimeSpan.FromSeconds(1));
        return await _dbContext.Set<User>().SingleAsync(x => x.Id == id && x.Name == name);
    }

    [Cacheable("user-single", "{id}", 60 * 10)]
    public virtual async Task<User> Single(string id)
    {
        Thread.Sleep(TimeSpan.FromSeconds(1));
        return await _dbContext.Set<User>().SingleAsync(x => x.Id == id);
    }

    [Cacheable("user-single", "{user:id}{user:thirdPartyIds}", 60 * 10)]
    [Evictable(new[] { "user-single", "users" }, "{user:id}")]
    public virtual async Task<User> Update(User user)
    {
        var first = await _dbContext.Set<User>().FirstAsync(x => x.Id == user.Id);
        first.Name = user.Name;
        _dbContext.Set<User>().Update(first);
        await _dbContext.SaveChangesAsync();
        return first;
    }

    [Cacheable("user-single", "{user:id}", 60 * 10)]
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

    [Evictable(new[] { "user-single", "users" }, "{entity:id}:{entity:name}")]
    public virtual async Task<User> Add(User entity, CancellationToken cancellationToken)
    {
        _dbContext.Set<User>().Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    [Cacheable("user-single", "{id}:{name}", 60 * 10)]
    public virtual async Task<User> Single(string id, string name, CancellationToken cancellationToken)
    {
        Thread.Sleep(TimeSpan.FromSeconds(1));
        return await _dbContext.Set<User>().SingleAsync(x => x.Id == id && x.Name == name, cancellationToken);
    }
}