using BPCVN.Infrastructure.Data;
using BPCVN.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BPCVN.SecurityTests;

/// <summary>
/// Kiểm thử rà soát các điểm sửa XSS, cấu hình Production, chunk upload và DbSeeder.
/// </summary>
public class XssAndConfigAuditTests
{
    [Fact]
    public void ViewsAndScripts_ShouldNotContainVulnerableInnerHtmlSinks()
    {
        var root = TestFactory.FindRepoRoot();

        var detailsCshtml = File.ReadAllText(Path.Combine(root, "src/BPCVN.Web/Views/Spec/Details.cshtml"));
        Assert.Contains("&quot;", detailsCshtml);
        Assert.Contains("&#39;", detailsCshtml);
        Assert.Contains("textSpan.textContent", detailsCshtml);
        Assert.DoesNotContain("<span>${msg}</span>", detailsCshtml);

        var pendingCshtml = File.ReadAllText(Path.Combine(root, "src/BPCVN.Web/Areas/Admin/Views/Admin/Pending.cshtml"));
        Assert.DoesNotContain("list.map(x => `<option", pendingCshtml);
        Assert.Contains("opt.textContent = x.name", pendingCshtml);

        var kitEditCshtml = File.ReadAllText(Path.Combine(root, "src/BPCVN.Web/Views/Kit/Edit.cshtml"));
        Assert.DoesNotContain("'<img src=\"' + previewSrc", kitEditCshtml);
        Assert.Contains("labelDiv.textContent", kitEditCshtml);

        var uploadCshtml = File.ReadAllText(Path.Combine(root, "src/BPCVN.Web/Views/Soundtest/Upload.cshtml"));
        Assert.DoesNotContain("'<span>' + msg + '</span>'", uploadCshtml);
        Assert.Contains("textSpan.textContent", uploadCshtml);

        var waveformJs = File.ReadAllText(Path.Combine(root, "src/BPCVN.Web/wwwroot/js/waveform-setup.js"));
        Assert.DoesNotContain("waveformDiv.innerHTML", waveformJs);
        Assert.Contains("errDiv.textContent", waveformJs);
    }

    [Fact]
    public void SecurityConfiguration_ShouldHaveSafeDefaults()
    {
        var root = TestFactory.FindRepoRoot();

        var webConfig = File.ReadAllText(Path.Combine(root, "src/BPCVN.Web/web.config"));
        Assert.Contains("stdoutLogEnabled=\"false\"", webConfig);

        var programCs = File.ReadAllText(Path.Combine(root, "src/BPCVN.Web/Program.cs"));
        Assert.Contains("Content-Security-Policy", programCs);
        Assert.Contains("options.Cookie.HttpOnly = true", programCs);

        var soundTestCtrl = File.ReadAllText(Path.Combine(root, "src/BPCVN.Web/Controllers/SoundTestController.cs"));
        Assert.Contains("MediaSecurityHelper.IsValidMediaSignature", soundTestCtrl);
        Assert.Contains("MediaSecurityHelper.MaxChunkCount", soundTestCtrl);
        Assert.Contains("\"App_Data\", \"temp\", \"chunks\", userId", soundTestCtrl);
    }

    [Fact]
    public void ChunkLimits_ShouldBeBounded()
    {
        Assert.True(MediaSecurityHelper.MaxChunkCount > 0 && MediaSecurityHelper.MaxChunkCount <= 20);
        Assert.Equal(100L * 1024 * 1024, MediaSecurityHelper.MaxAudioSizeBytes);
        Assert.Equal(200L * 1024 * 1024, MediaSecurityHelper.MaxVideoSizeBytes);
    }
}
