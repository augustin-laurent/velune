using Velune.Domain.Documents;

namespace Velune.Application.DTOs;

public sealed record DocumentTextLoadResult(
    DocumentTextIndex? Index,
    bool RequiresOcr,
    bool UsedCache);
