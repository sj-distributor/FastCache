using Microsoft.EntityFrameworkCore;
using TestApi.DB;
using TestApi.Entity;

namespace TestApi.Service;

public interface ILockUserService
{
    Task<User> Add(User user, int delayMs = 0, CancellationToken cancellationToken = default);
    Task<User> Update(User user);
    Task<User> Single(string id);
    bool Delete(string id);
    IEnumerable<User> List(string page);
}

public class LockUserService(MemoryDbContext dbContext) : IService, ILockUserService
{
    public virtual async Task<User> Add(User user, int delayMs = 0, CancellationToken cancellationToken = default)
    {
        if (delayMs > 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken);
        }

        dbContext.Set<User>().Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    public virtual async Task<User> Single(string id)
    {
        await Task.Delay(1000);
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