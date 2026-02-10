# MCP Server for Vehicle Fleet Management

ASP.NET Core 8.0 MCP (Model Context Protocol) server providing vehicle tracking, fleet management, and real-time monitoring capabilities through both REST APIs and MCP tools.

## Features

- **JWT Authentication** - Secure token-based authentication with automatic refresh
- **Vehicle Tracking** - Real-time status, GPS history, and trip analytics
- **Conversation Context** - AI-powered conversation management with automatic summarization
- **MCP Tools** - 13+ vehicle management tools accessible via Model Context Protocol
- **Fleet Analytics** - Statistics, daily summaries, and comprehensive reporting
- **Security Guardrails** - AI-powered query validation and threat detection
- **Audit Logging** - Complete audit trail of security events
- **Rate Limiting** - Configurable rate limits per endpoint
- **Clean Architecture** - Domain-driven design with clear separation of concerns

## Quick Start

### Prerequisites

- .NET 8.0 SDK
- SQL Server 2022+
- OpenAI API key (for conversation features)

### Installation

1. Clone the repository
```bash
git clone <repository-url>
cd MCP-Server-for-Vehicle-Query-main
```

2. Configure environment variables
```bash
cp .env.example .env
# Edit .env with your actual values:
# - OPENAI_API_KEY: Your OpenAI/GitHub Models API key
# - SQL_PASSWORD: Database password (used in appsettings.json via ${SQL_PASSWORD})
```

3. Configure application settings
```bash
cp appsettings.example.json appsettings.json
# Edit appsettings.json with your configuration:
# - ApiSettings: Vehicle API endpoints and credentials
# - ConversationContext: AI conversation settings
# - ConnectionStrings: Database connection string (uses ${SQL_PASSWORD} from .env)
```

4. Run database migrations
```bash
dotnet ef database update
```

5. Run the application
```bash
dotnet run
```

The server will start at `http://localhost:8080`

## Authentication

### Login

Request:
```bash
curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "your_username",
    "password": "your_password"
  }'
```

Response:
```json
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "eyJhbGc...",
  "expiresIn": 3600
}
```

### Using Bearer Tokens

Include the access token in all subsequent requests:
```bash
curl http://localhost:8080/api/conversation/messages \
  -H "Authorization: Bearer eyJhbGc..."
```

### Token Refresh

The system automatically refreshes tokens within 5 minutes of expiration. Manual refresh:
```bash
curl -X POST http://localhost:8080/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "eyJhbGc..."
  }'
```

## Core Concepts

### Sessions

Sessions map bearer tokens to conversation contexts:
- Automatically created on first authenticated request
- Persist across server restarts (database-backed)
- Store conversation history and summaries
- Clear via `DELETE /api/session/current`

### Conversation Context

The system maintains conversation history with intelligent context management:
- **Sliding Window**: Keeps last N messages in context (configurable, default 10)
- **Token Tracking**: Monitors token usage against budget (default 8000)
- **Auto-Summarization**: Compresses old messages when threshold reached (default 20 messages)
- **Formatted Context**: Combines system prompt + summaries + recent messages

### MCP Tools

13 specialized tools for vehicle management accessible via `/sse` endpoint:
- Vehicle registry queries (by plate, ID, group)
- Real-time status monitoring
- GPS history and trip analytics
- Daily statistics and reports
- Token refresh

See [Docs/TOOLS.md](Docs/TOOLS.md) for complete catalog.

## Configuration

Key settings in `appsettings.json`:

### API Endpoints
```json
{
  "ApiSettings": {
    "VehicleStatusUrl": "https://your-api.com/status",
    "VehicleApiUrl": "https://your-api.com/vehicles",
    "WaypointApiUrl": "https://your-api.com/waypoints",
    "AuthApiUrl": "https://your-api.com/auth/login"
  }
}
```

### Conversation Context
```json
{
  "ConversationContext": {
    "WindowSize": 10,
    "MaxTokens": 8000,
    "SummaryEnabled": true,
    "SummaryThreshold": 20,
    "SummaryPreserveLastK": 10,
    "SummaryMaxTokens": 512,
    "TokenBudgetForSummary": 6000,
    "MaxSummariesPerSession": 2
  }
}
```

### Database
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=VehicleMcp;User Id=sa;Password=YourPassword;TrustServerCertificate=True"
  }
}
```

## API Overview

### Authentication (`/api/auth`)
- `POST /login` - User authentication
- `POST /refresh` - Token refresh

### Chat (`/api/chat`)
- `POST /` - Proxy chat requests to OpenAI with conversation tracking

### Conversation (`/api/conversation`)
- `GET /messages` - Get paginated conversation history
- `GET /summary` - Get latest conversation summary
- `POST /summarize` - Manually trigger summarization
- `GET /history` - Get recent messages
- `GET /context` - Get formatted context
- `DELETE /clear` - Clear all conversation history

### Session (`/api/session`)
- `GET /current` - Get or create session
- `DELETE /current` - Clear current session

### MCP Tools (`/sse`)
- Server-Sent Events endpoint for MCP tool invocations

See [Docs/API.md](Docs/API.md) for detailed endpoint documentation.

## Rate Limiting

### Tool API (`/sse`)
- **Limit**: 60 requests per minute
- **Window**: Sliding (1 minute)
- **Partition**: By bearer token
- **Response**: HTTP 429 when exceeded

### Conversation API (`/api/conversation/*`)
- **Limit**: 10 requests per minute
- **Window**: Sliding (1 minute)
- **Partition**: By bearer token
- **Response**: HTTP 429 when exceeded

## Security

### Query Validation

All MCP tool requests undergo security validation:
- SQL injection detection
- XSS pattern detection
- Prompt injection prevention
- Domain topic validation (vehicle_registry, live_status, history, auth)
- Educational/off-topic query blocking

### AI Guardrails

Optional AI-powered validation using OpenAI:
- Contextual threat detection
- Intent analysis
- Automated reasoning for edge cases

### Audit Logging

Security events logged to `./logs/audit.log`:
- Blocked queries with reasons
- User identification
- Timestamp and query preview
- Automatic log rotation (10MB max, 5 files)

## Example Usage

### Get Vehicle Status
```bash
# Via REST API (using MCP tool internally)
curl -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "messages": [
      {"role": "user", "content": "What is the status of vehicle 51A-12345?"}
    ]
  }'
```

### Get Conversation History
```bash
curl "http://localhost:8080/api/conversation/messages?page=1&pageSize=20" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

### Clear Conversation
```bash
curl -X DELETE http://localhost:8080/api/conversation/clear \
  -H "Authorization: Bearer YOUR_TOKEN"
```

## Project Structure

```
├── Controllers/           # REST API endpoints
├── Services/             # Business logic layer
│   ├── Auth/            # Authentication services
│   ├── Conversation/    # Context management
│   ├── Vehicle/         # Vehicle data services
│   └── Mappers/         # DTO mapping
├── Tools/               # MCP tool implementations (13+ tools)
├── Data/                # EF Core persistence layer
│   ├── Entities/        # Database entities (EF Core)
│   │   ├── ConversationEntryEntity.cs
│   │   ├── ConversationSummaryEntity.cs
│   │   └── SessionEntity.cs
│   ├── ConversationDbContext.cs
│   └── Migrations/      # EF migrations
├── Models/              # Organized by type and feature
│   ├── Domain/          # Domain models (business logic)
│   │   ├── Conversation/  # Conversation domain
│   │   │   ├── ConversationEntry.cs
│   │   │   └── ConversationConfig.cs
│   │   └── Vehicle/       # Vehicle domain
│   │       ├── VehicleHistoryResult.cs
│   │       ├── VehicleTripSummary.cs
│   │       └── PaginatedVehicleHistoryResult.cs
│   ├── Dto/             # Data Transfer Objects (API contracts)
│   │   ├── Auth/          # Authentication DTOs
│   │   │   ├── AuthRequest.cs (LoginRequest)
│   │   │   ├── AuthResponse.cs (LoginApiResponse, LoginResponse)
│   │   │   └── TokenResponse.cs (CachedTokenPair, TokenResponse)
│   │   ├── Vehicle/       # Vehicle DTOs
│   │   │   ├── Vehicle.cs (ApiResponse, VehicleResponse)
│   │   │   ├── VehicleDto.cs
│   │   │   ├── VehicleStatus.cs (VehicleStatusResponse, VehicleStatus)
│   │   │   ├── FleetStatisticsDto.cs
│   │   │   ├── RealTimeVehicleStatusDto.cs
│   │   │   └── ... (10 total vehicle DTOs)
│   │   └── Trip/          # Trip/Daily DTOs
│   │       ├── Trip.cs
│   │       └── Daily.cs
│   ├── Protobuf/        # Binary serialization models
│   │   ├── Waypoint.cs    # GPS data with ProtoBuf attributes
│   │   └── ValueSensor.cs
│   ├── Requests/        # API request models
│   │   └── ChatRequest.cs
│   └── ValueObjects/    # Immutable value objects
│       ├── SecurityValidationResult.cs
│       ├── WaypointSummary.cs
│       └── CompressedWaypointSummary.cs
├── Helpers/             # Utility classes
├── Security/            # Security validation
│   ├── TokenHashHelper.cs
│   └── ToolValidationException.cs
└── wwwroot/             # Frontend assets
```

## Development Commands

```bash
# Build
dotnet build

# Build in Release mode
dotnet build -c Release

# Run with auto-reload
dotnet watch run

# Run the application
dotnet run

# EF Core migrations
dotnet ef migrations add MigrationName
dotnet ef database update
dotnet ef migrations remove
```

## Troubleshooting

### Port Already in Use
```bash
# Check what's using port 8080
lsof -i :8080

# Kill the process
kill -9 <PID>
```

### Database Connection Issues
- Verify connection string in `appsettings.json`
- Ensure SQL Server is running
- Check migrations are applied: `dotnet ef database update`
- Verify SQL_PASSWORD in .env matches your database password

### Token Expiration
- Access tokens expire after configured time (default 1 hour)
- Refresh tokens used automatically within 5-minute buffer
- Manual refresh via `/api/auth/refresh` endpoint

### Rate Limiting Errors (HTTP 429)
- Wait 1 minute before retrying
- Check rate limit policies in configuration
- Reduce request frequency

### Conversation Context Too Large
- Trigger manual summarization: `POST /api/conversation/summarize`
- Clear history: `DELETE /api/conversation/clear`
- Adjust `MaxTokens` and `SummaryThreshold` in config

## Architecture

See [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md) for detailed architecture documentation including:
- Component diagrams
- Sequence diagrams for key flows
- Database schema
- Layer responsibilities

## Support

For issues and questions, please open an issue in the repository.
