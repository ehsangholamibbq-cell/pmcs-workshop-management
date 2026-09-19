using System.Diagnostics.Metrics;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Pmcs.BuildingBlocks.Domain;
using Pmcs.BuildingBlocks.Modules;
using Pmcs.Modules.FieldOperations.Contracts;
using Pmcs.Modules.IdentityAccess.Services;
using Pmcs.Modules.Projects.Contracts;
using Pmcs.Modules.Projects.Domain;
using Pmcs.Modules.Reporting;
using Pmcs.Modules.Reporting.Domain;
using Pmcs.Modules.Reporting.Endpoints;
using Pmcs.Modules.Reporting.Rendering;
using Pmcs.Modules.Reporting.Services;

namespace Pmcs.Domain.Tests;

public sealed class ReportingTests
{
    private static readonly DateTimeOffset Cutoff = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DescriptorPublishesCertifiedReportingAndReadOnlyAgentContracts()
    {
        var descriptor = new ReportingModule().Descriptor;

        Assert.Equal("reporting.center", descriptor.ModuleId);
        Assert.False(descriptor.IsLegacy);
        Assert.Contains("projects.core", descriptor.Dependencies);
        Assert.Contains("legacy.fieldoperations", descriptor.Dependencies);
        Assert.Contains(descriptor.Permissions, item => item.Key == "reporting.catalog.read");
        Assert.Contains(descriptor.Permissions, item => item.Key == "reporting.run.create");
        Assert.Contains(descriptor.Permissions, item => item.Key == "reporting.output.download");
        Assert.Contains(descriptor.Permissions, item =>
            item.Key == "reporting.template.publish" && item.RiskClass == ManifestRiskClass.High);
        Assert.Contains(descriptor.NavigationItems, item =>
            item.FeatureFlag == "reporting.phase1" && item.Permission == "reporting.catalog.read");
        Assert.All(descriptor.Tools, item => Assert.Equal(ToolAccessMode.ReadOnly, item.AccessMode));
        Assert.Contains(descriptor.Tools, item => item.Id == "reporting.catalog.list");
        Assert.Contains(descriptor.Tools, item => item.Id == "reporting.runs.get");
        Assert.Contains(descriptor.Tools, item => item.Id == "reporting.outputs.describe");
        Assert.Contains(descriptor.Events, item =>
            item.Name == "reporting.report.completed" && item.Version == 1);
    }

    [Fact]
    public void CanonicalJsonSortsObjectPropertiesAndProducesStableLowercaseHash()
    {
        using var first = System.Text.Json.JsonDocument.Parse("{\"z\":2,\"a\":{\"d\":4,\"b\":3}}");
        using var second = System.Text.Json.JsonDocument.Parse("{\"a\":{\"b\":3,\"d\":4},\"z\":2}");

        var firstJson = CanonicalJson.Normalize(first.RootElement);
        var secondJson = CanonicalJson.Normalize(second.RootElement);

        Assert.Equal("{\"a\":{\"b\":3,\"d\":4},\"z\":2}", firstJson);
        Assert.Equal(firstJson, secondJson);
        Assert.Equal(CanonicalJson.Sha256(firstJson), CanonicalJson.Sha256(secondJson));
        Assert.Matches("^[0-9a-f]{64}$", CanonicalJson.Sha256(firstJson));
    }

    [Fact]
    public void RunTransitionsFromQueueToImmutableSnapshotReadyStage()
    {
        var run = CreateRun();

        run.StartAttempt("{\"allowed\":true}", Cutoff.AddSeconds(1));
        run.AttachSnapshot(Guid.NewGuid(), "{\"allowed\":true}", Cutoff.AddSeconds(2));

        Assert.Equal(ReportRunStatus.Processing, run.Status);
        Assert.Equal(ReportPipelineStage.SnapshotReady, run.PipelineStage);
        Assert.Equal(1, run.AttemptCount);
        Assert.NotNull(run.SnapshotId);
        Assert.Equal(3, run.Revision);
        Assert.Equal(
            "reporting.run.invalid_state",
            Assert.Throws<DomainRuleException>(() =>
                run.AttachSnapshot(Guid.NewGuid(), "{\"allowed\":true}", Cutoff.AddSeconds(3))).Code);
    }

    [Fact]
    public void RenderingRetryPreservesSnapshotAndRenderingCannotBeCancelled()
    {
        var run = CreateRun();
        var snapshotId = Guid.NewGuid();

        run.StartAttempt("{\"allowed\":true}", Cutoff.AddSeconds(1));
        run.AttachSnapshot(snapshotId, "{\"allowed\":true}", Cutoff.AddSeconds(2));
        run.BeginRendering(Cutoff.AddSeconds(3));

        Assert.Equal(
            "reporting.run.already_final",
            Assert.Throws<DomainRuleException>(() => run.Cancel(Cutoff.AddSeconds(4))).Code);

        run.RequeueRendering("reporting.renderer.transient", Cutoff.AddMinutes(1));
        Assert.Equal(ReportRunStatus.Processing, run.Status);
        Assert.Equal(ReportPipelineStage.SnapshotReady, run.PipelineStage);
        Assert.Equal(snapshotId, run.SnapshotId);
        Assert.Equal("reporting.renderer.transient", run.DiagnosticCode);

        run.Fail("reporting.renderer.transient", null, Cutoff.AddMinutes(2));
        run.RetryFailed(Cutoff.AddMinutes(3));

        Assert.Equal(ReportRunStatus.Processing, run.Status);
        Assert.Equal(ReportPipelineStage.SnapshotReady, run.PipelineStage);
        Assert.Equal(snapshotId, run.SnapshotId);
        Assert.Equal("reporting.retry.requested", run.DiagnosticCode);
    }

    [Fact]
    public void OfficialVersionRuleHonorsApprovalAndSupersessionCutoff()
    {
        var approvedAt = Cutoff.AddHours(-2);
        var supersededAt = Cutoff.AddHours(1);

        Assert.False(DailyReportReportingRules.IsOfficialAt(approvedAt, supersededAt, approvedAt.AddTicks(-1)));
        Assert.True(DailyReportReportingRules.IsOfficialAt(approvedAt, supersededAt, Cutoff));
        Assert.False(DailyReportReportingRules.IsOfficialAt(approvedAt, supersededAt, supersededAt));
        Assert.True(DailyReportReportingRules.IsOfficialAt(approvedAt, null, Cutoff.AddYears(1)));
    }

    [Fact]
    public void SemanticSnapshotHashIsIndependentOfBuildTimeAndPreservesLineage()
    {
        var tenantId = Guid.NewGuid();
        var project = Project(tenantId);
        var reportId = Guid.NewGuid();
        var chain = Chain(reportId);
        var parameters = new DailyReportReportParameters(reportId, true);
        var first = DailyReportSnapshotBuilder.Build(
            Guid.NewGuid(), tenantId, project, Cutoff, parameters, chain, Cutoff.AddMinutes(1));
        var second = DailyReportSnapshotBuilder.Build(
            Guid.NewGuid(), tenantId, project, Cutoff, parameters, chain, Cutoff.AddMinutes(30));

        Assert.Equal(ReportDataStatus.Available, first.DataStatus);
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(first.SourceManifestSha256, second.SourceManifestSha256);
        Assert.Contains(reportId.ToString(), first.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("copiedFromFactId", first.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingOfficialChainIsNoDataRatherThanFabricatedZero()
    {
        var tenantId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var snapshot = DailyReportSnapshotBuilder.Build(
            Guid.NewGuid(),
            tenantId,
            Project(tenantId),
            Cutoff,
            new DailyReportReportParameters(reportId, true),
            chain: null,
            builtAt: Cutoff.AddMinutes(1));

        Assert.Equal(ReportDataStatus.NoData, snapshot.DataStatus);
        Assert.DoesNotContain("quantity\":0", snapshot.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportingRoleMappingSeparatesObserverFromRunCreation()
    {
        Assert.True(ProjectPermissionService.GrantsRole("Observer", "reporting.catalog.read"));
        Assert.True(ProjectPermissionService.GrantsRole("Observer", "reporting.output.download"));
        Assert.False(ProjectPermissionService.GrantsRole("Observer", "reporting.run.create"));
        Assert.True(ProjectPermissionService.GrantsRole("SiteSupervisor", "reporting.run.create"));
        Assert.True(ProjectPermissionService.GrantsRole("TechnicalOffice", "reporting.run.create"));
        Assert.True(ProjectPermissionService.GrantsRole("ProjectController", "field.daily-reports.read"));
    }

    [Fact]
    public void OutputManifestRejectsMediaAndExtensionSubstitution()
    {
        var invalidContentType = Assert.Throws<DomainRuleException>(() => CreateOutput(
            contentType: "application/octet-stream",
            fileName: "report.pdf"));
        var invalidExtension = Assert.Throws<DomainRuleException>(() => CreateOutput(
            contentType: "application/pdf",
            fileName: "report.xlsx"));
        var output = CreateOutput(contentType: "application/pdf", fileName: "report.pdf");

        Assert.Equal("reporting.output.content_type.invalid", invalidContentType.Code);
        Assert.Equal("reporting.output.file_name.invalid", invalidExtension.Code);
        Assert.Equal(ReportOutputArchiveState.Active, output.ArchiveState);
        output.Archive(Cutoff.AddDays(1));
        Assert.Equal(ReportOutputArchiveState.Archived, output.ArchiveState);
    }

    [Fact]
    public void ArtifactIdentityAndPersianFormattingAreStableAndSpreadsheetSafe()
    {
        var runId = Guid.Parse("10000000-0000-4000-8000-000000000001");

        var firstPdf = ReportArtifactIdentity.OutputId(runId, ReportFormat.Pdf);
        var secondPdf = ReportArtifactIdentity.OutputId(runId, ReportFormat.Pdf);
        var xlsx = ReportArtifactIdentity.OutputId(runId, ReportFormat.Xlsx);

        Assert.Equal(firstPdf, secondPdf);
        Assert.NotEqual(firstPdf, xlsx);
        Assert.Equal(
            ReportArtifactIdentity.DocumentId(firstPdf),
            ReportArtifactIdentity.DocumentId(secondPdf));
        Assert.Equal("۱۴۰۳/۱۲/۳۰", PersianReportFormatting.FormatDate(new DateOnly(2025, 3, 20)));
        Assert.Equal("۱۴۰۴/۰۱/۰۱", PersianReportFormatting.FormatDate(new DateOnly(2025, 3, 21)));
        Assert.Equal("۱۴۰۴/۱۲/۲۹", PersianReportFormatting.FormatDate(new DateOnly(2026, 3, 20)));
        Assert.Equal("۱۴۰۵/۰۱/۰۱", PersianReportFormatting.FormatDate(new DateOnly(2026, 3, 21)));
        Assert.EndsWith(
            "۱۵:۳۰",
            PersianReportFormatting.FormatInstant(Cutoff, "Asia/Tehran"),
            StringComparison.Ordinal);
        Assert.Equal("'=SUM(A1:A2)", PersianReportFormatting.SafeSpreadsheetText("=SUM(A1:A2)"));
        Assert.Equal("'+1", PersianReportFormatting.SafeSpreadsheetText("+1"));
        Assert.Equal("'-1", PersianReportFormatting.SafeSpreadsheetText("-1"));
        Assert.Equal("'@cmd", PersianReportFormatting.SafeSpreadsheetText("@cmd"));
    }

    [Fact]
    public void CertifiedXlsxIsDeterministicRtlNamespaceCorrectAndFormulaFree()
    {
        var request = CreateXlsxRenderRequest("=HYPERLINK(\"https://invalid.example\",\"x\")");
        var renderer = new DailyReportXlsxRenderer(ReportingExecutionOptions.Default);

        var first = renderer.Render(request);
        var second = renderer.Render(request);

        Assert.True(first.Bytes.SequenceEqual(second.Bytes));
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal((byte)'P', first.Bytes[0]);
        Assert.Equal((byte)'K', first.Bytes[1]);

        using var stream = new MemoryStream(first.Bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.Equal(9, archive.Entries.Count);
        var metadataXml = ReadEntry(archive, "xl/worksheets/sheet1.xml");
        var dataXml = ReadEntry(archive, "xl/worksheets/sheet2.xml");
        var spreadsheet = (XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var metadata = XDocument.Parse(metadataXml);
        var data = XDocument.Parse(dataXml);

        Assert.Equal(spreadsheet + "worksheet", metadata.Root!.Name);
        Assert.Equal("1", metadata.Descendants(spreadsheet + "sheetView").Single()
            .Attribute("rightToLeft")?.Value);
        Assert.Equal("1", data.Descendants(spreadsheet + "sheetView").Single()
            .Attribute("rightToLeft")?.Value);
        Assert.Empty(metadata.Descendants(spreadsheet + "f"));
        Assert.Empty(data.Descendants(spreadsheet + "f"));
        Assert.Contains("'=HYPERLINK", dataXml, StringComparison.Ordinal);
        Assert.Contains($"/outputs/{request.OutputId}/verify", metadataXml, StringComparison.Ordinal);

        foreach (var entryName in new[] { "[Content_Types].xml", "_rels/.rels", "xl/_rels/workbook.xml.rels", "docProps/app.xml" })
        {
            var document = XDocument.Parse(ReadEntry(archive, entryName));
            Assert.All(
                document.Root!.DescendantsAndSelf(),
                element => Assert.NotEqual(XNamespace.None, element.Name.Namespace));
        }
    }

    [Fact]
    public void WorkerQualificationPauseIsDisabledByDefault()
    {
        var options = ReportingWorkerQualificationOptions.Create(
            new ConfigurationBuilder().Build());

        Assert.StartsWith("worker-", options.WorkerInstanceId, StringComparison.Ordinal);
        Assert.Equal(ReportingWorkerQualificationPausePoint.None, options.PausePoint);
        Assert.Equal(ReportingWorkerQualificationFailurePoint.None, options.FailurePoint);
        Assert.Null(options.TargetRunId);
        Assert.Null(options.PauseDuration);
    }

    [Fact]
    public void WorkerQualificationPauseRequiresTheIsolatedQaBoundary()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReportingCenter:WorkerInstanceId"] = "qa-worker-a",
                ["ReportingCenter:QualificationPausePoint"] = "BeforeStorage",
                ["ReportingCenter:QualificationTargetRunId"] = Guid.NewGuid().ToString()
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ReportingWorkerQualificationOptions.Create(configuration));

        Assert.Contains("isolated QA gateway", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkerQualificationPauseAcceptsAnExplicitQaTarget()
    {
        var targetRunId = Guid.NewGuid();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PMCS_QA_GATEWAY_ENABLED"] = "true",
                ["ReportingCenter:WorkerInstanceId"] = "qa-worker-a",
                ["ReportingCenter:QualificationPausePoint"] = "AfterStorage",
                ["ReportingCenter:QualificationTargetRunId"] = targetRunId.ToString()
            })
            .Build();

        var options = ReportingWorkerQualificationOptions.Create(configuration);

        Assert.Equal("qa-worker-a", options.WorkerInstanceId);
        Assert.Equal(ReportingWorkerQualificationPausePoint.AfterStorage, options.PausePoint);
        Assert.Equal(targetRunId, options.TargetRunId);
        Assert.Null(options.PauseDuration);
        Assert.True(options.ShouldPause(ReportingWorkerQualificationPausePoint.AfterStorage, targetRunId));
        Assert.False(options.ShouldPause(ReportingWorkerQualificationPausePoint.BeforeStorage, targetRunId));
    }

    [Fact]
    public void WorkerQualificationPauseAcceptsABoundedQaDuration()
    {
        var targetRunId = Guid.NewGuid();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PMCS_QA_GATEWAY_ENABLED"] = "true",
                ["ReportingCenter:WorkerInstanceId"] = "qa-worker-revocation",
                ["ReportingCenter:QualificationPausePoint"] = "BeforeStoragePermissionRecheck",
                ["ReportingCenter:QualificationTargetRunId"] = targetRunId.ToString(),
                ["ReportingCenter:QualificationPauseSeconds"] = "7"
            })
            .Build();

        var options = ReportingWorkerQualificationOptions.Create(configuration);

        Assert.Equal(
            ReportingWorkerQualificationPausePoint.BeforeStoragePermissionRecheck,
            options.PausePoint);
        Assert.Equal(TimeSpan.FromSeconds(7), options.PauseDuration);
        Assert.True(options.ShouldPause(
            ReportingWorkerQualificationPausePoint.BeforeStoragePermissionRecheck,
            targetRunId));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("61")]
    [InlineData("invalid")]
    public void WorkerQualificationPauseRejectsAnInvalidDuration(string pauseSeconds)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PMCS_QA_GATEWAY_ENABLED"] = "true",
                ["ReportingCenter:QualificationPausePoint"] = "BeforeStoragePermissionRecheck",
                ["ReportingCenter:QualificationTargetRunId"] = Guid.NewGuid().ToString(),
                ["ReportingCenter:QualificationPauseSeconds"] = pauseSeconds
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ReportingWorkerQualificationOptions.Create(configuration));

        Assert.Contains("between 1 and 60", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionBudgetsAreBoundedAndDefaultFailClosed()
    {
        var defaults = ReportingExecutionOptions.Create(new ConfigurationBuilder().Build());

        Assert.Equal(3, defaults.MaximumAttempts);
        Assert.Equal(2_000, defaults.MaximumPdfFacts);
        Assert.Equal(5_000, defaults.MaximumXlsxRows);
        Assert.Equal(25L * 1024L * 1024L, defaults.MaximumOutputBytes);
        Assert.Equal(TimeSpan.FromSeconds(30), defaults.RetryBaseDelay);
        Assert.Equal(TimeSpan.FromSeconds(120), defaults.ProcessingTimeout);
        Assert.Equal(TimeSpan.FromSeconds(120), defaults.QueueAgeWarning);

        var configured = ReportingExecutionOptions.Create(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReportingCenter:MaximumAttempts"] = "4",
                ["ReportingCenter:MaximumPdfFacts"] = "1800",
                ["ReportingCenter:MaximumXlsxRows"] = "4500",
                ["ReportingCenter:MaximumOutputBytes"] = "10485760",
                ["ReportingCenter:RetryBaseDelaySeconds"] = "10",
                ["ReportingCenter:ProcessingTimeoutSeconds"] = "90",
                ["ReportingCenter:QueueAgeWarningSeconds"] = "60"
            })
            .Build());

        Assert.Equal(4, configured.MaximumAttempts);
        Assert.Equal(1_800, configured.MaximumPdfFacts);
        Assert.Equal(4_500, configured.MaximumXlsxRows);
        Assert.Equal(10L * 1024L * 1024L, configured.MaximumOutputBytes);
        Assert.Equal(TimeSpan.FromSeconds(10), configured.RetryBaseDelay);
        Assert.Equal(TimeSpan.FromSeconds(90), configured.ProcessingTimeout);
        Assert.Equal(TimeSpan.FromSeconds(60), configured.QueueAgeWarning);
    }

    [Theory]
    [InlineData("ReportingCenter:MaximumAttempts", "0")]
    [InlineData("ReportingCenter:MaximumPdfFacts", "20001")]
    [InlineData("ReportingCenter:MaximumXlsxRows", "invalid")]
    [InlineData("ReportingCenter:MaximumOutputBytes", "1023")]
    [InlineData("ReportingCenter:RetryBaseDelaySeconds", "301")]
    [InlineData("ReportingCenter:ProcessingTimeoutSeconds", "901")]
    [InlineData("ReportingCenter:QueueAgeWarningSeconds", "4")]
    public void ExecutionBudgetsRejectInvalidExplicitConfiguration(string key, string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            ReportingExecutionOptions.Create(configuration));
    }

    [Fact]
    public void OrphanRemediationDefaultsToDisabledInventorySafeBoundaries()
    {
        var defaults = ReportingOrphanRemediationOptions.Create(new ConfigurationBuilder().Build());

        Assert.Equal(ReportingOrphanRemediationMode.Disabled, defaults.Mode);
        Assert.Equal(TimeSpan.FromHours(24), defaults.MinimumAge);
        Assert.Equal(TimeSpan.FromHours(1), defaults.PollingInterval);
        Assert.Equal(25, defaults.BatchSize);
        Assert.Equal(500, defaults.MaximumCandidatesPerSweep);

        var configured = ReportingOrphanRemediationOptions.Create(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReportingCenter:OrphanRemediationMode"] = "ApplyEligible",
                ["ReportingCenter:OrphanRemediationMinimumAgeHours"] = "48",
                ["ReportingCenter:OrphanRemediationPollSeconds"] = "15",
                ["ReportingCenter:OrphanRemediationBatchSize"] = "10",
                ["ReportingCenter:OrphanRemediationMaximumCandidatesPerSweep"] = "40"
            })
            .Build());

        Assert.Equal(ReportingOrphanRemediationMode.ApplyEligible, configured.Mode);
        Assert.Equal(TimeSpan.FromHours(48), configured.MinimumAge);
        Assert.Equal(TimeSpan.FromSeconds(15), configured.PollingInterval);
        Assert.Equal(10, configured.BatchSize);
        Assert.Equal(40, configured.MaximumCandidatesPerSweep);
    }

    [Theory]
    [InlineData("ReportingCenter:OrphanRemediationMode", "applyeligible")]
    [InlineData("ReportingCenter:OrphanRemediationMinimumAgeHours", "23")]
    [InlineData("ReportingCenter:OrphanRemediationPollSeconds", "4")]
    [InlineData("ReportingCenter:OrphanRemediationBatchSize", "101")]
    [InlineData("ReportingCenter:OrphanRemediationMaximumCandidatesPerSweep", "0")]
    public void OrphanRemediationRejectsUnsafeExplicitConfiguration(string key, string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            ReportingOrphanRemediationOptions.Create(configuration));
    }

    [Fact]
    public void OrphanRemediationRejectsASweepSmallerThanItsBatch()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReportingCenter:OrphanRemediationBatchSize"] = "20",
                ["ReportingCenter:OrphanRemediationMaximumCandidatesPerSweep"] = "10"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            ReportingOrphanRemediationOptions.Create(configuration));
    }

    [Fact]
    public void WorkerTransientFailureInjectionIsQaOnlyAndTargetBound()
    {
        var targetRunId = Guid.NewGuid();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PMCS_QA_GATEWAY_ENABLED"] = "true",
                ["ReportingCenter:WorkerInstanceId"] = "qa-worker-poison",
                ["ReportingCenter:QualificationFailurePoint"] = "BeforeStorageTransientFailure",
                ["ReportingCenter:QualificationTargetRunId"] = targetRunId.ToString()
            })
            .Build();

        var options = ReportingWorkerQualificationOptions.Create(configuration);

        Assert.Equal(
            ReportingWorkerQualificationFailurePoint.BeforeStorageTransientFailure,
            options.FailurePoint);
        Assert.True(options.ShouldFail(
            ReportingWorkerQualificationFailurePoint.BeforeStorageTransientFailure,
            targetRunId));
        Assert.False(options.ShouldFail(
            ReportingWorkerQualificationFailurePoint.BeforeStorageTransientFailure,
            Guid.NewGuid()));

        var unsafeConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReportingCenter:QualificationFailurePoint"] = "BeforeStorageTransientFailure",
                ["ReportingCenter:QualificationTargetRunId"] = targetRunId.ToString()
            })
            .Build();
        Assert.Throws<InvalidOperationException>(() =>
            ReportingWorkerQualificationOptions.Create(unsafeConfiguration));
    }

    [Fact]
    public void WorkerTelemetryPublishesBoundedLowCardinalityOperationalSignals()
    {
        var measurements = new List<TelemetryMeasurement>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, currentListener) =>
            {
                if (instrument.Meter.Name == ReportingWorkerTelemetry.MeterName)
                {
                    currentListener.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add(new TelemetryMeasurement(
                instrument.Name,
                value,
                tags.ToArray())));
        listener.SetMeasurementEventCallback<int>((instrument, value, tags, _) =>
            measurements.Add(new TelemetryMeasurement(
                instrument.Name,
                value,
                tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            measurements.Add(new TelemetryMeasurement(
                instrument.Name,
                value,
                tags.ToArray())));
        listener.Start();

        var telemetry = new ReportingWorkerTelemetry();
        telemetry.Heartbeat(DateTimeOffset.UtcNow);
        telemetry.ObserveQueue(3, TimeSpan.FromSeconds(17));
        telemetry.RecordClaim("snapshot", retry: false, TimeSpan.FromSeconds(9));
        telemetry.RecordFailure(
            "snapshot",
            "reporting.run.timeout",
            willRetry: true,
            TimeSpan.FromMilliseconds(25));
        telemetry.RecordClaim("rendering", retry: true, TimeSpan.FromSeconds(7));
        telemetry.RecordCompletion("rendering", TimeSpan.FromMilliseconds(40), 4_096);
        listener.RecordObservableInstruments();

        var names = measurements.Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var expected in new[]
        {
            "pmcs.reporting.worker.claims",
            "pmcs.reporting.worker.outcomes",
            "pmcs.reporting.worker.duration",
            "pmcs.reporting.worker.output.size",
            "pmcs.reporting.worker.claim.queue_age",
            "pmcs.reporting.worker.heartbeat.age",
            "pmcs.reporting.worker.active",
            "pmcs.reporting.worker.queue.depth",
            "pmcs.reporting.worker.queue.oldest_age"
        })
        {
            Assert.Contains(expected, names);
        }

        var allowedTagKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "reporting.diagnostic_code",
            "reporting.outcome",
            "reporting.retry",
            "reporting.work_kind"
        };
        Assert.All(
            measurements.SelectMany(item => item.Tags),
            tag => Assert.Contains(tag.Key, allowedTagKeys));
        Assert.DoesNotContain(
            measurements.SelectMany(item => item.Tags),
            tag => tag.Key.Contains("tenant", StringComparison.OrdinalIgnoreCase) ||
                tag.Key.Contains("project", StringComparison.OrdinalIgnoreCase) ||
                tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase) ||
                tag.Key.Contains("run_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(measurements, item =>
            item.Name == "pmcs.reporting.worker.queue.depth" && item.Value == 3);
        Assert.Contains(measurements, item =>
            item.Name == "pmcs.reporting.worker.queue.oldest_age" && item.Value == 17);
        Assert.Contains(measurements, item =>
            item.Name == "pmcs.reporting.worker.active" && item.Value == 0);
    }

    private static ReportRun CreateRun()
    {
        var parametersJson = CanonicalJson.Serialize(new DailyReportReportParameters(Guid.NewGuid(), true));
        return ReportRun.Queue(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "daily-report-certified",
            Guid.NewGuid(),
            "1.0.0",
            parametersJson,
            CanonicalJson.Sha256(parametersJson),
            CanonicalJson.Serialize(new[] { ReportFormat.Pdf, ReportFormat.Xlsx }),
            Cutoff,
            "Asia/Tehran",
            Guid.NewGuid(),
            "{\"allowed\":true}",
            "reporting-test",
            new string('a', 64),
            Cutoff);
    }

    private static ProjectControlProfile Project(Guid tenantId) => new(
        Guid.NewGuid(),
        tenantId,
        "PRJ-001",
        "پروژه آزمون",
        "Asia/Tehran",
        "IRR",
        7,
        Cutoff.AddDays(-1),
        ProjectStatus.Active,
        ContractModel.NotConfigured,
        PlanningMode.None,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured,
        ProjectFeatureState.NotConfigured,
        new ProjectCalendarProfile(ProjectCalendarConfigurationState.NotConfigured, null));

    private static ReportOutput CreateOutput(string contentType, string fileName) => ReportOutput.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        ReportFormat.Pdf,
        contentType,
        fileName,
        Guid.NewGuid(),
        1_024,
        new string('a', 64),
        "RPT-TEST-001",
        new string('b', 64),
        ReportClassification.Internal,
        "LongTerm",
        Cutoff);

    private static ReportRenderRequest CreateXlsxRenderRequest(string description)
    {
        var runId = Guid.Parse("10000000-0000-4000-8000-000000000001");
        var outputId = ReportArtifactIdentity.OutputId(runId, ReportFormat.Xlsx);
        var reportId = Guid.Parse("70000000-0000-4000-8000-000000000001");
        var chain = Chain(reportId, description);
        var project = new ReportProjectRenderIdentity(
            Guid.Parse("30000000-0000-4000-8000-000000000001"),
            "PRJ-001",
            "پروژه آزمون",
            "Asia/Tehran",
            7);
        var snapshot = new DailyReportRenderSnapshot(
            DailyReportSnapshotBuilder.SnapshotSchemaVersion,
            "daily-report-certified",
            "1.0.0",
            ReportDataStatus.Available,
            project,
            Cutoff,
            new DailyReportReportParameters(reportId, true),
            chain.RootReportId,
            chain.CurrentOfficialReportId,
            new string('f', 64),
            chain.Versions);
        return new ReportRenderRequest(
            runId,
            outputId,
            Guid.Parse("10000000-0000-4000-8000-000000000002"),
            Guid.Parse("10000000-0000-4000-8000-000000000003"),
            "daily-report-certified",
            "1.0.0",
            new string('c', 64),
            "daily-report-renderer/v1",
            "daily-report-layout/v1",
            ReportFormat.Xlsx,
            "daily-report-PRJ-001-1405-06-27-r1.xlsx",
            "RPT-1111-2222-3333-4444-5555",
            new string('d', 64),
            new string('e', 64),
            new string('f', 64),
            Cutoff,
            snapshot);
    }

    private static string ReadEntry(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name)
            ?? throw new InvalidOperationException($"Workbook entry '{name}' is missing.");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static DailyReportReportingChain Chain(Guid reportId, string description = "اجرای بتن‌ریزی")
    {
        var factId = Guid.NewGuid();
        var fact = new DailyReportReportingFact(
            factId,
            Guid.NewGuid(),
            DailyReportReportingFactKind.WorkProgress,
            description,
            "سازه",
            Guid.NewGuid(),
            "زون A",
            12.5m,
            "m3",
            null,
            null,
            DailyReportReportingImpactLevel.Medium,
            "OBS-1",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Cutoff.AddHours(-3));
        var version = new DailyReportReportingVersion(
            reportId,
            reportId,
            1,
            null,
            null,
            null,
            new DateOnly(2026, 9, 18),
            "زون A",
            "گزارش رسمی",
            DailyReportReportingVersionState.Approved,
            Guid.NewGuid(),
            Cutoff.AddHours(-4),
            Guid.NewGuid(),
            Cutoff.AddHours(-2),
            Cutoff.AddHours(-2),
            null,
            null,
            4,
            [fact]);
        return new DailyReportReportingChain(reportId, reportId, Cutoff, [version]);
    }

    private sealed record TelemetryMeasurement(
        string Name,
        double Value,
        KeyValuePair<string, object?>[] Tags);
}
