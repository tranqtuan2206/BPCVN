using BPCVN.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BPCVN.Controllers;

public class KitController : Controller
{
    private readonly AppDbContext _db;

    public KitController(AppDbContext db) => _db = db;

    // GET /Kit
    public async Task<IActionResult> Index()
    {
        var kits = await _db.Kits
                            .AsNoTracking()
                            .OrderBy(k => k.Brand)
                            .ThenBy(k => k.Name)
                            .ToListAsync();
        return View(kits);
    }
}
