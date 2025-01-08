using Cloud24_25.Infrastructure.Model;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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
        var connectionString = "Server=10.0.0.60;Database=test_db;Uid=cloud_admin;Pwd=Cloud_admin1;";
        var serverVersion = ServerVersion.AutoDetect(connectionString);
        optionsBuilder.UseMySql(connectionString, serverVersion);
    }
    
    public DbSet<User> Users { get; set; }
    public DbSet<FileRevision> FileRevisions { get; set; }
    public DbSet<File> Files { get; set; }
    public DbSet<Log> Logs { get; set; }
}