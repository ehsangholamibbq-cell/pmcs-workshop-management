using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.Modules.Finance.Contracts;
using Pmcs.Modules.Finance.Domain;
using Pmcs.Modules.Projects.Contracts;

namespace Pmcs.Modules.Finance.Services;

internal static class ProjectFinancialPositionReportingSelector
{
    public static ProjectFinancialPositionReportingSelection Select(
        ProjectFinancialPositionReportingProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var cutoff = projection.SourceCutoffUtc.ToUniversalTime();
        ValidateScope(projection, cutoff);

        var configurations = projection.Configurations
            .Select(ValidateAndNormalize)
            .OrderBy(item => item.EffectiveFromUtc)
            .ThenBy(item => item.ConfigurationVersion)
            .ToArray();
        EnsureDistinct(
            configurations.Select(item => item.ConfigurationVersion),
            "configuration.duplicate",
            "Financial reporting contains duplicate configuration versions.");
        var effectiveConfigurations = configurations
            .Where(item => IsEffectiveAt(item.EffectiveFromUtc, item.EffectiveToUtc, cutoff))
            .ToArray();
        if (effectiveConfigurations.Length > 1)
        {
            throw Invalid(
                "configuration.overlap",
                "More than one Finance configuration is effective at the reporting cutoff.");
        }

        var configuration = effectiveConfigurations.SingleOrDefault();
        var records = projection.FinancialRecords
            .Select(item => ValidateAndNormalize(item, projection.TenantId, projection.ProjectId))
            .OrderBy(item => item.TransactionDate)
            .ThenBy(item => item.Type)
            .ThenBy(item => item.RecordId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        EnsureDistinct(
            records.Select(item => item.RecordId),
            "record.duplicate",
            "Financial reporting contains duplicate record identities.");

        var obligations = projection.Obligations
            .Select(item => ValidateAndNormalize(item, projection.TenantId, projection.ProjectId))
            .OrderBy(item => item.Type)
            .ThenBy(item => item.DueDate)
            .ThenBy(item => item.NumberSnapshot, StringComparer.Ordinal)
            .ThenBy(item => item.ObligationId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        EnsureDistinct(
            obligations.Select(item => item.ObligationId),
            "obligation.duplicate",
            "Financial reporting contains duplicate obligation identities.");

        var settlements = projection.Settlements
            .Select(item => ValidateAndNormalize(item, projection.TenantId, projection.ProjectId))
            .OrderBy(item => item.SettledAt)
            .ThenBy(item => item.SettlementId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        EnsureDistinct(
            settlements.Select(item => item.SettlementId),
            "settlement.duplicate",
            "Financial reporting contains duplicate settlement identities.");

        var baselines = projection.BudgetBaselines
            .Select(item => ValidateAndNormalize(item, projection.TenantId, projection.ProjectId))
            .OrderBy(item => item.ApprovedAt)
            .ThenBy(item => item.BaselineId.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        EnsureDistinct(
            baselines.Select(item => item.BaselineId),
            "budget.duplicate",
            "Financial reporting contains duplicate Budget Baseline identities.");

        if (configuration is null &&
            (records.Length > 0 || obligations.Length > 0 || settlements.Length > 0 || baselines.Length > 0))
        {
            throw Invalid(
                "configuration.missing",
                "Financial evidence cannot be selected without an effective versioned configuration.");
        }

        var officialRecords = SelectOfficialRecords(
            records,
            configuration,
            projection.CutoffLocalDate,
            cutoff);
        var officialObligations = SelectOfficialObligations(
            obligations,
            configuration,
            projection.CutoffLocalDate,
            cutoff);
        var eligibleSettlements = SelectEligibleSettlements(
            settlements,
            records,
            obligations,
            officialRecords,
            officialObligations,
            cutoff);
        var effectiveBaseline = SelectEffectiveBudgetBaseline(baselines, configuration, cutoff);
        var classification = ResolveClassification(
            projection.Classification,
            configurations,
            records,
            obligations,
            settlements,
            baselines);
        var incompleteCollectionCount = new[]
        {
            projection.LedgerCompleteness,
            projection.ObligationCompleteness,
            projection.SettlementLineageCompleteness
        }.Count(item => item == ProjectFinancialSourceCompleteness.Incomplete);
        var counts = new ProjectFinancialSourceCounts(
            records.Length,
            officialRecords.Length,
            records.Length - officialRecords.Length,
            obligations.Length,
            officialObligations.Length,
            0,
            obligations.Length - officialObligations.Length,
            settlements.Length,
            eligibleSettlements.Length,
            settlements.Length - eligibleSettlements.Length,
            baselines.Length,
            effectiveBaseline is null ? 0 : 1,
            baselines.Length - (effectiveBaseline is null ? 0 : 1),
            incompleteCollectionCount);
        var sourceMaxChangedAt = ResolveSourceMaxChangedAt(
            configurations,
            records,
            obligations,
            settlements,
            baselines,
            cutoff);
        var manifest = BuildManifest(
            projection,
            cutoff,
            configuration,
            records,
            obligations,
            settlements,
            baselines);

        return new ProjectFinancialPositionReportingSelection(
            ProjectFinancialPositionReportingContract.Version,
            projection.TenantId,
            projection.ProjectId,
            projection.CutoffLocalDate,
            cutoff,
            configuration,
            officialRecords,
            officialObligations,
            eligibleSettlements,
            effectiveBaseline,
            projection.LedgerCompleteness,
            projection.ObligationCompleteness,
            projection.SettlementLineageCompleteness,
            classification,
            counts,
            sourceMaxChangedAt,
            manifest,
            ProjectFinancialPositionCanonicalJson.Sha256(
                ProjectFinancialPositionCanonicalJson.Serialize(manifest)));
    }

    private static void ValidateScope(
        ProjectFinancialPositionReportingProjection projection,
        DateTimeOffset cutoff)
    {
        if (!string.Equals(
                projection.ContractVersion,
                ProjectFinancialPositionReportingContract.Version,
                StringComparison.Ordinal) ||
            projection.TenantId == Guid.Empty || projection.ProjectId == Guid.Empty ||
            projection.CutoffLocalDate == default || cutoff == default ||
            projection.Configurations is null || projection.FinancialRecords is null ||
            projection.Obligations is null || projection.Settlements is null ||
            projection.BudgetBaselines is null ||
            projection.FinancialRecords.Count > ProjectFinancialPositionReportingContract.MaximumFinancialRecords ||
            projection.Obligations.Count > ProjectFinancialPositionReportingContract.MaximumObligations ||
            projection.Settlements.Count > ProjectFinancialPositionReportingContract.MaximumSettlements ||
            projection.BudgetBaselines.Count > ProjectFinancialPositionReportingContract.MaximumBudgetBaselines ||
            !Enum.IsDefined(projection.LedgerCompleteness) ||
            !Enum.IsDefined(projection.ObligationCompleteness) ||
            !Enum.IsDefined(projection.SettlementLineageCompleteness))
        {
            throw Invalid(
                "scope.invalid",
                "The financial position projection violates its bounded versioned scope.");
        }

        RequireConfidential(projection.Classification, "classification.invalid");
    }

    private static ProjectFinancialConfigurationVersion ValidateAndNormalize(
        ProjectFinancialConfigurationVersion configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var effectiveFrom = configuration.EffectiveFromUtc.ToUniversalTime();
        var effectiveTo = configuration.EffectiveToUtc?.ToUniversalTime();
        if (configuration.ConfigurationVersion <= 0 || configuration.ProjectRevision <= 0 ||
            !Enum.IsDefined(configuration.FinanceState) || !Enum.IsDefined(configuration.BudgetState) ||
            effectiveFrom == default || effectiveTo <= effectiveFrom)
        {
            throw Invalid(
                "configuration.invalid",
                "A Finance configuration version violates the reporting contract.");
        }

        RequireConfidential(configuration.Classification, "configuration.classification.invalid");
        return configuration with
        {
            BaseCurrencyCode = NormalizeCurrency(
                configuration.BaseCurrencyCode,
                "configuration.currency.invalid"),
            EffectiveFromUtc = effectiveFrom,
            EffectiveToUtc = effectiveTo
        };
    }

    private static ProjectFinancialRecordVersion ValidateAndNormalize(
        ProjectFinancialRecordVersion item,
        Guid tenantId,
        Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(item);
        var createdAt = item.CreatedAt.ToUniversalTime();
        var postedAt = item.PostedAt?.ToUniversalTime();
        if (item.RecordId == Guid.Empty || item.TenantId != tenantId || item.ProjectId != projectId ||
            item.Revision <= 0 || !Enum.IsDefined(item.Type) || !Enum.IsDefined(item.Status) ||
            item.TransactionDate == default || !IsMoney(item.Amount) || createdAt == default ||
            (item.Status == FinancialRecordStatus.Posted &&
                (!postedAt.HasValue || postedAt.Value < createdAt)) ||
            (item.Status != FinancialRecordStatus.Posted && postedAt.HasValue))
        {
            throw Invalid("record.invalid", "A Financial Record violates the reporting contract.");
        }

        RequireConfidential(item.Classification, "record.classification.invalid");
        return item with
        {
            CurrencyCode = NormalizeCurrency(item.CurrencyCode, "record.currency.invalid"),
            CreatedAt = createdAt,
            PostedAt = postedAt
        };
    }

    private static ProjectFinancialObligationVersion ValidateAndNormalize(
        ProjectFinancialObligationVersion item,
        Guid tenantId,
        Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(item);
        var createdAt = item.CreatedAt.ToUniversalTime();
        var approvedAt = item.ApprovedAt?.ToUniversalTime();
        var approvedStatus = item.Status is FinancialObligationStatus.Approved or
            FinancialObligationStatus.PartiallySettled or FinancialObligationStatus.Settled;
        if (item.ObligationId == Guid.Empty || item.TenantId != tenantId || item.ProjectId != projectId ||
            item.Revision <= 0 || !Enum.IsDefined(item.Type) || !Enum.IsDefined(item.Status) ||
            string.IsNullOrWhiteSpace(item.NumberSnapshot) || item.NumberSnapshot.Trim().Length > 80 ||
            item.CounterpartySnapshot?.Trim().Length > 200 || item.IssueDate == default ||
            item.DueDate < item.IssueDate || !IsMoney(item.Amount) || createdAt == default ||
            (approvedStatus && (!approvedAt.HasValue || approvedAt.Value < createdAt)) ||
            (!approvedStatus && approvedAt.HasValue))
        {
            throw Invalid("obligation.invalid", "A Financial Obligation violates the reporting contract.");
        }

        RequireConfidential(item.Classification, "obligation.classification.invalid");
        return item with
        {
            NumberSnapshot = item.NumberSnapshot.Trim(),
            CounterpartySnapshot = Optional(item.CounterpartySnapshot),
            CurrencyCode = NormalizeCurrency(item.CurrencyCode, "obligation.currency.invalid"),
            CreatedAt = createdAt,
            ApprovedAt = approvedAt
        };
    }

    private static ProjectFinancialSettlementVersion ValidateAndNormalize(
        ProjectFinancialSettlementVersion item,
        Guid tenantId,
        Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(item);
        var settledAt = item.SettledAt.ToUniversalTime();
        if (item.SettlementId == Guid.Empty || item.TenantId != tenantId || item.ProjectId != projectId ||
            item.ObligationId == Guid.Empty || item.FinancialRecordId == Guid.Empty ||
            !IsMoney(item.Amount) || settledAt == default)
        {
            throw Invalid("settlement.invalid", "A Financial Settlement violates the reporting contract.");
        }

        RequireConfidential(item.Classification, "settlement.classification.invalid");
        return item with { SettledAt = settledAt };
    }

    private static ProjectBudgetBaselineVersion ValidateAndNormalize(
        ProjectBudgetBaselineVersion item,
        Guid tenantId,
        Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(item);
        var createdAt = item.CreatedAt.ToUniversalTime();
        var approvedAt = item.ApprovedAt?.ToUniversalTime();
        var supersededAt = item.SupersededAt?.ToUniversalTime();
        var approved = item.Status == BudgetBaselineStatus.Approved;
        var superseded = item.Status == BudgetBaselineStatus.Superseded;
        if (item.BaselineId == Guid.Empty || item.TenantId != tenantId || item.ProjectId != projectId ||
            item.Revision <= 0 || !Enum.IsDefined(item.Status) || !IsMoney(item.Amount) ||
            createdAt == default ||
            (approved && (!approvedAt.HasValue || supersededAt.HasValue)) ||
            (superseded && (!approvedAt.HasValue || !supersededAt.HasValue)) ||
            (!approved && !superseded && (approvedAt.HasValue || supersededAt.HasValue)) ||
            (approvedAt.HasValue && approvedAt.Value < createdAt) ||
            supersededAt <= approvedAt)
        {
            throw Invalid("budget.invalid", "A Budget Baseline violates the reporting contract.");
        }

        RequireConfidential(item.Classification, "budget.classification.invalid");
        return item with
        {
            CurrencyCode = NormalizeCurrency(item.CurrencyCode, "budget.currency.invalid"),
            CreatedAt = createdAt,
            ApprovedAt = approvedAt,
            SupersededAt = supersededAt
        };
    }

    private static ProjectFinancialRecordVersion[] SelectOfficialRecords(
        ProjectFinancialRecordVersion[] records,
        ProjectFinancialConfigurationVersion? configuration,
        DateOnly cutoffLocalDate,
        DateTimeOffset cutoff)
    {
        var selected = records
            .Where(item => item.Status == FinancialRecordStatus.Posted &&
                item.PostedAt!.Value <= cutoff && item.TransactionDate <= cutoffLocalDate)
            .ToArray();
        if (selected.Length > 0 && configuration is null)
        {
            throw Invalid("configuration.missing", "Official Financial Records require an effective configuration.");
        }
        if (selected.Any(item => !string.Equals(
            item.CurrencyCode,
            configuration!.BaseCurrencyCode,
            StringComparison.Ordinal)))
        {
            throw Invalid(
                "record.currency_mismatch",
                "An official Financial Record does not use the effective Project Base Currency.");
        }

        return selected;
    }

    private static ProjectFinancialObligationVersion[] SelectOfficialObligations(
        ProjectFinancialObligationVersion[] obligations,
        ProjectFinancialConfigurationVersion? configuration,
        DateOnly cutoffLocalDate,
        DateTimeOffset cutoff)
    {
        var selected = obligations
            .Where(item => item.Status is FinancialObligationStatus.Approved or
                    FinancialObligationStatus.PartiallySettled or FinancialObligationStatus.Settled)
            .Where(item => item.ApprovedAt!.Value <= cutoff && item.IssueDate <= cutoffLocalDate)
            .ToArray();
        if (selected.Length > 0 && configuration is null)
        {
            throw Invalid("configuration.missing", "Official obligations require an effective configuration.");
        }
        if (selected.Any(item => !string.Equals(
            item.CurrencyCode,
            configuration!.BaseCurrencyCode,
            StringComparison.Ordinal)))
        {
            throw Invalid(
                "obligation.currency_mismatch",
                "An official obligation does not use the effective Project Base Currency.");
        }

        return selected;
    }

    private static ProjectFinancialSettlementVersion[] SelectEligibleSettlements(
        ProjectFinancialSettlementVersion[] settlements,
        ProjectFinancialRecordVersion[] records,
        ProjectFinancialObligationVersion[] obligations,
        ProjectFinancialRecordVersion[] officialRecords,
        ProjectFinancialObligationVersion[] officialObligations,
        DateTimeOffset cutoff)
    {
        var recordsById = records.ToDictionary(item => item.RecordId);
        var obligationsById = obligations.ToDictionary(item => item.ObligationId);
        foreach (var settlement in settlements)
        {
            if (!recordsById.TryGetValue(settlement.FinancialRecordId, out var record) ||
                !obligationsById.TryGetValue(settlement.ObligationId, out var obligation) ||
                record.Status != FinancialRecordStatus.Posted ||
                obligation.Status is not (FinancialObligationStatus.Approved or
                    FinancialObligationStatus.PartiallySettled or FinancialObligationStatus.Settled) ||
                !record.PostedAt.HasValue || !obligation.ApprovedAt.HasValue ||
                record.PostedAt.Value > settlement.SettledAt ||
                obligation.ApprovedAt.Value > settlement.SettledAt ||
                !string.Equals(record.CurrencyCode, obligation.CurrencyCode, StringComparison.Ordinal) ||
                (obligation.Type == FinancialObligationType.Payable &&
                    record.Type != FinancialRecordType.Payment) ||
                (obligation.Type == FinancialObligationType.Receivable &&
                    record.Type != FinancialRecordType.Receipt))
            {
                throw Invalid(
                    "settlement.lineage.invalid",
                    "A settlement does not have valid obligation and official Financial Record lineage.");
            }
        }

        var officialRecordIds = officialRecords.Select(item => item.RecordId).ToHashSet();
        var officialObligationIds = officialObligations.Select(item => item.ObligationId).ToHashSet();
        var selected = settlements
            .Where(item => item.SettledAt <= cutoff &&
                officialRecordIds.Contains(item.FinancialRecordId) &&
                officialObligationIds.Contains(item.ObligationId))
            .ToArray();
        foreach (var group in selected.GroupBy(item => item.ObligationId))
        {
            if (group.Sum(item => item.Amount) > obligationsById[group.Key].Amount)
            {
                throw Invalid(
                    "settlement.overallocated",
                    "Settlement lineage exceeds the official obligation amount at the cutoff.");
            }
        }
        foreach (var group in selected.GroupBy(item => item.FinancialRecordId))
        {
            if (group.Sum(item => item.Amount) > recordsById[group.Key].Amount)
            {
                throw Invalid(
                    "settlement.record_overallocated",
                    "Settlement lineage exceeds the linked official Financial Record amount.");
            }
        }

        return selected;
    }

    private static ProjectBudgetBaselineVersion? SelectEffectiveBudgetBaseline(
        ProjectBudgetBaselineVersion[] baselines,
        ProjectFinancialConfigurationVersion? configuration,
        DateTimeOffset cutoff)
    {
        var effective = baselines
            .Where(item => item.ApprovedAt.HasValue && item.ApprovedAt.Value <= cutoff &&
                (!item.SupersededAt.HasValue || cutoff < item.SupersededAt.Value))
            .ToArray();
        if (effective.Length > 1)
        {
            throw Invalid(
                "budget.overlap",
                "More than one official Budget Baseline is effective at the reporting cutoff.");
        }
        if (effective.Length == 1 && (configuration is null || !string.Equals(
            effective[0].CurrencyCode,
            configuration.BaseCurrencyCode,
            StringComparison.Ordinal)))
        {
            throw Invalid(
                "budget.currency_mismatch",
                "The effective Budget Baseline does not use the Project Base Currency.");
        }

        return effective.SingleOrDefault();
    }

    private static ProjectFinancialPositionReportingClassification ResolveClassification(
        ProjectFinancialPositionReportingClassification source,
        ProjectFinancialConfigurationVersion[] configurations,
        ProjectFinancialRecordVersion[] records,
        ProjectFinancialObligationVersion[] obligations,
        ProjectFinancialSettlementVersion[] settlements,
        ProjectBudgetBaselineVersion[] baselines) =>
        new[] { source }
            .Concat(configurations.Select(item => item.Classification))
            .Concat(records.Select(item => item.Classification))
            .Concat(obligations.Select(item => item.Classification))
            .Concat(settlements.Select(item => item.Classification))
            .Concat(baselines.Select(item => item.Classification))
            .Max();

    private static DateTimeOffset? ResolveSourceMaxChangedAt(
        ProjectFinancialConfigurationVersion[] configurations,
        ProjectFinancialRecordVersion[] records,
        ProjectFinancialObligationVersion[] obligations,
        ProjectFinancialSettlementVersion[] settlements,
        ProjectBudgetBaselineVersion[] baselines,
        DateTimeOffset cutoff)
    {
        var values = new List<DateTimeOffset>();
        foreach (var configuration in configurations)
        {
            AddAtOrBefore(values, configuration.EffectiveFromUtc, cutoff);
            AddAtOrBefore(values, configuration.EffectiveToUtc, cutoff);
        }
        foreach (var item in records)
        {
            AddAtOrBefore(values, item.CreatedAt, cutoff);
            AddAtOrBefore(values, item.PostedAt, cutoff);
        }
        foreach (var item in obligations)
        {
            AddAtOrBefore(values, item.CreatedAt, cutoff);
            AddAtOrBefore(values, item.ApprovedAt, cutoff);
        }
        foreach (var item in settlements)
        {
            AddAtOrBefore(values, item.SettledAt, cutoff);
        }
        foreach (var item in baselines)
        {
            AddAtOrBefore(values, item.CreatedAt, cutoff);
            AddAtOrBefore(values, item.ApprovedAt, cutoff);
            AddAtOrBefore(values, item.SupersededAt, cutoff);
        }

        return values.Count == 0 ? null : values.Max().ToUniversalTime();
    }

    private static ProjectFinancialPositionSourceManifest BuildManifest(
        ProjectFinancialPositionReportingProjection projection,
        DateTimeOffset cutoff,
        ProjectFinancialConfigurationVersion? configuration,
        ProjectFinancialRecordVersion[] records,
        ProjectFinancialObligationVersion[] obligations,
        ProjectFinancialSettlementVersion[] settlements,
        ProjectBudgetBaselineVersion[] baselines) => new(
        ProjectFinancialPositionReportingContract.SourceManifestVersion,
        ProjectFinancialPositionReportingContract.Version,
        ProjectFinancialPositionReportingContract.PolicyVersion,
        projection.TenantId,
        projection.ProjectId,
        projection.CutoffLocalDate,
        cutoff,
        configuration is null
            ? null
            : new ProjectFinancialConfigurationManifest(
                configuration.ConfigurationVersion,
                configuration.ProjectRevision,
                configuration.FinanceState,
                configuration.BudgetState,
                configuration.BaseCurrencyCode,
                configuration.EffectiveFromUtc,
                configuration.EffectiveToUtc),
        records.Select(item => new ProjectFinancialRecordManifest(
            item.RecordId,
            item.Revision,
            item.Type,
            item.Status,
            item.TransactionDate,
            item.PostedAt,
            DefinitionHash(item))).ToArray(),
        obligations.Select(item => new ProjectFinancialObligationManifest(
            item.ObligationId,
            item.Revision,
            item.Type,
            item.Status,
            item.IssueDate,
            item.DueDate,
            item.ApprovedAt,
            DefinitionHash(item))).ToArray(),
        settlements.Select(item => new ProjectFinancialSettlementManifest(
            item.SettlementId,
            item.ObligationId,
            item.FinancialRecordId,
            item.SettledAt,
            DefinitionHash(item))).ToArray(),
        baselines.Select(item => new ProjectBudgetBaselineManifest(
            item.BaselineId,
            item.Revision,
            item.Status,
            item.ApprovedAt,
            item.SupersededAt,
            DefinitionHash(item))).ToArray(),
        projection.LedgerCompleteness,
        projection.ObligationCompleteness,
        projection.SettlementLineageCompleteness);

    private static string DefinitionHash<T>(T item) =>
        ProjectFinancialPositionCanonicalJson.Sha256(
            ProjectFinancialPositionCanonicalJson.Serialize(item));

    private static void AddAtOrBefore(
        List<DateTimeOffset> values,
        DateTimeOffset? candidate,
        DateTimeOffset cutoff)
    {
        if (candidate.HasValue && candidate.Value.ToUniversalTime() <= cutoff)
        {
            values.Add(candidate.Value.ToUniversalTime());
        }
    }

    private static void EnsureDistinct<T>(
        IEnumerable<T> values,
        string suffix,
        string message)
        where T : notnull
    {
        var all = values.ToArray();
        if (all.Distinct().Count() != all.Length)
        {
            throw Invalid(suffix, message);
        }
    }

    private static bool IsEffectiveAt(
        DateTimeOffset effectiveFromUtc,
        DateTimeOffset? effectiveToUtc,
        DateTimeOffset cutoffUtc) =>
        effectiveFromUtc <= cutoffUtc && (!effectiveToUtc.HasValue || cutoffUtc < effectiveToUtc.Value);

    private static bool IsMoney(decimal value) =>
        value > 0 && decimal.Round(value, 2, MidpointRounding.AwayFromZero) == value;

    private static string NormalizeCurrency(string value, string suffix)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length != 3 || normalized.Any(character => !char.IsAsciiLetterUpper(character)))
        {
            throw Invalid(suffix, "Currency must be a three-letter uppercase ISO-style code.");
        }

        return normalized;
    }

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void RequireConfidential(
        ProjectFinancialPositionReportingClassification classification,
        string suffix)
    {
        if (!Enum.IsDefined(classification) ||
            classification < ProjectFinancialPositionReportingClassification.Confidential)
        {
            throw Invalid(
                suffix,
                "Certified financial evidence must have an explicit Confidential or Restricted classification.");
        }
    }

    private static DomainRuleException Invalid(string suffix, string message) =>
        new($"finance.financial_position_reporting.{suffix}", message);
}

internal static class ProjectFinancialPositionCanonicalJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize<T>(T value)
    {
        var element = JsonSerializer.SerializeToElement(value, SerializerOptions);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteCanonical(writer, element);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string Sha256(string canonicalJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson))).ToLowerInvariant();

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(
                    item => item.Name,
                    StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(writer, item);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(element), element.ValueKind, null);
        }
    }
}
