using Microsoft.AspNetCore.Mvc;
using Nop.Web.Factories;

namespace Nop.Web.Controllers;

public partial class HomeController : BasePublicController
{
    private readonly ICatalogModelFactory _catalogModelFactory;

    public HomeController(ICatalogModelFactory catalogModelFactory)
    {
        _catalogModelFactory = catalogModelFactory;
    }

    public virtual IActionResult Index()
    {
        return View();
    }

    public virtual IActionResult About()
    {
        return View();
    }

    public virtual IActionResult Products()
    {
        return View();
    }

    public virtual IActionResult Categories()
    {
        return View();
    }

    public virtual async Task<IActionResult> Brands()
    {
        var model = await _catalogModelFactory.PrepareManufacturerAllModelsAsync();
        return View(model);
    }
}