using System.Text.Json;
using System.Text.Json.Serialization;
using Pmcs.BuildingBlocks.Domain;

namespace Pmcs.Modules.TechnicalOffice.Domain;

internal static class TechnicalOfficeRules
{
    internal static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static void Identity(params Guid[] values)
    {
        if (values.Any(value => value == Guid.Empty))
        {
            throw new DomainRuleException("technical.identity.required", "All required identifiers must be present.");
        }
    }

    public static string Required(string? value, int maximumLength, string code)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainRuleException(code, "A required value is missing.");
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new DomainRuleException(code, $"Value must be at most {maximumLength} characters.");
        }

        return normalized;
    }

    public static string? Optional(string? value, int maximumLength, string code)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new DomainRuleException(code, $"Value must be at most {maximumLength} characters.");
        }

        return normalized;
    }

    public static string Sha256(string? value)
    {
        var normalized = Required(value, 64, "technical.file.sha256.invalid").ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new DomainRuleException("technical.file.sha256.invalid", "SHA-256 must contain 64 hexadecimal characters.");
        }

        return normalized;
    }

    public static void Revision(long current, long supplied, string code)
    {
        if (current != supplied)
        {
            throw new DomainRuleException(code, "The record changed after it was loaded.");
        }
    }

    public static string SerializeIds(IReadOnlyCollection<Guid>? ids, int maximum, string code)
    {
        var normalized = ids?.Where(id => id != Guid.Empty).Distinct().ToArray() ?? [];
        if (normalized.Length > maximum || (ids?.Any(id => id == Guid.Empty) ?? false))
        {
            throw new DomainRuleException(code, $"At most {maximum} valid references are allowed.");
        }

        return JsonSerializer.Serialize(normalized, JsonOptions);
    }

    public static IReadOnlyCollection<Guid> DeserializeIds(string json) =>
        JsonSerializer.Deserialize<Guid[]>(json, JsonOptions) ?? [];

    public static string OfficialNumber(string prefix, DateTimeOffset createdAt, Guid id) =>
        $"{prefix}-{PersianDateCode.FromInstant(createdAt)}-{id:N}"[..(prefix.Length + 1 + 8 + 1 + 8)].ToUpperInvariant();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public enum TechnicalDocumentType
{
    Drawing = 1,
    Specification = 2,
    MethodStatement = 3,
    MaterialSubmittal = 4,
    ShopDrawing = 5,
    CalculationOrReport = 6,
    MeetingMinute = 7,
    Correspondence = 8,
    Instruction = 9,
    MeasurementSheet = 10,
    HandoverOrTestRecord = 11,
    Other = 12
}

public enum DocumentRevisionPurpose
{
    WorkInProgress = 1,
    ForReview = 2,
    ForApproval = 3,
    Approved = 4,
    ApprovedWithComments = 5,
    ForConstruction = 6,
    AsBuilt = 7,
    Rejected = 8
}

public enum DocumentRevisionStatus
{
    Draft = 1,
    Submitted = 2,
    Returned = 3,
    Approved = 4,
    Issued = 5,
    Superseded = 6
}

public enum TransmittalStatus
{
    Draft = 1,
    Issued = 2,
    Acknowledged = 3
}

public enum RfiStatus
{
    Draft = 1,
    InternalReview = 2,
    Submitted = 3,
    Answered = 4,
    ResponseAccepted = 5,
    ClarificationRequired = 6,
    Closed = 7
}

[Flags]
public enum PotentialImpact
{
    None = 0,
    Time = 1,
    Cost = 2,
    Quality = 4,
    Scope = 8,
    Safety = 16
}

public enum RfiResponseClassification
{
    InformationOnly = 1,
    DesignClarification = 2,
    NewRevisionRequired = 3,
    InstructionPotential = 4,
    ChangePotential = 5
}

public enum TechnicalSubmittalType
{
    MaterialOrProductData = 1,
    ShopDrawing = 2,
    MethodStatement = 3,
    SampleOrMockup = 4,
    TechnicalCalculation = 5,
    VendorDocument = 6,
    TestOrCertificate = 7,
    AsBuiltOrHandover = 8
}

public enum SubmittalStatus
{
    Draft = 1,
    Submitted = 2,
    UnderReview = 3,
    Approved = 4,
    ApprovedAsNoted = 5,
    ReviseAndResubmit = 6,
    Rejected = 7,
    Closed = 8
}

public enum SubmittalReviewOutcome
{
    Approved = 1,
    ApprovedAsNoted = 2,
    ReviseAndResubmit = 3,
    Rejected = 4,
    ForInformation = 5
}
