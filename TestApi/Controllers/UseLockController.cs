using FastCache.Core.Enums;
using FastCache.MultiSource.Attributes;
using Microsoft.AspNetCore.Mvc;
using TestApi.Entity;
using TestApi.Service;

namespace TestApi.Controllers;

[ApiController]
[Route("/[controller]")]
public class UseLockController(ILockUserService lockUserService)
{
    [HttpPost("add-with-cache")]
    [DistributedLock("user-add")]
    [MultiSourceCacheable("MultiSource-single", "{user:id}", Target.Redis, 5)]
    [MultiSourceEvictable(new[] { "MultiSource-single", "MultiSources" }, "{user:id}", Target.Redis)]
    public virtual async Task<User> AddWithCache(User user, int delayMs = 0)
    {
        return await lockUserService.Add(user, delayMs);
    }

    [HttpPost]
    [DistributedLock("user-add")]
    public virtual async Task<User> Add(User user, int delayMs = 0)
    {
        return await lockUserService.Add(user, delayMs);
    }

    [HttpGet]
    [MultiSourceCacheable("MultiSource-single", "{id}", Target.Redis, 5)]
    public virtual async Task<User> Get(string id)
    {
        return await lockUserService.Single(id);
    }
    
    [HttpGet("users")]
    [MultiSourceCacheable("MultiSource-single", "{id}", Target.Redis, 5)]
    public virtual IEnumerable<User> Users(string page)
    {
        return lockUserService.List(page);
    }

    [HttpDelete]
    [MultiSourceEvictable(new[] { "MultiSource-single", "MultiSources" }, "*{id}*", Target.Redis)]
    public virtual bool Delete(string id)
    {
        return lockUserService.Delete(id);
    }
}