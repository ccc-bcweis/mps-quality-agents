// Copyright (c) 2026 Bryan C. Weis and Common Consulting Company

using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

using Mps.Application.Tools;
using Mps.Application.Writes;
using Mps.Domain.Capa;
using Mps.Domain.Enterprise;
using Mps.Domain.Identity;
using Mps.Domain.Incidents;

namespace Mps.Connectors;

/// <summary>
/// HTTP client for the mock enterprise API. Set <see cref="MockFault"/> to send <c>X-Mock-Fault</c> on the next write.
/// </summary>
public sealed class MockEnterpriseClient : IMesTool, IQmsQueryTool, IPlmTool, IErpTool, ICmmsTool, IQmsCapaWriter, ITicketingPort
{
    private readonly HttpClient _http;
    private readonly TimeProvider _time;

    public MockEnterpriseClient(HttpClient http, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
        _time = time ?? TimeProvider.System;
    }

    public string? MockFault { get; set; }

    public async Task<IReadOnlyList<ProcessRun>> GetRunsAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var path = Query("/mes/runs", ("toolId", toolId.Value), ("from", Stamp(from)), ("until", Stamp(until)));
        return await GetAsync<ProcessRun>(path, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LotReference>> GetLotsAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        // LotReference has no timestamp, so the mock GET cannot apply from/until. The connector returns every lot for the tool.
        _ = from;
        _ = until;
        return await GetAsync<LotReference>(Query("/mes/lots", ("toolId", toolId.Value)), cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NcrRecord>> SearchNcrsAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var rows = await GetAsync<NcrRecord>(Query("/qms/ncrs", ("toolId", toolId.Value)), cancellationToken).ConfigureAwait(false);
        return Between(rows, from, until, ncr => ncr.OpenedAt);
    }

    public async Task<IReadOnlyList<EcoRecord>> GetEcosAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var rows = await GetAsync<EcoRecord>(Query("/plm/ecos", ("toolId", toolId.Value)), cancellationToken).ConfigureAwait(false);
        return Between(rows, from, until, eco => eco.EffectiveAt);
    }

    public async Task<ConfigRevision> GetConfigAsync(ToolId toolId, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(Query("/plm/config", ("toolId", toolId.Value)), cancellationToken).ConfigureAwait(false);
        await EnsureAsync(response).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<ConfigRevision>(EnterpriseJson.Options, cancellationToken).ConfigureAwait(false)
            ?? throw new EnterpriseFaultException((int)response.StatusCode, "Config response was empty.");
    }

    public async Task<IReadOnlyList<MaterialLot>> GetMaterialLotsAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var rows = await GetAsync<MaterialLot>(Query("/erp/lots", ("toolId", toolId.Value)), cancellationToken).ConfigureAwait(false);
        return Between(rows, from, until, lot => lot.ReceivedAt);
    }

    public async Task<IReadOnlyList<SupplierNotice>> GetSupplierNoticesAsync(string? materialCode, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var rows = await GetAsync<SupplierNotice>(Query("/erp/notices", ("materialCode", materialCode)), cancellationToken).ConfigureAwait(false);
        return Between(rows, from, until, notice => notice.IssuedAt);
    }

    public async Task<IReadOnlyList<WorkOrder>> GetWorkOrdersAsync(ToolId toolId, DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var rows = await GetAsync<WorkOrder>(Query("/cmms/work-orders", ("toolId", toolId.Value)), cancellationToken).ConfigureAwait(false);
        return Between(rows, from, until, order => order.PerformedAt);
    }

    public async Task<IReadOnlyList<CalibrationRecord>> GetCalibrationsAsync(ToolId toolId, CancellationToken cancellationToken)
    {
        return await GetAsync<CalibrationRecord>(Query("/cmms/calibrations", ("toolId", toolId.Value)), cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> CreateAsync(CapaAction capa, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capa);
        var payload = new
        {
            capaId = capa.Id.Value,
            incidentId = capa.IncidentId.Value,
            approvalTokenId = capa.ApprovalTokenId.Value,
            summary = capa.Summary,
        };
        var created = await PostAsync<IdResponse>("/qms/capas", payload, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(created.ExternalId))
        {
            throw new EnterpriseFaultException(201, "CAPA response did not include an external id.");
        }

        return created.ExternalId;
    }

    public async Task<Ticket> OpenAsync(Incident incident, string title, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incident);
        var payload = new
        {
            incidentId = incident.Id.Value,
            toolId = incident.ToolId.Value,
            title,
        };
        var opened = await PostAsync<TicketResponse>("/ticketing/tickets", payload, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(opened.Number))
        {
            throw new EnterpriseFaultException(201, "Ticket response did not include a number.");
        }

        return new Ticket(TicketIdFrom(opened.Number), opened.Number, incident.Id, incident.ToolId, title, _time.GetUtcNow());
    }

    private async Task<T[]> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(path, cancellationToken).ConfigureAwait(false);
        await EnsureAsync(response).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<T[]>(EnterpriseJson.Options, cancellationToken).ConfigureAwait(false) ?? [];
    }

    private async Task<T> PostAsync<T>(string path, object payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (!string.IsNullOrWhiteSpace(MockFault))
        {
            request.Headers.TryAddWithoutValidation("X-Mock-Fault", MockFault);
        }

        request.Content = JsonContent.Create(payload, options: EnterpriseJson.Options);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureAsync(response).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<T>(EnterpriseJson.Options, cancellationToken).ConfigureAwait(false)
            ?? throw new EnterpriseFaultException((int)response.StatusCode, "Enterprise write response was empty.");
    }

    private static async Task EnsureAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        throw new EnterpriseFaultException((int)response.StatusCode, $"Enterprise call failed with {(int)response.StatusCode}. {body}");
    }

    private static T[] Between<T>(IEnumerable<T> rows, DateTimeOffset from, DateTimeOffset until, Func<T, DateTimeOffset> stamp)
    {
        return rows.Where(row => stamp(row) >= from && stamp(row) <= until).ToArray();
    }

    private static string Query(string path, params (string Name, string? Value)[] pairs)
    {
        var parts = new List<string>();
        foreach (var (name, value) in pairs)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(string.Concat(name, "=", Uri.EscapeDataString(value)));
            }
        }

        return parts.Count == 0 ? path : path + "?" + string.Join('&', parts);
    }

    private static string Stamp(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    private static TicketId TicketIdFrom(string number)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(number));
        var id = new Guid(hash.AsSpan(0, 16));
        if (id == Guid.Empty)
        {
            id = new Guid(hash.AsSpan(16, 16));
        }

        return new TicketId(id);
    }

    private sealed class IdResponse
    {
        public string? ExternalId { get; set; }
    }

    private sealed class TicketResponse
    {
        public string? Number { get; set; }
    }
}
