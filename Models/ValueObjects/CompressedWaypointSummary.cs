namespace McpVersionVer2.Models.ValueObjects;
public class CompressedWaypointSummary
{
    public string Timestamp { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Speed { get; set; }
    public double CumulativeDistanceKm { get; set; }
    public string VehicleStatus { get; set; } = ""; 
    public int? Altitude { get; set; }
    public byte? Heading { get; set; }
    public short? EventId { get; set; }
}
