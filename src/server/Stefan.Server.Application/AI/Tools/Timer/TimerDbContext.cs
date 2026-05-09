using Microsoft.EntityFrameworkCore;

namespace Stefan.Server.Application.AI.Tools.Timer;

public class TimerDbContext(DbContextOptions<TimerDbContext> options) : DbContext(options)
{
    public DbSet<TimerEntry> Timers => Set<TimerEntry>();
}
