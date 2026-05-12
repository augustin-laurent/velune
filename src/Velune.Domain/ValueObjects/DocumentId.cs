namespace Velune.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a document session.
/// </summary>
public readonly record struct DocumentId(Guid Value)
{
    /// <summary>
    /// Generates a new unique document identifier.
    /// </summary>
    /// <returns>A new <see cref="DocumentId"/> wrapping a fresh GUID.</returns>
    public static DocumentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
