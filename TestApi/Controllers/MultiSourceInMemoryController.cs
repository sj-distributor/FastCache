using FastCache.Core.Enums;
using FastCache.MultiSource.Attributes;
using Microsoft.AspNetCore.Mvc;
using TestApi.Entity;
using TestApi.Service;

namespace TestApi.Controllers;

[ApiController]
[Route("/[controller]")]
public class MultiSourceInMemoryController : ControllerBase
{
    private readonly IMultiSourceService _userService;

    public MultiSourceInMemoryController(IMultiSourceService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    [MultiSourceCacheable("MultiSource-single", "{id}", Target.InMemory, 5)]
    public virtual async Task<User> Get(string id)
    {
        return await _userService.Single(id);
    }

    [HttpPost]
    public User Add(User user)
    {
        return _userService.Add(user);
    }

    [HttpPut]
    [MultiSourceCacheable("MultiSource-single", "{user:id}", Target.InMemory, 5)]
    [MultiSourceEvictable(new[] { "MultiSource-single", "MultiSources" }, ["{user:id}"], Target.InMemory)]
    public virtual async Task<User> Update(User user)
    {
        return await _userService.Update(user);
    }

    [HttpDelete]
    [MultiSourceEvictable(new[] { "MultiSource-single", "MultiSources" }, ["{id}"], Target.InMemory)]
    public virtual bool Delete(string id)
    {
        return _userService.Delete(id);
    }

    [HttpGet("users")]
    [MultiSourceCacheable("MultiSources", "{page}", Target.InMemory, 5)]
    public virtual IEnumerable<User> Users(string page)
    {
        return _userService.List(page);
    }
}