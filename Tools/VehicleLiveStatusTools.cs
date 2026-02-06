using System.ComponentModel;
using McpVersionVer2.Models.Dto;
using McpVersionVer2.Services;
using McpVersionVer2.Services.Mappers;
using McpVersionVer2.Helpers;
using ModelContextProtocol.Server;
using static McpVersionVer2.Services.AppJsonSerializerOptions;

namespace McpVersionVer2.Tools;

[McpServerToolType]
public class VehicleLiveStatusTools
{
    private const double DISTANCE_DIVISOR = 1000.0;
    private const double SPEED_DIVISOR = 100.0;

    private readonly VehicleStatusService _statusService;
    private readonly VehicleResolverService _vehicleResolver;
    private readonly VehicleStatusMapperService _mapper;
    private readonly SecurityValidationService _securityService;
    private readonly IConversationContextService _contextService;
    private readonly RequestContextService _requestContext;

    public VehicleLiveStatusTools(
        VehicleStatusService statusService,
        VehicleResolverService vehicleResolver,
        VehicleStatusMapperService mapper, 
        SecurityValidationService securityService,
        IConversationContextService contextService,
        RequestContextService requestContext)
    {
        _statusService = statusService;
        _vehicleResolver = vehicleResolver;
        _mapper = mapper;
        _securityService = securityService;
        _contextService = contextService;
        _requestContext = requestContext;
    }

    private static string FormatRunTime(int totalSeconds)
    {
        var timeSpan = TimeSpan.FromSeconds(totalSeconds);
        return timeSpan.Hours > 0
            ? timeSpan.ToString(@"hh\:mm\:ss")
            : timeSpan.ToString(@"mm\:ss");
    }

    [McpServerTool, Description("VEHICLE LIVE STATUS: Get real-time vehicle status. Supports: all vehicles, by plate, by ID, by group, by type, or filtered by status (all, moving, stopped, idle, overspeeding). Returns speed, location, heading, and status info. REJECT: non-vehicle queries.")]
    public async Task<string> GetVehicleLiveStatus(
        [Description("Bearer token")] string bearerToken,
        [Description("Filter by plate number. Optional.")] string? plate = null,
        [Description("Filter by vehicle ID. Optional.")] string? id = null,
        [Description("Filter by group name. Optional.")] string? group = null,
        [Description("Filter by vehicle type (e.g., 'Xe máy'). Optional.")] string? type = null,
        [Description("Filter by status: 'all', 'moving', 'stopped', 'idle', 'overspeeding'. Default: 'all'.")] string? status = null)
    {
        // Always include at least one allowed topic for security validation
        var queryContext = $"GetVehicleLiveStatus status plate:{plate ?? ""} id:{id ?? ""} group:{group ?? ""} type:{type ?? ""} status:{status ?? ""}";

        return await ToolExecutionHelper.ExecuteValidatedToolRequestWithContextAsync(
            securityService: _securityService,
            queryContext: queryContext,
            domain: "live_status",
            bearerToken: bearerToken,
            contextService: _contextService,
            requestContext: _requestContext,
            action: async (token) => 
            {
                // Use VehicleResolverService when a specific vehicle identifier is provided (without group/type filters)
                if ((!string.IsNullOrWhiteSpace(plate) || !string.IsNullOrWhiteSpace(id)) && 
                    string.IsNullOrWhiteSpace(group) && string.IsNullOrWhiteSpace(type))
                {
                    var identifier = plate ?? id!;
                    var resolvedVehicleId = await _vehicleResolver.ResolveVehicleIdAsync(token, identifier);
                    var vehicles = await _statusService.GetVehiclesWithFilterAsync(token, null, resolvedVehicleId, null, null);
                    return _statusService.FilterByStatus(vehicles, status);
                }
                
                // For group/type filters or all vehicles, use the original service
                var allVehicles = await _statusService.GetVehiclesWithFilterAsync(token, plate, id, group, type);
                return _statusService.FilterByStatus(allVehicles, status);
            },
            successResponse: (vehicles) =>
            {
                vehicles.RequireNonEmptyResult("vehicles", "No vehicles found matching the specified criteria.");
                var summaries = _mapper.MapToSummaries(vehicles);
                return System.Text.Json.JsonSerializer.Serialize(summaries, Default);
            });
    }

    [McpServerTool, Description("VEHICLE DAILY STATS: Get daily statistics (mileage, runtime, max speed, overspeed count, engine off count). Returns GPS mileage, run time, max speed, over-speed events, and stop counts. Optional: filter by plate. REJECT: non-vehicle queries.")]
    public async Task<string> GetDailyStatistics(
        [Description("Bearer token")] string bearerToken,
        [Description("Filter by plate number. Optional.")] string? plate = null)
    {
        var queryContext = $"GetDailyStatistics daily statistics mileage runtime plate:{plate ?? ""}";

        return await ToolExecutionHelper.ExecuteValidatedToolRequestWithContextAsync(
            securityService: _securityService,
            queryContext: queryContext,
            domain: "live_status",
            bearerToken: bearerToken,
            contextService: _contextService,
            requestContext: _requestContext,
            action: async (token) => 
            {
                // Use VehicleResolverService when a specific plate is provided
                if (!string.IsNullOrWhiteSpace(plate))
                {
                    var resolvedVehicleId = await _vehicleResolver.ResolveVehicleIdAsync(token, plate);
                    var vehicles = await _statusService.GetVehiclesWithFilterAsync(token, null, resolvedVehicleId, null, null);
                    vehicles.RequireNonEmptyResult("vehicle statuses", "No vehicles found.");
                    return vehicles;
                }
                
                // Get all vehicles if no plate specified
                var allVehicles = await _statusService.GetVehiclesWithFilterAsync(token, null, null, null, null);
                allVehicles.RequireNonEmptyResult("vehicle statuses", "No vehicles found.");
                return allVehicles;
            },
            successResponse: (vehicles) =>
            {
                var dailyStats = string.IsNullOrEmpty(plate)
                    ? _mapper.MapToDailyStatsSummaries(vehicles)
                    : new List<DailyStatisticsSummaryDto> { _mapper.MapToDailyStatsSummary(vehicles.First()) };
                return System.Text.Json.JsonSerializer.Serialize(dailyStats, Default);
            });
    }

    [McpServerTool, Description("VEHICLE DAILY STATUS: Get daily status summary (mileage, runtime, max speed, over-speed count, engine off count, vehicle stop count). Optional: filter by plate. REJECT: non-vehicle queries.")]
    public async Task<string> GetVehicleDailyStatus(
        [Description("Bearer token")] string bearerToken,
        [Description("Filter by plate number. Optional - returns all if not specified.")] string? plate = null)
    {
        var queryContext = $"GetVehicleDailyStatus daily status mileage runtime plate:{plate ?? ""}";

        return await ToolExecutionHelper.ExecuteValidatedToolRequestWithContextAsync(
            securityService: _securityService,
            queryContext: queryContext,
            domain: "live_status",
            bearerToken: bearerToken,
            contextService: _contextService,
            requestContext: _requestContext,
            action: async (token) => 
            {
                // Use VehicleResolverService when a specific plate is provided
                if (!string.IsNullOrWhiteSpace(plate))
                {
                    var resolvedVehicleId = await _vehicleResolver.ResolveVehicleIdAsync(token, plate);
                    var vehicles = await _statusService.GetVehiclesWithFilterAsync(token, null, resolvedVehicleId, null, null);
                    vehicles.RequireNonEmptyResult("vehicle statuses", "No vehicles found.");
                    return vehicles;
                }
                
                // Get all vehicles if no plate specified
                var allVehicles = await _statusService.GetVehiclesWithFilterAsync(token, null, null, null, null);
                allVehicles.RequireNonEmptyResult("vehicle statuses", "No vehicles found.");
                return allVehicles;
            },
            successResponse: (vehicles) =>
            {
                var dailyStatus = vehicles.Select(v => new
                {
                    plate = v.Plate,
                    displayName = v.CustomPlateNumber,
                    gpsMileage = $"{(v.Daily?.GpsMileage ?? 0) / DISTANCE_DIVISOR:F2} km",
                    runTime = FormatRunTime(v.Daily?.RunTime ?? 0),
                    maxSpeed = $"{(v.Daily?.MaxSpeed ?? 0) / SPEED_DIVISOR:F1} km/h",
                    overSpeedCount = v.Daily?.OverSpeed ?? 0,
                    engineOffCount = v.Daily?.StopCount ?? 0,
                    vehicleStopCount = v.Daily?.IdleCount ?? 0
                }).ToList();
                return System.Text.Json.JsonSerializer.Serialize(dailyStatus, Default);
            });
    }
}
