using BPCVN.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Controllers;

public class SwitchController : Controller
{
    private readonly AppDbContext _db;

    public SwitchController(AppDbContext db) => _db = db;

    // GET /Switch
    public async Task<IActionResult> Index()
    {
        var switches = await _db.Switches
                                .AsNoTracking()
                                .OrderBy(s => s.Type)
                                .ThenBy(s => s.Name)
                                .ToListAsync();
        return View(switches);
    }
}
