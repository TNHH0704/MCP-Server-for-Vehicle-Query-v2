using McpVersionVer2.Models.Dto;

namespace McpVersionVer2.Services;

/// <summary>
/// Unified service for resolving vehicle identifiers (plate or ID) to vehicle IDs.
/// Handles validation and provides consistent error messages.
/// </summary>
public class VehicleResolverService
{
    private readonly VehicleService _vehicleService;
    private readonly SecurityValidationService _securityService;
    private readonly ILogger<VehicleResolverService> _logger;

    public VehicleResolverService(
        VehicleService vehicleService, 
        SecurityValidationService securityService,
        ILogger<VehicleResolverService> logger)
    {
        _vehicleService = vehicleService;
        _securityService = securityService;
        _logger = logger;
    }

    /// <summary>
    /// Resolves a vehicle identifier (plate or ID) to a vehicle ID.
    /// Auto-detects whether input is a plate number or vehicle ID.
    /// Uses fuzzy matching for plates (matches both plate and displayName fields).
    /// </summary>
    /// <param name="bearerToken">Authentication token</param>
    /// <param name="identifier">Plate number or vehicle ID</param>
    /// <returns>Resolved vehicle ID</returns>
    /// <exception cref="ArgumentException">If identifier format is invalid</exception>
    /// <exception cref="InvalidOperationException">If no vehicle found</exception>
    public async Task<string> ResolveVehicleIdAsync(string bearerToken, string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Vehicle identifier cannot be empty.", nameof(identifier));
        }

        // Try as vehicle ID first (faster, more specific)
        if (_securityService.IsValidVehicleId(identifier))
        {
            _logger.LogDebug("Attempting to resolve as vehicle ID: {Identifier}", identifier);
            var vehicleById = await _vehicleService.GetVehicleByIdAsync(bearerToken, identifier);
            if (vehicleById != null)
            {
                _logger.LogDebug("Resolved vehicle ID: {VehicleId}", vehicleById.Id);
                return vehicleById.Id;
            }
        }

        // Try as plate number (with fuzzy matching)
        if (_securityService.IsValidPlateNumber(identifier))
        {
            _logger.LogDebug("Attempting to resolve as plate number: {Identifier}", identifier);
            var vehicleByPlate = await _vehicleService.GetVehicleByPlateAsync(bearerToken, identifier);
            if (vehicleByPlate != null)
            {
                _logger.LogDebug("Resolved plate '{Plate}' to vehicle ID: {VehicleId}", identifier, vehicleByPlate.Id);
                return vehicleByPlate.Id;
            }
        }

        // No match found
        throw new InvalidOperationException(
            $"No vehicle found with identifier '{identifier}'. " +
            "Please verify the plate number or vehicle ID is correct.");
    }

    /// <summary>
    /// Resolves a vehicle identifier and returns the full vehicle object.
    /// </summary>
    public async Task<VehicleResponse> ResolveVehicleAsync(string bearerToken, string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Vehicle identifier cannot be empty.", nameof(identifier));
        }

        // Try as vehicle ID first
        if (_securityService.IsValidVehicleId(identifier))
        {
            var vehicleById = await _vehicleService.GetVehicleByIdAsync(bearerToken, identifier);
            if (vehicleById != null)
            {
                return vehicleById;
            }
        }

        // Try as plate number
        if (_securityService.IsValidPlateNumber(identifier))
        {
            var vehicleByPlate = await _vehicleService.GetVehicleByPlateAsync(bearerToken, identifier);
            if (vehicleByPlate != null)
            {
                return vehicleByPlate;
            }
        }

        throw new InvalidOperationException(
            $"No vehicle found with identifier '{identifier}'. " +
            "Please verify the plate number or vehicle ID is correct.");
    }
}
