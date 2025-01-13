using FastCache.Core.Driver;
using Microsoft.AspNetCore.Mvc;
using TestApi.Entity;
using TestApi.Service;

namespace TestApi.Controllers;

[ApiController]
[Route("/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    private readonly ICacheClient _cacheClient;

    public UserController(IUserService userService, ICacheClient cacheClient)
    {
        _userService = userService;
        _cacheClient = cacheClient;
    }

    [HttpGet]
    public virtual async Task<User> Get(string id)
    {
        var user = await _userService.Single(id);
        return user;
    }

    [HttpGet("id-and-name")]
    public virtual async Task<User> Get(string id, string name)
    {
        var user = await _userService.Single(id, name);
        return user;
    }

    [HttpGet("indirect-impl")]
    public virtual async Task<User> GetByMutiImp(string id, string name)
    {
        var user = await _userService.Single(id, name, CancellationToken.None);
        return user;
    }

    [HttpPost("indirect-impl")]
    public async Task<User> AddByMutiImp(User user)
    {
        return await _userService.Add(user, CancellationToken.None);
    }

    [HttpPost]
    public User Add(User user)
    {
        return _userService.Add(user);
    }

    [HttpPut]
    public async Task<User> Update(User user)
    {
        return await _userService.Update(user);
    }

    [HttpDelete]
    public bool Delete(string id)
    {
        return _userService.Delete(id);
    }

    [HttpGet("users")]
    public IEnumerable<User> Users(string page)
    {
        return _userService.List(page);
    }

    [HttpGet("check-cache-result")]
    public async Task<bool> GetCheckCacheResult(string key, bool isExist, string id, string name)
    {
        var result = await _cacheClient.Get(key);
        var value = (User?) result.Value;

        if (!isExist) return value == null;

        return value != null && value.Id == id && value.Name == name;
    }
}