using Crawler.Model;
using Microsoft.EntityFrameworkCore;

namespace Crawler.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<CrawledJob>? Staging_GhanaJob { get; set; }
    public DbSet<GhanaJobId>? Staging_GhanaJobIds { get; set; }
}