namespace Velune.Application.Results;

/// <summary>Categorizes application errors by their nature.</summary>
public enum ErrorType
{
    /// <summary>Input validation failure.</summary>
    Validation = 0,
    /// <summary>Requested resource was not found.</summary>
    NotFound = 1,
    /// <summary>Operation conflicts with current state.</summary>
    Conflict = 2,
    /// <summary>Caller is not authorized.</summary>
    Unauthorized = 3,
    /// <summary>Operation is not supported.</summary>
    Unsupported = 4,
    /// <summary>Infrastructure or external service failure.</summary>
    Infrastructure = 5,
    /// <summary>An unexpected or unclassified error.</summary>
    Unexpected = 6
}
