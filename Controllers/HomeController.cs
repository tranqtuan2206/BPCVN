using BPCVN.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        ViewBag.KitCount    = await _db.Kits.CountAsync();
        ViewBag.SwitchCount = await _db.Switches.CountAsync();
        ViewBag.SpecCount   = await _db.Specs.CountAsync();
        return View();
    }
}
