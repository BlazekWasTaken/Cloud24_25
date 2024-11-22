using Cloud24_25.Infrastructure.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using File = Cloud24_25.Infrastructure.Model.File;

namespace Cloud24_25.Infrastructure;

public class MyDbContext(DbContextOptions<MyDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    private DbSet<User> Users { get; set; }
    private DbSet<FileRevision> FileRevisions { get; set; }
    private DbSet<File> Files { get; set; }
    private DbSet<Log> Logs { get; set; }
}