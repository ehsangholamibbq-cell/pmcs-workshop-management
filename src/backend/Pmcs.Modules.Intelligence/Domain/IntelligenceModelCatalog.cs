namespace Pmcs.Modules.Intelligence.Domain;

internal sealed class IntelligenceModelCatalog
{
    public Guid Id { get; private set; }
    public int Version { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public ModelCapability Capabilities { get; private set; }
    public IntelligenceDataClass MaximumDataClass { get; private set; }
    public long InputMicrounitsPerToken { get; private set; }
    public long OutputMicrounitsPerToken { get; private set; }
    public bool Enabled { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public long Revision { get; private set; }

    private IntelligenceModelCatalog() { }

    internal static IntelligenceModelCatalog Create(
        Guid id, int version, string provider, string model, ModelCapability capabilities,
        IntelligenceDataClass maximumDataClass, Guid createdBy, DateTimeOffset now,
        long inputMicrounitsPerToken, long outputMicrounitsPerToken)
    {
        if (id == Guid.Empty || version < 1 || createdBy == Guid.Empty ||
            provider is not ("OpenAI" or "GoogleGemini" or "AnthropicClaude") ||
            string.IsNullOrWhiteSpace(model) || model.Length > 160 ||
            capabilities == ModelCapability.None ||
            (capabilities & ~(ModelCapability.StructuredOutput | ModelCapability.ToolCalling)) != 0 ||
            !Enum.IsDefined(maximumDataClass) ||
            inputMicrounitsPerToken is < 1 or > 1_000_000 ||
            outputMicrounitsPerToken is < 1 or > 1_000_000)
        {
            throw new ArgumentException("Invalid INT1 model catalog entry.");
        }

        return new IntelligenceModelCatalog
        {
            Id = id, Version = version, Provider = provider, Model = model,
            Capabilities = capabilities, MaximumDataClass = maximumDataClass,
            CreatedBy = createdBy, CreatedAt = now, Revision = 1,
            InputMicrounitsPerToken = inputMicrounitsPerToken,
            OutputMicrounitsPerToken = outputMicrounitsPerToken
        };
    }

    internal void Verify(string provider, string model, DateTimeOffset now)
    {
        if (!string.Equals(Provider, provider, StringComparison.Ordinal) ||
            !string.Equals(Model, model, StringComparison.Ordinal) || now < CreatedAt)
            throw new InvalidOperationException("Connection verification does not match this model version.");
        VerifiedAt = now;
        Enabled = true;
        Revision++;
    }

    internal void Disable()
    {
        if (!Enabled) return;
        Enabled = false;
        Revision++;
    }

    internal ModelCatalogEntry ToPolicy() => new(Id, Version, Provider, Model,
        Capabilities, MaximumDataClass, Enabled, VerifiedAt is not null,
        InputMicrounitsPerToken, OutputMicrounitsPerToken);
}
