using System.ComponentModel.DataAnnotations;

namespace Cloud24_25.Infrastructure.Model;

public class Log
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public LogType LogType { get; set; }

    [MaxLength(255)] public required string Message { get; set; }
}

public enum LogType
{
    Register,
    Login,
    LoginAttempt,
    Logout,
    FileUpload,
    FileDownload,
    ViewListOfFiles,
    Failure
}