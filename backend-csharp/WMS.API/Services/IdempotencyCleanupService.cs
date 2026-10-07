using WMS.Data;
using Microsoft.EntityFrameworkCore;

namespace WMS.API.Services;

public class IdempotencyCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IdempotencyCleanupService> _logger;

    public IdempotencyCleanupService(IServiceProvider serviceProvider, ILogger<IdempotencyCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<WmsDbContext>();
                
                var deleted = await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM \"IdempotencyKeys\" WHERE \"CreatedAt\" < {DateTime.UtcNow.AddDays(-7)}", stoppingToken);
                
                if (deleted > 0)
                {
                    _logger.LogInformation("Deleted {Count} expired idempotency keys", deleted);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while cleaning up expired idempotency keys.");
            }

            // Run once a day
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}
