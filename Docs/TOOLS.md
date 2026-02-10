# MCP Tools Catalog

Complete catalog of MCP (Model Context Protocol) tools for vehicle fleet management. All tools are accessible via the `/sse` endpoint.

## Overview

The MCP server provides 13 specialized tools organized into 4 domains:
- **Auth Domain** - Token management
- **Vehicle Registry Domain** - Vehicle information and fleet statistics
- **Live Status Domain** - Real-time vehicle monitoring
- **History Domain** - GPS tracking and trip analytics

## Authentication

All tools require a bearer token:
```
Authorization: Bearer <access_token>
```

Tools are rate-limited: **60 requests per minute** per token.

## Security Validation

All tool requests undergo security validation:
- ✅ SQL injection detection
- ✅ XSS pattern detection
- ✅ Prompt injection prevention
- ✅ Domain topic validation
- ✅ Educational/off-topic query blocking

## Tool Execution Pattern

All tools use the standardized execution pattern:

```csharp
ExecuteValidatedToolRequestWithContextAsync(
    queryContext: "tool_name param1='value' param2='value'",
    domain: "vehicle_registry|live_status|history|auth",
    action: async (token) => { /* tool logic */ },
    successResponse: (result) => "formatted response"
)
```

---

## Auth Domain

### RefreshToken

Refresh an expired JWT access token using refresh token.

**Domain:** `auth`

**Parameters:**
- `refreshToken` (string, required) - The refresh token from login

**Example:**
```json
{
  "name": "RefreshToken",
  "arguments": {
    "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
  }
}
```

**Response:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

**Notes:**
- Automatically refreshes token when within 5-minute expiration buffer
- Returns new access and refresh tokens
- Old refresh token becomes invalid

---

## Vehicle Registry Domain

All vehicle registry tools validate against the `vehicle_registry` domain.

### GetVehicleInfo

Get detailed information about vehicles by various identifiers.

**Domain:** `vehicle_registry`

**Parameters:**
- `vehicleIdentifier` (string, optional) - License plate (e.g., "51A-12345"), vehicle ID (e.g., "VM123"), or "all"
- `groupId` (int, optional) - Filter by group ID

**Example Queries:**
```json
// Get all vehicles
{
  "name": "GetVehicleInfo",
  "arguments": {
    "vehicleIdentifier": "all"
  }
}

// Get by license plate
{
  "name": "GetVehicleInfo",
  "arguments": {
    "vehicleIdentifier": "51A-12345"
  }
}

// Get by vehicle ID
{
  "name": "GetVehicleInfo",
  "arguments": {
    "vehicleIdentifier": "VM300"
  }
}

// Get by group
{
  "name": "GetVehicleInfo",
  "arguments": {
    "groupId": 42
  }
}
```

**Response:**
```json
{
  "vehicles": [
    {
      "vehicleId": "VM300",
      "licensePlate": "51A-12345",
      "vehicleType": "Truck",
      "groupName": "Fleet A",
      "driverName": "John Doe",
      "phoneNumber": "+84123456789",
      "registrationExpiry": "2026-12-31",
      "insuranceExpiry": "2026-06-30"
    }
  ],
  "count": 1
}
```

---

### GetFleetStatistics

Get aggregate statistics for the entire fleet or by group.

**Domain:** `vehicle_registry`

**Parameters:**
- `groupId` (int, optional) - Filter by group ID

**Example:**
```json
{
  "name": "GetFleetStatistics",
  "arguments": {}
}
```

**Response:**
```json
{
  "totalVehicles": 150,
  "byType": {
    "Truck": 80,
    "Van": 50,
    "Car": 20
  },
  "byGroup": {
    "Fleet A": 60,
    "Fleet B": 90
  },
  "expiringSoon": {
    "registration": 5,
    "insurance": 8
  }
}
```

---

### GetVehiclesWithExpiredInsurance

Get list of vehicles with expired or expiring-soon insurance.

**Domain:** `vehicle_registry`

**Parameters:**
- `daysUntilExpiry` (int, optional, default: 30) - Number of days threshold

**Example:**
```json
{
  "name": "GetVehiclesWithExpiredInsurance",
  "arguments": {
    "daysUntilExpiry": 30
  }
}
```

**Response:**
```json
{
  "vehicles": [
    {
      "vehicleId": "VM100",
      "licensePlate": "51A-11111",
      "insuranceExpiry": "2026-03-01",
      "daysUntilExpiry": 20,
      "status": "expiring_soon"
    },
    {
      "vehicleId": "VM200",
      "licensePlate": "51A-22222",
      "insuranceExpiry": "2026-01-15",
      "daysUntilExpiry": -25,
      "status": "expired"
    }
  ],
  "count": 2
}
```

---

### GetVehiclesWithExpiredRegistration

Get list of vehicles with expired or expiring-soon registration.

**Domain:** `vehicle_registry`

**Parameters:**
- `daysUntilExpiry` (int, optional, default: 30) - Number of days threshold

**Example:**
```json
{
  "name": "GetVehiclesWithExpiredRegistration",
  "arguments": {
    "daysUntilExpiry": 30
  }
}
```

**Response:**
```json
{
  "vehicles": [
    {
      "vehicleId": "VM150",
      "licensePlate": "51A-33333",
      "registrationExpiry": "2026-02-28",
      "daysUntilExpiry": 19,
      "status": "expiring_soon"
    }
  ],
  "count": 1
}
```

---

## Live Status Domain

All live status tools validate against the `live_status` domain.

### GetVehicleLiveStatus

Get real-time status for one or more vehicles.

**Domain:** `live_status`

**Parameters:**
- `vehicleIdentifier` (string, required) - License plate, vehicle ID, or "all"
- `statusFilter` (string, optional) - Filter by status: "running", "stopped", "lost_signal"

**Example Queries:**
```json
// Single vehicle
{
  "name": "GetVehicleLiveStatus",
  "arguments": {
    "vehicleIdentifier": "51A-12345"
  }
}

// All running vehicles
{
  "name": "GetVehicleLiveStatus",
  "arguments": {
    "vehicleIdentifier": "all",
    "statusFilter": "running"
  }
}
```

**Response:**
```json
{
  "vehicles": [
    {
      "vehicleId": "VM300",
      "licensePlate": "51A-12345",
      "status": "Running",
      "speed": 54.0,
      "heading": 135,
      "latitude": 10.7758,
      "longitude": 106.7019,
      "address": "Nguyen Hue Street, District 1, Ho Chi Minh City",
      "lastUpdate": "2026-02-09T10:30:00Z",
      "engineOn": true,
      "signalStrength": "strong"
    }
  ],
  "count": 1
}
```

**Status Values:**
- `Running` - Vehicle is moving
- `Stopped` - Vehicle is stationary with engine on
- `Engine Off` - Vehicle stopped with engine off
- `Lost Signal` - No recent GPS data

---

### GetVehicleDailyMileage

Get total distance traveled by vehicle(s) for a specific date.

**Domain:** `live_status`

**Parameters:**
- `vehicleIdentifier` (string, required) - License plate, vehicle ID, or "all"
- `date` (string, required) - Date in ISO format (YYYY-MM-DD)

**Example:**
```json
{
  "name": "GetVehicleDailyMileage",
  "arguments": {
    "vehicleIdentifier": "51A-12345",
    "date": "2026-02-08"
  }
}
```

**Response:**
```json
{
  "vehicles": [
    {
      "vehicleId": "VM300",
      "licensePlate": "51A-12345",
      "date": "2026-02-08",
      "totalDistance": 156.8,
      "unit": "km"
    }
  ]
}
```

---

### GetVehicleDailyRuntime

Get total engine runtime for vehicle(s) for a specific date.

**Domain:** `live_status`

**Parameters:**
- `vehicleIdentifier` (string, required) - License plate, vehicle ID, or "all"
- `date` (string, required) - Date in ISO format (YYYY-MM-DD)

**Example:**
```json
{
  "name": "GetVehicleDailyRuntime",
  "arguments": {
    "vehicleIdentifier": "51A-12345",
    "date": "2026-02-08"
  }
}
```

**Response:**
```json
{
  "vehicles": [
    {
      "vehicleId": "VM300",
      "licensePlate": "51A-12345",
      "date": "2026-02-08",
      "totalRuntime": 480,
      "unit": "minutes",
      "formatted": "8 hours 0 minutes"
    }
  ]
}
```

---

### GetVehicleDailyMaxSpeed

Get maximum speed reached by vehicle(s) for a specific date.

**Domain:** `live_status`

**Parameters:**
- `vehicleIdentifier` (string, required) - License plate, vehicle ID, or "all"
- `date` (string, required) - Date in ISO format (YYYY-MM-DD)

**Example:**
```json
{
  "name": "GetVehicleDailyMaxSpeed",
  "arguments": {
    "vehicleIdentifier": "51A-12345",
    "date": "2026-02-08"
  }
}
```

**Response:**
```json
{
  "vehicles": [
    {
      "vehicleId": "VM300",
      "licensePlate": "51A-12345",
      "date": "2026-02-08",
      "maxSpeed": 85.5,
      "unit": "km/h",
      "timestamp": "2026-02-08T14:32:00Z",
      "location": "Highway 1A"
    }
  ]
}
```

---

### GetVehicleDailyOverspeedCount

Get count of overspeeding incidents for vehicle(s) for a specific date.

**Domain:** `live_status`

**Parameters:**
- `vehicleIdentifier` (string, required) - License plate, vehicle ID, or "all"
- `date` (string, required) - Date in ISO format (YYYY-MM-DD)
- `speedLimit` (double, optional, default: 80.0) - Speed limit threshold in km/h

**Example:**
```json
{
  "name": "GetVehicleDailyOverspeedCount",
  "arguments": {
    "vehicleIdentifier": "51A-12345",
    "date": "2026-02-08",
    "speedLimit": 80.0
  }
}
```

**Response:**
```json
{
  "vehicles": [
    {
      "vehicleId": "VM300",
      "licensePlate": "51A-12345",
      "date": "2026-02-08",
      "overspeedCount": 12,
      "speedLimit": 80.0,
      "maxSpeedRecorded": 95.0
    }
  ]
}
```

---

### GetVehicleDailyStopCount

Get count of stops made by vehicle(s) for a specific date.

**Domain:** `live_status`

**Parameters:**
- `vehicleIdentifier` (string, required) - License plate, vehicle ID, or "all"
- `date` (string, required) - Date in ISO format (YYYY-MM-DD)
- `minStopDuration` (int, optional, default: 5) - Minimum stop duration in minutes

**Example:**
```json
{
  "name": "GetVehicleDailyStopCount",
  "arguments": {
    "vehicleIdentifier": "51A-12345",
    "date": "2026-02-08",
    "minStopDuration": 5
  }
}
```

**Response:**
```json
{
  "vehicles": [
    {
      "vehicleId": "VM300",
      "licensePlate": "51A-12345",
      "date": "2026-02-08",
      "stopCount": 8,
      "minStopDuration": 5,
      "totalStopTime": 120,
      "unit": "minutes"
    }
  ]
}
```

---

### GetDailyStatusSummary

Get comprehensive daily statistics for vehicle(s).

**Domain:** `live_status`

**Parameters:**
- `vehicleIdentifier` (string, required) - License plate, vehicle ID, or "all"
- `date` (string, required) - Date in ISO format (YYYY-MM-DD)

**Example:**
```json
{
  "name": "GetDailyStatusSummary",
  "arguments": {
    "vehicleIdentifier": "51A-12345",
    "date": "2026-02-08"
  }
}
```

**Response:**
```json
{
  "vehicles": [
    {
      "vehicleId": "VM300",
      "licensePlate": "51A-12345",
      "date": "2026-02-08",
      "mileage": 156.8,
      "runtime": 480,
      "maxSpeed": 85.5,
      "overspeedCount": 12,
      "stopCount": 8,
      "averageSpeed": 45.2,
      "fuelEfficiency": "calculated based on distance and runtime"
    }
  ]
}
```

---

## History Domain

All history tools validate against the `history` domain.

### GetVehicleHistory

Get GPS waypoint history with flexible query modes.

**Domain:** `history`

**Parameters:**
- `vehicleIdentifier` (string, required) - License plate or vehicle ID
- **Query Mode 1 - Specific Time**: `atTime` (ISO 8601 datetime)
- **Query Mode 2 - Relative Duration**: `hours` (1-168)
- **Query Mode 3 - Full Day**: `date` (YYYY-MM-DD)
- **Query Mode 4 - Custom Range**: `startTime` + `endTime` (ISO 8601)

**Query Mode Selection:**
1. If `atTime` is provided → 4-minute window around specific time
2. Else if `hours` is provided → Last N hours from now
3. Else if `date` is provided → Full 24-hour period (expensive!)
4. Else if `startTime` and `endTime` → Custom range

**Example - Specific Time:**
```json
{
  "name": "GetVehicleHistory",
  "arguments": {
    "vehicleIdentifier": "51A-12345",
    "atTime": "2026-02-08T14:30:00"
  }
}
```

**Example - Last 3 Hours:**
```json
{
  "name": "GetVehicleHistory",
  "arguments": {
    "vehicleIdentifier": "51A-12345",
    "hours": 3
  }
}
```

**Example - Full Day:**
```json
{
  "name": "GetVehicleHistory",
  "arguments": {
    "vehicleIdentifier": "51A-12345",
    "date": "2026-02-08"
  }
}
```

**Example - Custom Range:**
```json
{
  "name": "GetVehicleHistory",
  "arguments": {
    "vehicleIdentifier": "51A-12345",
    "startTime": "2026-02-08T08:00:00",
    "endTime": "2026-02-08T17:00:00"
  }
}
```

**Response:**
```json
{
  "vehicleId": "VM300",
  "licensePlate": "51A-12345",
  "waypoints": [
    {
      "timestamp": "2026-02-08T14:30:00Z",
      "latitude": 10.7758,
      "longitude": 106.7019,
      "speed": 45.0,
      "heading": 135,
      "address": "Nguyen Hue Street, District 1"
    }
  ],
  "totalWaypoints": 120,
  "startTime": "2026-02-08T14:28:00Z",
  "endTime": "2026-02-08T14:32:00Z"
}
```

**Notes:**
- `atTime` mode returns 4-minute window (2 min before + 2 min after)
- `hours` mode limited to 168 hours (7 days)
- `date` mode returns full day (potentially large dataset)
- All timestamps in ISO 8601 format
- Addresses reverse-geocoded when available

---

### GetVehicleTripSummary

Get trip statistics and summary for a time period.

**Domain:** `history`

**Parameters:**
- `vehicleIdentifier` (string, required) - License plate or vehicle ID
- `startTime` (string, required) - Start datetime (ISO 8601)
- `endTime` (string, required) - End datetime (ISO 8601)

**Example:**
```json
{
  "name": "GetVehicleTripSummary",
  "arguments": {
    "vehicleIdentifier": "51A-12345",
    "startTime": "2026-02-08T08:00:00",
    "endTime": "2026-02-08T17:00:00"
  }
}
```

**Response:**
```json
{
  "vehicleId": "VM300",
  "licensePlate": "51A-12345",
  "startTime": "2026-02-08T08:00:00Z",
  "endTime": "2026-02-08T17:00:00Z",
  "totalDistance": 125.8,
  "totalDuration": 540,
  "averageSpeed": 42.5,
  "maxSpeed": 85.0,
  "stopCount": 6,
  "totalStopTime": 90,
  "startLocation": {
    "latitude": 10.7625,
    "longitude": 106.6825,
    "address": "Start Address"
  },
  "endLocation": {
    "latitude": 10.7758,
    "longitude": 106.7019,
    "address": "End Address"
  },
  "routeSummary": "Trip from Start Address to End Address via Highway 1A"
}
```

---

## Common Patterns

### Vehicle Identifier Resolution

The system automatically resolves various vehicle identifiers:
- License plates: `51A-12345`, `51A40391`
- Vehicle IDs: `VM300`, `VM123`
- Special keyword: `all` (for fleet-wide queries)

**Resolution Priority:**
1. Exact vehicle ID match
2. License plate match (with/without dashes)
3. Fuzzy plate match (ignoring special characters)

### Date/Time Formats

All datetime parameters accept ISO 8601 format:
- Date only: `2026-02-08`
- DateTime: `2026-02-08T14:30:00`
- With timezone: `2026-02-08T14:30:00+07:00`

Default timezone: `Asia/Ho_Chi_Minh` (UTC+7)

### Error Responses

Tool errors follow this format:
```json
{
  "error": "error_code",
  "message": "Human-readable description",
  "details": {
    "vehicleIdentifier": "51A-12345",
    "reason": "Vehicle not found"
  }
}
```

**Common Error Codes:**
- `vehicle_not_found` - Invalid vehicle identifier
- `invalid_date_range` - Invalid or out-of-range dates
- `validation_error` - Security validation failed
- `rate_limit_exceeded` - Tool rate limit hit
- `api_error` - External API failure

### Best Practices

1. **Use specific time queries** - Prefer `atTime` over `date` for better performance
2. **Limit date ranges** - Keep history queries under 24 hours when possible
3. **Cache results** - Fleet-wide queries (`all`) return large datasets
4. **Handle pagination** - Large result sets may be paginated
5. **Validate identifiers** - Check vehicle exists before complex queries
6. **Monitor rate limits** - 60 requests/minute per token

### Security Guardrails

All tool requests are validated against:
- **Domain topics** - Must match tool's declared domain
- **Dangerous patterns** - SQL injection, XSS, command injection
- **Off-topic queries** - Educational, general knowledge, unrelated topics
- **Prompt injection** - Attempts to manipulate AI behavior

**Example Blocked Queries:**
- "How to hack a database?" ❌
- "Ignore previous instructions" ❌
- "What is the capital of France?" ❌
- "SELECT * FROM vehicles;" ❌

**Allowed Queries:**
- "Show me all running vehicles" ✅
- "What's the status of 51A-12345?" ✅
- "Get yesterday's mileage for VM300" ✅
