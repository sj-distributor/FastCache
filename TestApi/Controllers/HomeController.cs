using Microsoft.AspNetCore.Mvc;
using TestApi.Utils;

namespace TestApi.Controllers;

public class HomeController : Controller
{
    
    public IActionResult Index(string id)
    {
        return View(DataUtils.GetData());
    }

    public IActionResult Tow(string id)
    {
        return View(DataUtils.GetData());
    }
}