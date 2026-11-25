using BPCVN.Application.ViewModels;
using BPCVN.Domain.Entities;
using BPCVN.Infrastructure.Data;
using BPCVN.Infrastructure.Services;
using BPCVN.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BPCVN.SecurityTests;

/// <summary>
/// Kiểm thử kiểm tra URL ảnh an toàn và validation đầu vào trên các Controller danh mục.
/// </summary>
public class UrlValidationTests
{
    [Theory]
    [InlineData("https://example.com/image.png", true)]
    [InlineData("http://cdn.example.com/a.jpg", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("JAVASCRIPT:alert(document.cookie)", false)]
    [InlineData("data:text/html,<script>alert(1)</script>", false)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("", false)]
    public void IsSafeHttpUrl_ShouldAllowOnlyHttpAndHttps(string url, bool expected)
    {
        Assert.Equal(expected, MediaSecurityHelper.IsSafeHttpUrl(url));
    }

    [Theory]
    [InlineData("/images/switch.png", true)]
    [InlineData("https://res.cloudinary.com/demo/image/upload/a.png", true)]
    [InlineData("//evil.com/payload.png", false)]
    [InlineData("/uploads/../appsettings.json", false)]
    [InlineData("javascript:alert(1)", false)]
    public void IsSafeImageUrlOrLocalPath_ShouldRejectProtocolRelativeAndSchemes(string url, bool expected)
    {
        Assert.Equal(expected, MediaSecurityHelper.IsSafeImageUrlOrLocalPath(url));
    }

    [Fact]
    public async Task KitController_ShouldRejectJavascriptUrlInModelState()
    {
        using var env = new TempWebHostEnvironment();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=.;Database=BPCVN_Dummy;Trusted_Connection=True;")
            .Options;
        await using var db = new AppDbContext(options);
        var imgService = new ImageService(env, NullLogger<ImageService>.Instance, TestFactory.CreateCloudinaryConfig());
        var kitService = new KitService(db);
        var controller = new KitController(db, imgService, kitService);

        var vm = new KitCreateViewModel
        {
            Name = "Neo65",
            ExternalImageUrls = ["javascript:alert(1)"]
        };

        var result = await controller.Create(vm);
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey("ExternalImageUrls"));
        Assert.Same(vm, viewResult.Model);
    }

    [Fact]
    public async Task SwitchAndKeycapControllers_ShouldRejectJavascriptImageUrl()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=.;Database=BPCVN_Dummy;Trusted_Connection=True;")
            .Options;
        await using var db = new AppDbContext(options);

        var switchCtrl = new SwitchController(db, new SwitchService(db));
        var swResult = await switchCtrl.Create(new Switch { Name = "TestSwitch", ImageUrl = "javascript:alert(1)" });
        Assert.IsType<ViewResult>(swResult);
        Assert.False(switchCtrl.ModelState.IsValid);

        var keycapCtrl = new KeycapController(db, new KeycapService(db));
        var kcResult = await keycapCtrl.Create(new Keycap { Name = "TestKeycap", ImageUrl = "data:text/html,<svg onload=alert(1)>" });
        Assert.IsType<ViewResult>(kcResult);
        Assert.False(keycapCtrl.ModelState.IsValid);
    }
}
