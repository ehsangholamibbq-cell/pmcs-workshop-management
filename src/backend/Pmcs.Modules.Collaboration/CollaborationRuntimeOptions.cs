using Microsoft.Extensions.Configuration;

namespace Pmcs.Modules.Collaboration;

public sealed record CollaborationRuntimeOptions(bool Enabled)
{
    public static CollaborationRuntimeOptions Create(IConfiguration configuration)
    {
        var value = configuration["Collaboration:Enabled"];
        return new CollaborationRuntimeOptions(value is null ? false :
            bool.TryParse(value, out var enabled) ? enabled :
            throw new InvalidOperationException("Collaboration:Enabled must be a boolean."));
    }
}
