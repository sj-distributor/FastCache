using Microsoft.AspNetCore.Mvc;
using TestApi.Entity;
using TestApi.Service;

namespace TestApi.Controllers;

[ApiController]
[Route("/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public virtual async Task<User> Get(string id)
    {
        var user = await _userService.Single(id);
        return user;
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
    
    [HttpGet("get")]
    public async Task<User?> GetSingleOrDefault(string userId)
    {
        return await _userService.SingleOrDefault(userId);
    }
}