using Microsoft.AspNetCore.Mvc;
using TestApi.Entity;
using TestApi.Service;

namespace TestApi.Controllers;

[ApiController]
[Route("/user", Name = "/user")]
public class UserController : ControllerBase
{
    private readonly UserService _userService;

    public UserController(UserService userService)
    {
        _userService = userService;
    }

    [Route("/"), HttpGet]
    public  User Get(string id)
    {
        return _userService.Single(id);
    }

    [Route("/"), HttpPost]
    public User Add(User user)
    {
        return _userService.Add(user);
    }

    [Route("/"), HttpPut]
    public User Update(User user)
    {
        return _userService.Update(user);
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