using FastCache.Core.Enums;
using FastCache.MultiSource.Attributes;
using Microsoft.AspNetCore.Mvc;
using TestApi.Entity;
using TestApi.Service;

namespace TestApi.Controllers;

[ApiController]
[Route("/[controller]")]
public class MultiSourceController : ControllerBase
{
    private readonly IMultiSourceService _userService;

    public MultiSourceController(IMultiSourceService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    [MultiSourceCacheable("MultiSource-single", "{id}", Target.Redis, 5)]
    public virtual async Task<User> Get(string id)
    {
        return await _userService.Single(id);
    }
    
    [HttpGet("get/two")]
    public virtual async Task<User> Get(string id, string name)
    {
        return await _userService.SingleOrDefault(id, name, true);
    }

    [HttpPost]
    public User Add(User user)
    {
        return _userService.Add(user);
    }

    [HttpPut]
    [MultiSourceCacheable("MultiSource-single", "{user:id}", Target.Redis, 5)]
    [MultiSourceEvictable(new[] { "MultiSource-single", "MultiSources" }, ["{user:id}", "{user:name}"], Target.Redis)]
    public virtual async Task<User> Update(User user)
    {
        return await _userService.Update(user);
    }

    [HttpDelete]
    [MultiSourceEvictable(new[] { "MultiSource-single", "MultiSources" }, ["{id}"], Target.Redis)]
    public virtual bool Delete(string id)
    {
        return _userService.Delete(id);
    }

    [HttpGet("users")]
    [MultiSourceCacheable("MultiSources", "{page}", Target.Redis, 5)]
    public virtual IEnumerable<User> Users(string page)
    {
        return _userService.List(page);
    }
    
    [HttpGet("get")]
    public virtual async Task<User?> TestResultNull(string id)
    {
        return await _userService.SingleOrDefault(id);
    }
    
    [HttpGet("get/name")]
    public virtual async Task<User?> SearchName(string name)
    {
        return await _userService.SingleOrDefaultByName(name);
    }
}