namespace McpVersionVer2.Models.Domain.Vehicle;

/// <summary>
/// Paginated vehicle history result for handling large datasets
/// </summary>
public class PaginatedVehicleHistoryResult
{
    public string VehicleId { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int TotalWaypoints { get; set; }
    public List<object> Waypoints { get; set; } = new();
    public double TotalDistanceKm { get; set; }
    public string TotalRunningTimeFormatted { get; set; } = "";
    public string TotalStopTimeFormatted { get; set; } = "";
    public int AmountOfTimeStop { get; set; }
    public double AverageSpeedKmh { get; set; }
    public double HighestSpeedKmh { get; set; }
    
    // Pagination
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
    
    // Compression info
    public string CompressionLevel { get; set; } = "none";
    public bool IsDownsampled { get; set; }
    public int? DownsampleIntervalSeconds { get; set; }
}
