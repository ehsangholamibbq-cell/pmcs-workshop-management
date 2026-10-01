namespace Pmcs.Modules.Reporting.Domain;

internal sealed class ReportDefinitionRecord
{
    private ReportDefinitionRecord()
    {
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public ReportDefinitionScope Scope { get; private set; }
    public ReportClassification Classification { get; private set; }
    public string SupportedFormatsJson { get; private set; } = "[]";
    public string RequiredPermissionsJson { get; private set; } = "[]";
    public string ParameterSchemaVersion { get; private set; } = string.Empty;
    public ReportDefinitionStatus Status { get; private set; }
    public Guid CurrentTemplateVersionId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}

internal sealed class ReportTemplateVersionRecord
{
    private ReportTemplateVersionRecord()
    {
    }

    public Guid Id { get; private set; }
    public Guid DefinitionId { get; private set; }
    public string Version { get; private set; } = string.Empty;
    public string RendererContractVersion { get; private set; } = string.Empty;
    public string LayoutContractVersion { get; private set; } = string.Empty;
    public string ContentDigest { get; private set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; private set; }
    public DateTimeOffset? RetiredAt { get; private set; }
    public string PageSize { get; private set; } = string.Empty;
    public string Orientation { get; private set; } = string.Empty;
    public string Locale { get; private set; } = string.Empty;
    public string Calendar { get; private set; } = string.Empty;
}
