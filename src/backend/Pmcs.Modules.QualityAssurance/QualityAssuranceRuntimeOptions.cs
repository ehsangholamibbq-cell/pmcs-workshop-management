using System.Collections;
using System.Data.Common;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Pmcs.Modules.QualityAssurance;

public sealed record QualityAssuranceRuntimeOptions(
    bool Enabled,
    string EnvironmentName,
    string DatabaseName,
    string AuthenticationKey,
    string ReleaseCommit,
    string ReleaseVersion,
    string ReleaseBuiltAt)
{
    public const string EnabledConfigurationKey = "PMCS_QA_GATEWAY_ENABLED";
    public const string AuthenticationKeyConfigurationKey = "PMCS_QA_AUTH_KEY";
    public const string DatabasePrefix = "pmcs_qa_";

    public static QualityAssuranceRuntimeOptions Create(
        IHostEnvironment environment,
        IConfiguration configuration,
        string releaseCommit,
        string releaseVersion,
        string releaseBuiltAt)
    {
        var enabled = ReadStrictBoolean(configuration, EnabledConfigurationKey);
        if (!enabled)
        {
            return new(
                false,
                environment.EnvironmentName,
                string.Empty,
                string.Empty,
                releaseCommit,
                releaseVersion,
                releaseBuiltAt);
        }

        if (!environment.IsDevelopment() &&
            !environment.IsEnvironment("QA"))
        {
            throw new InvalidOperationException(
                $"{EnabledConfigurationKey} is allowed only in Development or QA.");
        }

        var connectionString = configuration.GetConnectionString("Pmcs")
            ?? throw new InvalidOperationException("Connection string 'Pmcs' is required.");
        var databaseName = QaDatabaseSafety.RequireIsolatedDatabase(connectionString);
        var authenticationKey = configuration[AuthenticationKeyConfigurationKey];
        ValidateAuthenticationKey(authenticationKey);

        return new(
            true,
            environment.EnvironmentName,
            databaseName,
            authenticationKey!,
            releaseCommit,
            releaseVersion,
            releaseBuiltAt);
    }

    private static bool ReadStrictBoolean(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return bool.TryParse(value, out var enabled)
            ? enabled
            : throw new InvalidOperationException($"{key} must be either true or false.");
    }

    private static void ValidateAuthenticationKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            Encoding.UTF8.GetByteCount(value) < 32 ||
            Encoding.UTF8.GetByteCount(value) > 512 ||
            value.Any(char.IsControl))
        {
            throw new InvalidOperationException(
                $"{AuthenticationKeyConfigurationKey} must contain 32 to 512 bytes without control characters.");
        }
    }
}

public static class QaDatabaseSafety
{
    public static string RequireIsolatedDatabase(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The QA database connection string is required.");
        }

        DbConnectionStringBuilder builder;
        try
        {
            builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException("The QA database connection string is invalid.", exception);
        }

        var databaseName = ReadDatabaseName(builder);
        if (string.IsNullOrWhiteSpace(databaseName) ||
            !databaseName.StartsWith(QualityAssuranceRuntimeOptions.DatabasePrefix, StringComparison.OrdinalIgnoreCase) ||
            databaseName.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-'))
        {
            throw new InvalidOperationException(
                $"The QA database name must start with '{QualityAssuranceRuntimeOptions.DatabasePrefix}' and contain only ASCII letters, digits, '_' or '-'.");
        }

        return databaseName;
    }

    private static string? ReadDatabaseName(DbConnectionStringBuilder builder)
    {
        foreach (DictionaryEntry entry in builder)
        {
            var key = Convert.ToString(entry.Key, System.Globalization.CultureInfo.InvariantCulture);
            if (string.Equals(key, "Database", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "Initial Catalog", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToString(entry.Value, System.Globalization.CultureInfo.InvariantCulture)?.Trim();
            }
        }

        return null;
    }
}
