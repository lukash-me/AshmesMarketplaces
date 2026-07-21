namespace AshmesMarketplaces.Domain.Shared;

public class Errors
{
    public static class General
    {
        public static Error ValueIsInvalid(string? name = null)
        {
            var label = name ?? "value";
            return Error.Validation("value.is.invalid", $"{label} is invalid");
        }

        public static Error NotFound(Guid? id = null)
        {
            var forId = id == null ? "" : $" for id '{id}'";
            return Error.NotFound("record.not.found", $"record not found{forId}");
        }
        
        public static Error ValueIsRequired(string? name = null)
        {
            var label = name ?? "value";
            return Error.Validation("value.is.required", $"{name} is required");
        }
        
        public static Error InvalidLength(string? name = null)
        {
            var label = name ?? "value";
            return Error.Validation("value.length.is.invalid", $"{name} has invalid length");
        }
        
        public static Error InvalidLink(string? name = null)
        {
            var label = name ?? "link";
            return Error.Validation("link.is.invalid", $"{name} is invalid");
        }
    }
    
    public static class Minio
    {
        public static Error BucketNotFound(string? name = null)
        {
            var label = name ?? "value";
            return Error.NotFound("bucket.not.found", $"No such bucket with name {name}");
        }
        
        public static Error FileNotFound(string? path = null)
        {
            var label = path ?? "value";
            return Error.NotFound("file.not.found", $"No such file with path {path}");
        }
        
        public static Error FileCheckFailure(string? path = null)
        {
            var label = path ?? "value";
            return Error.Failure("file.check", $"Can not to check if file exist with path {path}");
        }
        
        public static Error DownloadFailure(string? path = null)
        {
            var label = path ?? "value";
            return Error.Failure("file.download", $"Fail to download file from minio with path {path}");
        }
        
        public static Error UploadFailure(string? path = null)
        {
            var label = path ?? "value";
            return Error.Failure("file.upload", $"Fail to upload file to minio with path {path}");
        }
        
        public static Error DeleteFailure(string? path = null)
        {
            var label = path ?? "value";
            return Error.Failure("file.delete", $"Fail to delete file into minio with path {path}");
        }
        
    }
}