using Cloud24_25.Infrastructure.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using File = Cloud24_25.Infrastructure.Model.File;

namespace Cloud24_25.Infrastructure;

public class MyDbContext(DbContextOptions<MyDbContext> options) : IdentityDbContext<User>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var connectionString = ""; // TODO: use secret
        var serverVersion = ServerVersion.AutoDetect(connectionString);
        optionsBuilder
            .UseMySql(connectionString, serverVersion)
            .LogTo(Console.WriteLine, LogLevel.Information)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors();
    }
    
    public DbSet<User> Users { get; set; }
    public DbSet<FileRevision> FileRevisions { get; set; }
    public DbSet<File> Files { get; set; }
    public DbSet<Log> Logs { get; set; }
}