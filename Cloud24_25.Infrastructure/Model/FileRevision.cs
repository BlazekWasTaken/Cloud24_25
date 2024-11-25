using System.ComponentModel.DataAnnotations;

namespace Cloud24_25.Infrastructure.Model;

public class FileRevision
{
    public Guid Id { get; set; }
    public DateTime Created { get; set; }

    [MaxLength(255)] public required string ObjectName { get; set; }
}