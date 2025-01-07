using System.IO.Compression;
using System.Security.Cryptography;
using Cloud24_25.Infrastructure;
using Cloud24_25.Infrastructure.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Oci.Common;
using Oci.Common.Auth;
using Oci.Common.Retry;
using Oci.ObjectstorageService;
using Oci.ObjectstorageService.Requests;
using Oci.ObjectstorageService.Responses;
using File = Cloud24_25.Infrastructure.Model.File;

namespace Cloud24_25.Service;

public static class FileService
{
    private const string BucketName = "bucket-20241022-1249";
    private const string NamespaceName = "frgrfeumviow";
    private const int NumberOfRevisions = 5;
    private const long Space = (long)2 * 1024 * 1024 * 1024;

    private static readonly ObjectStorageClient Client = new(
        new ConfigFileAuthenticationDetailsProvider("DEFAULT"),
        new ClientConfiguration
        {
            ClientUserAgent = "Cloud24_25",
            TimeoutMillis = 1000 * 1000,
            RetryConfiguration = new RetryConfiguration
            {
                // maximum number of attempts to retry the same request
                MaxAttempts = 5,
                // retries the request if the response status code is in the range [400-499] or [500-599]
                RetryableStatusCodeFamilies = [4, 5]
            }
        });

    public static async Task<IResult> UploadFileAsync(
        HttpContext context,
        IFormFile myFile,
        List<string?> hashes,
        MyDbContext db)
    {
        var endpointUser = context.User;
        var username = endpointUser.Identity?.Name;
        if (username == null)
        {
            await LogService.Log(LogType.FileUploadAttempt, "There was an attempt to upload a file, but user's credentials are incorrect.", db, null);
            return Results.Unauthorized();
        }
        
        if (!db.Users.Any() || !db.Users.Any(x => x.UserName == username))
        {
            await LogService.Log(LogType.FileUploadAttempt, $"User {username} was not found.", db, null);
            return Results.NotFound();
        }
        var user = db.Users
            .Include(x => x.Files)
            .ThenInclude(x => x.Revisions)
            .First(x => x.UserName == username);

        var userFilesSize = user.Files.Sum(x => x.Revisions.Sum(y => y.Size));
        if (myFile.OpenReadStream().Length + userFilesSize > Space)
        {
            await LogService.Log(LogType.FileUploadAttempt, "There was an attempt to upload a file, but user doesn't have enough free space.", db, user);
            return Results.BadRequest("Not enough free space to upload.");
        }
        if (myFile.ContentType != "application/zip")
        {
            if (hashes.Count != 1) return Results.BadRequest();
            if (GetHash(myFile.OpenReadStream()) != hashes[0]) return Results.BadRequest();
            var stream = myFile.OpenReadStream();
            await SaveFile(user, stream, myFile.FileName, myFile.ContentType, stream.Length, db);
        }
        else
        {
            ZipArchive archive = new(myFile.OpenReadStream());
            
            if (!IsZipArchiveCorrect(archive, hashes)) return Results.BadRequest();
            
            foreach (var entry in archive.Entries)
            {
                new FileExtensionContentTypeProvider().TryGetContentType(entry.Name, out var contentType);
                contentType ??= "application/octet-stream";
                await SaveFile(user, entry.Open(), entry.Name, contentType, entry.Length, db);
            }
        }
        
        return Results.Ok();
    }

    public static async Task<IResult> ListFiles(
        HttpContext context, 
        MyDbContext db)
    {
        var endpointUser = context.User;
        var username = endpointUser.Identity?.Name;
        if (username == null)
        {
            await LogService.Log(LogType.Failure, "There was an attempt to list files, but user's credentials are incorrect.", db, null);
            return Results.Unauthorized();
        }
        if (!db.Users.Any() || !db.Users.Any(x => x.UserName == username))
        {
            await LogService.Log(LogType.Failure, $"User {username} was not found.", db, null);
            return Results.NotFound();
        }
        
        var user = db.Users
            .Include(x => x.Files)
            .ThenInclude(x => x.Revisions)
            .First(x => x.UserName == username);
        var userFiles = user.Files;
        
        await LogService.Log(LogType.ViewListOfFiles, $"User {username} listed {userFiles.Count} files.", db, user);
        return Results.Ok(userFiles);
    }

    public static async Task<IResult> DeleteFile(
        HttpContext context,
        MyDbContext db,
        Guid id) 
    {
        var endpointUser = context.User;
        var username = endpointUser.Identity?.Name;
        if (username == null)
        {
            await LogService.Log(LogType.FileDeleteAttempt, "There was an attempt to delete a file, but user's credentials are incorrect.", db, null);
            return Results.Unauthorized();
        }
        if (!db.Users.Any() || !db.Users.Any(x => x.UserName == username))
        {
            await LogService.Log(LogType.FileDeleteAttempt, $"User {username} was not found.", db, null);
            return Results.NotFound();
        }
        
        var user = db.Users
            .Include(x => x.Files)
            .ThenInclude(x => x.Revisions)
            .First(x => x.UserName == username);
        var userFiles = user.Files;

        var fileToDelete = userFiles.First(x => x.Id == id);
        var revisionsToDelete = fileToDelete.Revisions;

        try
        {
            foreach (var revision in revisionsToDelete)
            {
                await DeleteObject(revision.ObjectName);
                db.FileRevisions.Remove(revision);
            }
            
            db.Files.Remove(fileToDelete);
            user.Files.Remove(fileToDelete);
            db.Users.Update(user);
            
            await db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            await LogService.Log(LogType.FileDeleteAttempt, $"{username}'s file delete attempt failed. Message: {e.Message}", db, user);
            throw;
        }

        await LogService.Log(LogType.FileDelete, $"{username} deleted a file called {fileToDelete.Name}", db, user);
        return Results.Ok();
    }
    
    public static async Task<IResult> DownloadFile(
        HttpContext context,
        MyDbContext db,
        Guid id)
    {
        var endpointUser = context.User;
        var username = endpointUser.Identity?.Name;
        if (username == null)
        {
            await LogService.Log(LogType.FileDeleteAttempt, "There was an attempt to delete a file, but user's credentials are incorrect.", db, null);
            return Results.Unauthorized();
        }

        if (!db.Users.Any() || !db.Users.Any(x => x.UserName == username))
        {
            await LogService.Log(LogType.FileDeleteAttempt, $"User {username} was not found.", db, null);
            return Results.NotFound();
        }
        
        var user = db.Users
            .Include(x => x.Files)
            .ThenInclude(x => x.Revisions)
            .First(x => x.UserName == username);
        var userFiles = user.Files;

        var fileToDownload = userFiles.First(x => x.Id == id);
        var revisionToDownload = fileToDownload.Revisions.OrderBy(x => x.Created).Last();
        
        var objectStream = await GetObject(revisionToDownload.ObjectName);
        if (objectStream.InputStream is null)
        {
            await LogService.Log(LogType.FileDownloadAttempt, $"{username} attempted to download a file, but the file was not found.", db, user);
            return Results.NotFound();
        }
        
        await LogService.Log(LogType.FileDownload, $"{username} downloaded a file called {fileToDownload.Name}.", db, user);
        return Results.File(objectStream.InputStream, fileToDownload.ContentType, fileToDownload.Name);
    }
    
    public static async Task<IResult> DownloadMultipleFiles(
        HttpContext context,
        MyDbContext db,
        List<Guid> ids)
    {
        var endpointUser = context.User;
        var username = endpointUser.Identity?.Name;
        if (username == null)
        {
            await LogService.Log(LogType.FileDownloadAttempt, "There was an attempt to delete a file, but user's credentials are incorrect.", db, null);
            return Results.Unauthorized();
        }

        if (!db.Users.Any() || !db.Users.Any(x => x.UserName == username))
        {
            await LogService.Log(LogType.FileDownloadAttempt, $"User {username} was not found.", db, null);
            return Results.NotFound();
        }
        
        var user = db.Users
            .Include(x => x.Files)
            .ThenInclude(x => x.Revisions)
            .First(x => x.UserName == username);
        var userFiles = user.Files;

        if (!ids.All(x => userFiles.Select(y => y.Id).Contains(x)))
        {
            await LogService.Log(LogType.FileDownloadAttempt,$"{username} attempted to download a file, but the file was not found.", db, user);
            return Results.NotFound();
        }
        var filesToDownload = userFiles.Where(x => ids.Contains(x.Id));
        var toDownload = filesToDownload.ToList();
        var revisionsToDownload = toDownload.Select(x => x.Revisions.OrderBy(y => y.Created).Last());

        var objects = new List<(GetObjectResponse response, string name)>();
        
        foreach (var revision in revisionsToDownload)
        {
            var objectStream = await GetObject(revision.ObjectName);
            if (objectStream.InputStream is null)
            {
                await LogService.Log(LogType.FileDownloadAttempt, $"{username} attempted to download a file, but the file was not found.", db, user);
                return Results.NotFound();
            }

            var name = toDownload.First(x => x.Revisions.Contains(revision)).Name;
            
            objects.Add((objectStream, name));
            
            await LogService.Log(LogType.FileDownload, $"{username} downloaded a file called {name}.", db, user);
        }
        
        var archive = CreateZipArchive(objects);
        
        return Results.File(archive, "application/zip", $"download_{username}_{DateTime.UtcNow:yyyy-MM-dd_HH-mm-ss}.zip");
    }

    private static async Task SaveFile(User user, Stream fileStream, string fileName, string fileContentType, long streamLength, MyDbContext db)
    {
        var userFiles = user.Files;
        
        File fileObject;
        if (userFiles.Any(x => x.Name == fileName))
        {
            fileObject = userFiles.First(x => x.Name == fileName);
        }
        else
        { 
            fileObject = new File
            { 
                Id = Guid.NewGuid(),
                Name = fileName, 
                ContentType = fileContentType,
                Created = DateTime.UtcNow,
                Modified = DateTime.UtcNow,
                Revisions = []
            };
        }

        int revisionNumber;
        if (fileObject.Revisions.Count == 0)
        {
            revisionNumber = 1;
        }
        else
        {
            revisionNumber = int.Parse(fileObject.Revisions.OrderBy(x => x.Created).Last().ObjectName.Split('@').Last()) + 1;
        }
        
        FileRevision fileRevisionObject = new()
        {
            Id = Guid.NewGuid(),
            Created = DateTime.UtcNow,
            ObjectName = $"{user.UserName}@{fileObject.Name}@{revisionNumber}",
            Size = streamLength
        };
        fileObject.Revisions.Add(fileRevisionObject);
        
        if (userFiles.All(x => x.Name != fileName))
        {
            await db.Files.AddAsync(fileObject);
        }
        else
        {
            db.Files.Update(fileObject);
        }
        
        await db.FileRevisions.AddAsync(fileRevisionObject);
        user.Files.Add(fileObject);
        db.Users.Update(user);

        try
        {
            await PutObject(fileRevisionObject.ObjectName, fileStream);
            if (fileObject.Revisions.Count > NumberOfRevisions)
            {
                var toDelete = fileObject.Revisions.OrderBy(x => x.Created).First();
                fileObject.Revisions.Remove(toDelete);
                db.FileRevisions.Remove(toDelete);
                await DeleteObject(toDelete.ObjectName);
            }
            await db.SaveChangesAsync();
        }
        catch (Exception e)
        {
            await LogService.Log(LogType.FileUploadAttempt, $"{user.UserName}'s file upload attempt failed. Message: {e.Message}", db, user);
            throw;
        }
        
        await LogService.Log(LogType.FileUpload, $"{user.UserName} uploaded a file called {fileName}", db, user);
    }
    
    private static async Task PutObject(string objectName, Stream file)
    { 
        var putObjectRequest = new PutObjectRequest
        {
            BucketName = BucketName,
            NamespaceName = NamespaceName,
            ObjectName = objectName,
            PutObjectBody = file,
            
        };
        
        await Client.PutObject(putObjectRequest);
    }

    private static async Task<GetObjectResponse> GetObject(string objectName)
    {
        var getObjectRequest = new GetObjectRequest
        {
            BucketName = BucketName,
            NamespaceName = NamespaceName,
            ObjectName = objectName
        };

        return await Client.GetObject(getObjectRequest);
    }

    private static async Task DeleteObject(string objectName)
    {
        var deleteObjectRequest = new DeleteObjectRequest
        {
            BucketName = BucketName,
            NamespaceName = NamespaceName,
            ObjectName = objectName
        };

        await Client.DeleteObject(deleteObjectRequest);
    }
    
    public static string GetHash(Stream stream)
    {
        using var sha512 = SHA512.Create();
        var hash = sha512.ComputeHash(stream);
        return Convert.ToBase64String(hash);
    }
    
    private static bool IsZipArchiveCorrect(ZipArchive archive, List<string?> hashes)
    {
        if (hashes.Any(x => x is null)) return false;
        if (archive.Entries.Count != hashes.Count) return false;
        
        List<string> hashCopy = [..hashes!];
             
        foreach (var entry in archive.Entries)
        {
            if (entry.Length == 0) return false;
            using var stream = entry.Open();
            var hash = GetHash(stream);
            if (hashCopy.All(x => x != hash)) return false;
            hashCopy.Remove(hash);
        }

        return true;
    }
    
    private static Stream CreateZipArchive(List<(GetObjectResponse file, string name)> files)
    {
        var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var file in files)
            {
                var demoFile = archive.CreateEntry(file.name);
                using var entryStream = demoFile.Open();
                file.file.InputStream.CopyTo(entryStream);
            }
        }

        memoryStream.Seek(0, SeekOrigin.Begin);

        return memoryStream;
    }

    public static async Task<IResult> GetFreeSpaceForUser(HttpContext httpContext, MyDbContext db)
    {
        var endpointUser = httpContext.User;
        var username = endpointUser.Identity?.Name;
        if (username == null)
        {
            return Results.Unauthorized();
        }

        if (!db.Users.Any() || !db.Users.Any(x => x.UserName == username))
        {
            return Results.NotFound();
        }
        
        var user = db.Users
            .Include(x => x.Files)
            .ThenInclude(x => x.Revisions)
            .First(x => x.UserName == username);
        var userFilesSize = user.Files.Sum(x => x.Revisions.Sum(y => y.Size));
        return Results.Ok(Space - userFilesSize); 
    }
}