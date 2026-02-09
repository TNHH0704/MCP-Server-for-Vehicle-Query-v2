namespace McpVersionVer2.Models.Requests;

public class ChatRequest
{
    public required string Model { get; set; }
    public required object Messages { get; set; } 
    
    public List<object>? Tools { get; set; } 
}