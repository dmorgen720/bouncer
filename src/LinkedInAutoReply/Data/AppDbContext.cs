using LinkedInAutoReply.Models;
using Microsoft.EntityFrameworkCore;

namespace LinkedInAutoReply.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<RecruiterMessage> RecruiterMessages => Set<RecruiterMessage>();
    public DbSet<SyncState> SyncStates => Set<SyncState>();
}
