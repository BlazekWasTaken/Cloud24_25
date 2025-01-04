using Cloud24_25.Infrastructure;
using Cloud24_25.Infrastructure.Model;
using Microsoft.AspNetCore.Http;
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

    private static readonly ObjectStorageClient Client = new(
        new ConfigFileAuthenticationDetailsProvider("DEFAULT"),
        new ClientConfiguration
        {
            ClientUserAgent = "Cloud24_25",
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
        MyDbContext db)
    {
        var endpointUser = context.User;
        var username = endpointUser.Identity?.Name;
        if (username == null)
        {
            await LogService.Log("There was an attempt to upload a file, but user's credentials are incorrect.", db);
            await db.SaveChangesAsync();
            return Results.Unauthorized();
        }

        File fileObject;
        if (!db.Users.Any() || !db.Users.Any(x => x.UserName == username))
        {
            await LogService.Log($"User {username} was not found.", db);
            await db.SaveChangesAsync();
            return Results.NotFound();
        }
        var user = db.Users
            .Include(x => x.Files)
            .ThenInclude(x => x.Revisions)
            .First(x => x.UserName == username);
        var userFiles = user.Files;
        
        if (userFiles.Any(x => x.Name == myFile.FileName))
        {
            fileObject = userFiles.First(x => x.Name == myFile.FileName);
        }
        else
        { 
            fileObject = new File
            { 
                Id = Guid.NewGuid(),
                Name = myFile.FileName, 
                ContentType = myFile.ContentType,
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
            ObjectName = $"{user.UserName}@{fileObject.Name}@{revisionNumber}"
        };
        fileObject.Revisions.Add(fileRevisionObject);
        
        if (userFiles.All(x => x.Name != myFile.FileName))
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
            await PutObject(fileRevisionObject.ObjectName, myFile.OpenReadStream());
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
            // TODO: test if this doesn't save bad data when errored
            await LogService.Log($"{username}'s file upload attempt failed. Message: {e.Message}", db);
            throw;
        }
        
        await LogService.Log($"{username} uploaded a file called {myFile.FileName}", db);
        
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
            await LogService.Log("There was an attempt to list files, but user's credentials are incorrect.", db);
            return Results.Unauthorized();
        }
        if (!db.Users.Any() || !db.Users.Any(x => x.UserName == username))
        {
            await LogService.Log($"User {username} was not found.", db);
            return Results.NotFound();
        }
        
        var user = db.Users
            .Include(x => x.Files)
            .ThenInclude(x => x.Revisions)
            .First(x => x.UserName == username);
        var userFiles = user.Files;
        
        await LogService.Log($"User {username} listed {userFiles.Count} files.", db);
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
            await LogService.Log("There was an attempt to delete a file, but user's credentials are incorrect.", db);
            return Results.Unauthorized();
        }
        if (!db.Users.Any() || !db.Users.Any(x => x.UserName == username))
        {
            await LogService.Log($"User {username} was not found.", db);
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
            await LogService.Log($"{username}'s file delete attempt failed. Message: {e.Message}", db);
            throw;
        }

        await LogService.Log($"{username} deleted a file called {fileToDelete.Name}", db);
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
            await LogService.Log("There was an attempt to delete a file, but user's credentials are incorrect.", db);
            return Results.Unauthorized();
        }

        if (!db.Users.Any() || !db.Users.Any(x => x.UserName == username))
        {
            await LogService.Log($"User {username} was not found.", db);
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
            await LogService.Log($"{username} attempted to download a file, but the file was not found.", db);
            return Results.NotFound();
        }
        
        await LogService.Log($"{username} downloaded a file called {fileToDownload.Name}.", db);
        
        return Results.File(objectStream.InputStream, fileToDownload.ContentType, fileToDownload.Name);
    }
    
    private static async Task PutObject(string objectName, Stream file)
    { 
        var putObjectRequest = new PutObjectRequest
        {
            BucketName = BucketName,
            NamespaceName = NamespaceName,
            ObjectName = objectName,
            PutObjectBody = file
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
}