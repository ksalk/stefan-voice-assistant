using Microsoft.EntityFrameworkCore;

namespace StefanAssistant.Server.Tools.Timer;

public class TimerDbContext(DbContextOptions<TimerDbContext> options) : DbContext(options)
{
    public DbSet<TimerEntry> Timers => Set<TimerEntry>();
}
