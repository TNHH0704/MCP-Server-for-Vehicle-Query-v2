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

    [McpServerTool, Description("VEHICLE TRACKING: Get GPS waypoint history for a vehicle with automatic compression for LLMs. Supports multiple query modes: Time range (startTime+endTime), Last N hours (hours), By date (date). Uses 'compact' compression by default (saves 65% tokens, 4-decimal coordinates). Returns coordinates, speed, cumulative distance, vehicle state, and trip statistics. For queries >4h, automatically downsamples waypoints while preserving state transitions. Pagination available for very large results. REJECT: non-vehicle queries.")]
    public async Task<string> GetVehicleHistory(
        [Description("Bearer token for authentication")] string bearerToken,
        [Description("Vehicle identifier: plate number (e.g., '51A40391') OR vehicle ID")] string vehicleIdentifier,
        [Description("Start time in ISO 8601 format (e.g., '2026-01-07T00:00:00'). Required unless using hours or date.")] string? startTime = null,
        [Description("End time in ISO 8601 format (e.g., '2026-01-07T23:59:59'). Required unless using hours or date.")] string? endTime = null,
        [Description("Number of hours to look back (1-168). Alternative to startTime/endTime.")] int? hours = null,
        [Description("Date in 'dd-MM-yyyy' format (e.g., '07-01-2026'). Alternative to time range.")] string? date = null,
        [Description("Compression: 'compact' (default, saves 65% tokens) or 'none' (full detail). Compact uses 4-decimal coordinates and removes redundant fields.")] string? compressionLevel = "compact",
        [Description("Page number for pagination (default: 1). Use for very large results.")] int? pageNumber = null,
        [Description("Page size for pagination (default: 500). Reduce for smaller LLM context windows.")] int? pageSize = null)
    {
        string timeRange;
        if (!string.IsNullOrEmpty(date))
        {
            timeRange = $"date:{date}";
        }
        else if (hours.HasValue)
        {
            timeRange = $"hours:{hours}";
        }
        else
        {
            timeRange = $"time:{startTime ?? ""}-{endTime ?? ""}";
        }
        var queryContext = $"GetVehicleHistory vehicle:{vehicleIdentifier} {timeRange}";

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
                    
                    // Unified resolution: plate OR ID
                    var vehicleId = await _vehicleResolver.ResolveVehicleIdAsync(token, vehicleIdentifier);

                    if (!string.IsNullOrEmpty(date))
                    {
                        if (!DateTime.TryParseExact(date, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out var dateValue))
                        {
                            throw new ArgumentException("Date must be in dd-MM-yyyy format (e.g., '20-01-2026').", nameof(date));
                        }
                        start = dateValue.Date; 
                        end = dateValue.Date.AddDays(1).AddSeconds(-1); 
                    }
                    else if (hours.HasValue)
                    {
                        end = DateTime.UtcNow;
                        start = end.AddHours(-hours.Value);
                        if (hours.Value < 1 || hours.Value > 168)
                        {
                            throw new ArgumentException("Hours parameter must be between 1 and 168.", nameof(hours));
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(startTime) && !DateTime.TryParse(startTime, out start))
                        {
                            throw new ArgumentException("Invalid start time format.", nameof(startTime));
                        }

                        if (!string.IsNullOrEmpty(endTime) && !DateTime.TryParse(endTime, out end))
                        {
                            throw new ArgumentException("Invalid end time format.", nameof(endTime));
                        }
                    }

                    if (!string.IsNullOrEmpty(vehicleId) && !_securityService.IsValidVehicleId(vehicleId))
                    {
                        throw new ArgumentException("Invalid vehicle ID format.", nameof(vehicleId));
                    }

                    // Use compressed history by default for better LLM performance
                    var actualCompressionLevel = compressionLevel ?? "compact";
                    var actualPageNumber = pageNumber ?? 1;
                    var actualPageSize = pageSize ?? 500;

                    // Always use compressed and paginated endpoint for consistency
                    return await _historyService.GetCompressedVehicleHistoryAsync(
                        token, 
                        vehicleId!, 
                        start, 
                        end, 
                        actualCompressionLevel, 
                        actualPageNumber, 
                        actualPageSize, 
                        enableDownsampling: true);
                },
                successResponse: (result) => System.Text.Json.JsonSerializer.Serialize(result, Default));
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
