namespace Velune.Application.Results;

public enum ErrorType
{
    Validation = 0,
    NotFound = 1,
    Conflict = 2,
    Unauthorized = 3,
    Unsupported = 4,
    Infrastructure = 5,
    Unexpected = 6
}
