using System.ComponentModel;
using McpVersionVer2.Services;
using McpVersionVer2.Helpers;
using ModelContextProtocol.Server;
using static McpVersionVer2.Services.AppJsonSerializerOptions;

namespace McpVersionVer2.Tools;

[McpServerToolType]
public class VehicleHistoryTools
{
    private readonly VehicleHistoryService _historyService;
    private readonly VehicleResolverService _vehicleResolver;
    private readonly SecurityValidationService _securityService;
    private readonly IConversationContextService _contextService;
    private readonly RequestContextService _requestContext;

    public VehicleHistoryTools(
        VehicleHistoryService historyService,
        VehicleResolverService vehicleResolver,
        SecurityValidationService securityService,
        IConversationContextService contextService,
        RequestContextService requestContext)
    {
        _historyService = historyService;
        _vehicleResolver = vehicleResolver;
        _securityService = securityService;
        _contextService = contextService;
        _requestContext = requestContext;
    }

    [McpServerTool, Description("VEHICLE TRACKING: Retrieves GPS history (waypoints) for a vehicle. " +
    "QUERY MODES (Choose exactly ONE based on user intent):\n" +
    "1. 'atTime': BEST for specific timestamps (e.g., 'Where was it at 2pm?'). Automatically queries a tight 4-minute window.\n" +
    "2. 'hours': Relative duration (e.g., 'last 3 hours').\n" +
    "3. 'date': Full day history (e.g., 'history for Jan 20th'). Returns LARGE dataset.\n" +
    "4. 'startTime'/'endTime': Custom range.\n" +
    "REJECT: non-vehicle queries.")]
    public async Task<string> GetVehicleHistory(
    [Description("Bearer token for authentication")] string bearerToken,
    [Description("Vehicle identifier: plate number (e.g., '51A-123.45') OR vehicle ID")] string vehicleIdentifier,
    [Description("Target ISO timestamp (e.g., '2026-02-06T14:30:00'). Use for 'at X time' queries. Tool handles +/- 2min window.")]
    string? atTime = null,
    [Description("Look back N hours from now (1-168).")]
    int? hours = null,
    [Description("Full day date (ISO 8601 YYYY-MM-DD). Use ONLY for 'full day' requests.")]
    string? date = null,
    [Description("Start time (ISO 8601).")] string? startTime = null,
    [Description("End time (ISO 8601).")] string? endTime = null,
    [Description("Compression: 'compact' (default, saves tokens) or 'none'.")] string? compressionLevel = "compact",
    [Description("Page number (default: 1).")] int? pageNumber = null,
    [Description("Page size (default: 500).")] int? pageSize = null)
    {
        var timeContext = !string.IsNullOrEmpty(atTime) ? $"at:{atTime}" :
                          !string.IsNullOrEmpty(date) ? $"date:{date}" :
                          hours.HasValue ? $"hours:{hours}" : $"range:{startTime}-{endTime}";

        var queryContext = $"GetVehicleHistory vehicle:{vehicleIdentifier} {timeContext}";

        return await ToolExecutionHelper.ExecuteValidatedToolRequestWithContextAsync(
            _securityService,
            queryContext: queryContext,
            domain: "history",
            bearerToken: bearerToken,
            contextService: _contextService,
            requestContext: _requestContext,
            action: async (token) =>
            {
                DateTime start = DateTime.UtcNow;
                DateTime end = DateTime.UtcNow;

                var vehicleId = await _vehicleResolver.ResolveVehicleIdAsync(token, vehicleIdentifier);
                if (string.IsNullOrEmpty(vehicleId)) throw new ArgumentException($"Vehicle '{vehicleIdentifier}' not found.");

                if (!string.IsNullOrEmpty(atTime))
                {
                    if (DateTime.TryParse(atTime, out var center))
                    {
                        start = center.AddMinutes(-2);
                        end = center.AddMinutes(2);
                    }
                    else throw new ArgumentException("Invalid 'atTime' format. Use ISO 8601.");
                }
                else if (hours.HasValue)
                {
                    if (hours.Value < 1 || hours.Value > 168) throw new ArgumentException("Hours must be 1-168.");
                    end = DateTime.UtcNow;
                    start = end.AddHours(-hours.Value);
                }
                else if (!string.IsNullOrEmpty(date))
                {
                    if (DateTime.TryParse(date, out var dateVal))
                    {
                        start = dateVal.Date;
                        end = dateVal.Date.AddDays(1).AddSeconds(-1);
                    }
                    else throw new ArgumentException("Invalid 'date' format. Use YYYY-MM-DD.");
                }
                else
                {
                    if (!DateTime.TryParse(startTime, out start) || !DateTime.TryParse(endTime, out end))
                    {
                        throw new ArgumentException("Valid 'startTime' and 'endTime' required if no other mode selected.");
                    }
                }

                if (!_securityService.IsValidVehicleId(vehicleId))
                {
                    throw new ArgumentException("Access denied for this vehicle.");
                }

                var actualCompression = compressionLevel ?? "compact";
                var actualPage = pageNumber ?? 1;
                var actualSize = pageSize ?? 500;

                return await _historyService.GetCompressedVehicleHistoryAsync(
                    token,
                    vehicleId,
                    start,
                    end,
                    actualCompression,
                    actualPage,
                    actualSize,
                    enableDownsampling: true);
            },
            successResponse: (result) => System.Text.Json.JsonSerializer.Serialize(result));
    }

    [McpServerTool, Description("VEHICLE TRACKING ONLY: Get trip summary statistics (distance, speed, duration, start/end locations) for a vehicle over a time range. REJECT: non-vehicle queries.")]
    public async Task<string> GetTripSummary(
        [Description("Bearer token for authentication")] string bearerToken,
        [Description("Vehicle identifier: plate number OR vehicle ID")] string vehicleIdentifier,
        [Description("Start time in ISO 8601 format (e.g., '2026-01-07T00:00:00')")] string startTime,
        [Description("End time in ISO 8601 format (e.g., '2026-01-07T23:59:59')")] string endTime)
    {
        var queryContext = $"GetTripSummary vehicle:{vehicleIdentifier} time:{startTime}-{endTime}";

        return await ToolExecutionHelper.ExecuteValidatedToolRequestWithContextAsync(
            _securityService,
            queryContext: queryContext,
            domain: "history",
            bearerToken: bearerToken,
            contextService: _contextService,
            requestContext: _requestContext,
            action: async (token) =>
            {
                // Unified resolution: plate OR ID
                var vehicleId = await _vehicleResolver.ResolveVehicleIdAsync(token, vehicleIdentifier);

                if (!DateTime.TryParse(startTime, out var start))
                {
                    throw new ArgumentException("Invalid start time format", nameof(startTime));
                }

                if (!DateTime.TryParse(endTime, out var end))
                {
                    throw new ArgumentException("Invalid end time format", nameof(endTime));
                }

                return await _historyService.GetVehicleTripSummaryAsync(token, vehicleId, start, end);
            },
                successResponse: (result) => System.Text.Json.JsonSerializer.Serialize(result, Default));
    }
}
