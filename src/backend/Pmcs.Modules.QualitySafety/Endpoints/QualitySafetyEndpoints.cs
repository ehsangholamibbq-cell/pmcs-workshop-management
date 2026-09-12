using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Pmcs.Modules.QualitySafety.Endpoints;

internal static partial class QualitySafetyEndpoints
{
    public static void MapQualitySafetyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/projects/{projectId:guid}/quality-safety").WithTags("Quality and HSE");
        group.MapGet("/state", GetStateAsync);
        group.MapPut("/configuration", ConfigureAsync);
        group.MapPost("/matrices", CreateMatrixAsync);
        group.MapPost("/intakes", CaptureIntakeAsync);
        group.MapPost("/intakes/{intakeId:guid}/triage", BeginTriageAsync);
        group.MapPost("/intakes/{intakeId:guid}/resolve", ResolveIntakeAsync);
        group.MapPost("/intakes/{intakeId:guid}/convert", ConvertIntakeAsync);
        group.MapPost("/inspections", RequestInspectionAsync);
        group.MapPost("/inspections/{inspectionId:guid}/readiness", RecordReadinessAsync);
        group.MapPost("/inspections/{inspectionId:guid}/result", RecordResultAsync);
        group.MapPost("/ncrs", CreateNcrAsync);
        group.MapPost("/ncrs/{ncrId:guid}/transition", TransitionNcrAsync);
        group.MapPost("/defects", CreateDefectAsync);
        group.MapPost("/defects/{defectId:guid}/assign", AssignDefectAsync);
        group.MapPost("/defects/{defectId:guid}/transition", TransitionDefectAsync);
        group.MapPost("/incidents", ReportIncidentAsync);
        group.MapPost("/incidents/{incidentId:guid}/transition", TransitionIncidentAsync);
        group.MapPost("/corrective-actions", CreateActionAsync);
        group.MapPost("/corrective-actions/{actionId:guid}/transition", TransitionActionAsync);
        group.MapPost("/corrective-actions/{actionId:guid}/extend", ExtendActionAsync);
        group.MapPost("/permits", CreatePermitAsync);
        group.MapPost("/permits/{permitId:guid}/transition", TransitionPermitAsync);
        group.MapPost("/toolbox-talks", RecordToolboxTalkAsync);
        group.MapPost("/inspection-test-plans", CreateInspectionTestPlanAsync);
        group.MapPost("/checklist-templates", CreateChecklistTemplateAsync);
        group.MapPost("/test-records", RecordQualityTestAsync);
        group.MapPost("/competencies", RecordCompetencyAsync);
        group.MapPost("/exposure-hours", RecordExposureHoursAsync);
    }
}
