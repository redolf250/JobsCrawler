using Crawler.Model;
using Microsoft.EntityFrameworkCore;

namespace Crawler.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<CrawledJob>? CrawledJobs { get; set; }
    public DbSet<GhanaJobId>? GhanaJobIds { get; set; }
}