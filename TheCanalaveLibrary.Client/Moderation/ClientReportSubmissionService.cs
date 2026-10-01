using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IReportSubmissionService"/> — the member-facing half of the Moderation cluster
/// that <c>ReportDialog</c> injects (owner ruling D9's split, WU-ModerationIntegrity 2026-09-30). HttpClient
/// wrapper over <c>GET api/moderation/report-reasons</c> and <c>POST api/moderation/reports</c>; same DTOs
/// and contracts as the server class, only the transport differs (the Layer-5 body-swap).
/// <para>Status mapping via <see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/>: 400 →
/// <see cref="ModerationValidationException"/> (the allow-set, a duplicate open report — the message
/// reaches the reporter verbatim); 404 → the target is gone or hidden; 401 →
/// <see cref="SessionExpiredException"/>; and 429 → <see cref="WriteRateLimitExceededException"/>,
/// because submission sits behind the <see cref="WriteActionKind.Report"/> throttle — without the
/// <c>rateLimitedAction</c> argument a 429 would fall through to <c>ServerFaultException</c>.</para>
/// </summary>
public sealed class ClientReportSubmissionService(HttpClient http) : IReportSubmissionService
{
    public async Task<ReportReasonDto[]> GetReportReasonsAsync() =>
        await http.GetFromJsonAsync<ReportReasonDto[]>("api/moderation/report-reasons") ?? [];

    public async Task SubmitReportAsync(SubmitReportRequest request)
    {
        HttpResponseMessage response = await http.PostAsJsonAsync("api/moderation/reports", request);
        await ClientHttpHelpers.ThrowIfWriteFailedAsync(response,
            detail => new ModerationValidationException([detail]),
            rateLimitedAction: WriteActionKind.Report);
    }
}
