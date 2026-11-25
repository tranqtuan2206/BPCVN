using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace BPCVN.SecurityTests;

/// <summary>
/// Kiểm thử bộ giới hạn tần suất (Rate Limiting) phân vùng theo IP chống Brute-force.
/// </summary>
public class RateLimitTests
{
    [Fact]
    public async Task LoginRateLimit_ShouldBlockOnlyAbusiveIp_AndAllowOtherIps()
    {
        using var limiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            Program.CreateIpFixedWindowPartition(ctx, permitLimit: 5, windowMinutes: 15));

        var attackerCtx = new DefaultHttpContext();
        attackerCtx.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.10");

        var normalUserCtx = new DefaultHttpContext();
        normalUserCtx.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.25");

        // 5 lần thử đầu tiên từ IP attacker đều được phép đi qua
        for (int i = 0; i < 5; i++)
        {
            using var lease = await limiter.AcquireAsync(attackerCtx, 1);
            Assert.True(lease.IsAcquired);
        }

        // Lần thứ 6 từ cùng IP phải bị chặn
        using (var blockedLease = await limiter.AcquireAsync(attackerCtx, 1))
        {
            Assert.False(blockedLease.IsAcquired);
        }

        // Người dùng hợp lệ ở IP khác vẫn đăng nhập bình thường
        using (var normalLease = await limiter.AcquireAsync(normalUserCtx, 1))
        {
            Assert.True(normalLease.IsAcquired);
        }
    }
}
