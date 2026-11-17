using Microsoft.AspNetCore.Mvc;

namespace BPCVN.Web.Controllers;

public class TypingTestController : Controller
{
    // GET /TypingTest
    public IActionResult Index()
    {
        ViewData["Title"] = "Gõ Phím";
        return View();
    }
}
