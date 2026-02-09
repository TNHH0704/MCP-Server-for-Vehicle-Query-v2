namespace McpVersionVer2.Models.Domain.Vehicle;

public class VehicleHistoryResult
{
    public string VehicleId { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int TotalWaypoints { get; set; }
    public List<WaypointSummary> Waypoints { get; set; } = new();
    public int? HoursBack { get; set; }
    public string? Date { get; set; }
    public double TotalDistanceKm { get; set; }
    public string TotalRunningTimeFormatted { get; set; } = "";
    public string TotalStopTimeFormatted { get; set; } = "";
    public int AmountOfTimeStop { get; set; }
    public double AverageSpeedKmh { get; set; }
    public double HighestSpeedKmh { get; set; }
}
