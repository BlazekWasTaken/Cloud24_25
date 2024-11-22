using System.ComponentModel.DataAnnotations;

namespace Cloud24_25.Infrastructure.Model;

public class File
{
    public Guid Id { get; set; }

    [MaxLength(255)] public required string Name { get; set; }

    [MaxLength(255)] public required string Path { get; set; }

    [MaxLength(255)] public required string Extension { get; set; }

    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }

    [MaxLength(5)] public required List<FileRevision> Revisions { get; set; }
}