using FastCache.MultiSource.Attributes;
using Microsoft.EntityFrameworkCore;
using TestApi.DB;
using TestApi.Entity;

namespace TestApi.Service;

public interface ILockUserService
{
    User Add(User user, int delayMs = 0);
    IEnumerable<User> List(string page);
}

public class LockUserService(MemoryDbContext dbContext) : IService, ILockUserService
{
    public virtual User Add(User user, int delayMs = 0)
    {
        if (delayMs > 0)
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(delayMs));
        }

        dbContext.Set<User>().Add(user);
        dbContext.SaveChanges();
        return user;
    }

    public virtual async Task<User> Single(string id)
    {
        Thread.Sleep(TimeSpan.FromSeconds(1));
        return await dbContext.Set<User>().SingleAsync(x => x.Id == id);
    }

    public virtual async Task<User> Update(User user)
    {
        var first = await dbContext.Set<User>().FirstAsync(x => x.Id == user.Id);
        first.Name = user.Name;
        dbContext.Set<User>().Update(first);
        await dbContext.SaveChangesAsync();
        return first;
    }

    public virtual bool Delete(string id)
    {
        var user = dbContext.Set<User>().FirstOrDefault(x => x.Id == id);
        if (user == null) return false;
        dbContext.Set<User>().Remove(user);
        dbContext.SaveChanges();
        return true;
    }

    public virtual IEnumerable<User> List(string page)
    {
        Thread.Sleep(TimeSpan.FromSeconds(1));
        return dbContext.Set<User>().ToList();
    }
}