using Microsoft.AspNetCore.Mvc;
using TestApi.Entity;
using TestApi.Service;

namespace TestApi.Controllers;

[ApiController]
[Route("/user", Name = "/user")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [Route("/"), HttpGet]
    public virtual async Task<User> Get(string id)
    {
        return await _userService.Single(id);
    }

    [Route("/"), HttpPost]
    public User Add(User user)
    {
        return _userService.Add(user);
    }

    [Route("/"), HttpPut]
    public async Task<User> Update(User user)
    {
        return await _userService.Update(user);
    }

    [Route("/"), HttpDelete]
    public bool Delete(string id)
    {
        return _userService.Delete(id);
    }

    [Route("/users"), HttpGet]
    public IEnumerable<User> Users(string page)
    {
        return _userService.List(page);
    }
}