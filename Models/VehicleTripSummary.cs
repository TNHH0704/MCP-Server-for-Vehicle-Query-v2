public class VehicleTripSummary
{
    public string VehicleId { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public double TotalDistanceKm { get; set; }
    public double DurationHours { get; set; }
    public double AverageSpeedKmh { get; set; }
    public double MaxSpeedKmh { get; set; }
    public int StopCount { get; set; }
    public double AmountOfTimeStop { get; set; } 
    public double AmountOfTimeRunning { get; set; } 
    public string StartInfo { get; set; } = "";
    public string EndInfo { get; set; } = "";
}