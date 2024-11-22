using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Cloud24_25.Infrastructure.Model;

public class User : IdentityUser
{
    [MaxLength(255)] public required List<File> Files { get; set; }

    [MaxLength(255)] public required List<Log> Logs { get; set; }
}