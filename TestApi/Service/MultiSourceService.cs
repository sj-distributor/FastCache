using FastCache.Core.Attributes;
using Microsoft.EntityFrameworkCore;
using TestApi.DB;
using TestApi.Entity;

namespace TestApi.Service;

public class MultiSourceService : IService, IMultiSourceService
{
    private readonly MemoryDbContext _dbContext;

    public MultiSourceService(MemoryDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public virtual User Add(User user)
    {
        _dbContext.Set<User>().Add(user);
        _dbContext.SaveChanges();
        return user;
    }

    public virtual async Task<User> Single(string id)
    {
        Thread.Sleep(TimeSpan.FromSeconds(1));
        return await _dbContext.Set<User>().SingleAsync(x => x.Id == id);
    }

    public virtual async Task<User> Update(User user)
    {
        var first = await _dbContext.Set<User>().FirstAsync(x => x.Id == user.Id);
        first.Name = user.Name;
        _dbContext.Set<User>().Update(first);
        await _dbContext.SaveChangesAsync();
        return first;
    }

    public virtual bool Delete(string id)
    {
        var user = _dbContext.Set<User>().FirstOrDefault(x => x.Id == id);
        if (user == null) return false;
        _dbContext.Set<User>().Remove(user);
        _dbContext.SaveChanges();
        return true;
    }

    public virtual IEnumerable<User> List(string page)
    {
        Thread.Sleep(TimeSpan.FromSeconds(1));
        return _dbContext.Set<User>().ToList();
    }
}