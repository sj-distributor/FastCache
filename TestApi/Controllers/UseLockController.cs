using FastCache.MultiSource.Attributes;
using Microsoft.AspNetCore.Mvc;
using TestApi.Entity;
using TestApi.Service;

namespace TestApi.Controllers;

[ApiController]
[Route("/[controller]")]
public class UseLockController(ILockUserService lockUserService)
{
    [HttpPost]
    [DistributedLock("user-add")]
    public User Add(User user, int delayMs = 0)
    {
        return lockUserService.Add(user, delayMs);
    }
    
    [HttpGet("users")]
    public virtual IEnumerable<User> Users(string page)
    {
        return lockUserService.List(page);
    }
}