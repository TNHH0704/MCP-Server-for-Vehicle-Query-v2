# API Reference

Complete REST API documentation for the MCP Server for Vehicle Fleet Management.

## Base URL

```
http://localhost:8080
```

## Authentication

All endpoints (except `/api/auth/*`) require a bearer token in the `Authorization` header:

```
Authorization: Bearer <access_token>
```

## Response Codes

| Code | Description |
|------|-------------|
| 200 | Success |
| 400 | Bad Request - Invalid parameters |
| 401 | Unauthorized - Missing or invalid token |
| 429 | Too Many Requests - Rate limit exceeded |
| 500 | Internal Server Error |

---

## Auth Controller

### POST /api/auth/login

Authenticate user and receive access/refresh tokens.

**Rate Limit:** None

**Request:**
```json
{
  "username": "string",
  "password": "string"
}
```

**Response (200):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

**Errors:**
- `400` - Missing username or password
- `401` - Invalid credentials
- `500` - Authentication service error

**Example:**
```bash
curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "password123"
  }'
```

---

### POST /api/auth/refresh

Refresh an expired access token using refresh token.

**Rate Limit:** None

**Request:**
```json
{
  "refreshToken": "string"
}
```

**Response (200):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 3600
}
```

**Errors:**
- `400` - Missing refresh token
- `401` - Invalid or expired refresh token
- `500` - Refresh service error

**Example:**
```bash
curl -X POST http://localhost:8080/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
  }'
```

---

## Chat Controller

### POST /api/chat

Proxy chat requests to Azure OpenAI with automatic conversation tracking.

**Authentication:** Required  
**Rate Limit:** None

**Request:**
```json
{
  "messages": [
    {
      "role": "user|assistant|system",
      "content": "string"
    }
  ],
  "model": "string (optional)",
  "temperature": 0.7 (optional),
  "max_tokens": 1000 (optional)
}
```

**Response (200):**
Returns OpenAI chat completion response format.

```json
{
  "choices": [
    {
      "message": {
        "role": "assistant",
        "content": "Response text"
      },
      "finish_reason": "stop"
    }
  ],
  "usage": {
    "prompt_tokens": 10,
    "completion_tokens": 20,
    "total_tokens": 30
  }
}
```

**Errors:**
- `401` - Missing or invalid bearer token
- `400` - Invalid request format
- `500` - OpenAI API error

**Example:**
```bash
curl -X POST http://localhost:8080/api/chat \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "messages": [
      {"role": "user", "content": "What is the status of vehicle 51A-12345?"}
    ]
  }'
```

**Notes:**
- User messages and final assistant responses are automatically saved to conversation history
- Tool messages are excluded from persistence
- Session ID derived from bearer token or specified in `X-Session-Id` header

---

## Conversation Controller

All conversation endpoints require authentication and are rate-limited (10 requests/minute).

### GET /api/conversation/messages

Get paginated conversation message history for the current user.

**Authentication:** Required  
**Rate Limit:** 10/minute (conversationApi)

**Query Parameters:**
- `page` (int, optional) - Page number (default: 1)
- `pageSize` (int, optional) - Items per page (default: 50, max: 100)

**Response (200):**
```json
{
  "messages": [
    {
      "id": "guid",
      "timestamp": "2026-02-09T10:30:00Z",
      "role": "user|assistant",
      "message": "Message content",
      "toolName": null,
      "tokenCount": 15
    }
  ],
  "totalCount": 100,
  "page": 1,
  "pageSize": 50,
  "hasMore": true
}
```

**Errors:**
- `401` - Missing bearer token
- `429` - Rate limit exceeded
- `500` - Database error

**Example:**
```bash
curl "http://localhost:8080/api/conversation/messages?page=1&pageSize=20" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**Notes:**
- Returns only user and final assistant messages (excludes tool responses)
- Newest messages first
- Tool responses (messages with `toolName` set) are filtered out

---

### GET /api/conversation/summary

Get the latest conversation summary for a session.

**Authentication:** Required  
**Rate Limit:** 10/minute (conversationApi)

**Query Parameters:**
- `sessionId` (string, required) - Session identifier

**Response (200):**
```json
{
  "hasSummary": true,
  "summary": "User asked about vehicle 51A-12345...",
  "messageCount": 25,
  "tokenCount": 3420,
  "createdAt": "2026-02-09T10:30:00Z",
  "sequence": 1
}
```

**Response (200 - No Summary):**
```json
{
  "hasSummary": false
}
```

**Errors:**
- `400` - Missing sessionId
- `429` - Rate limit exceeded
- `500` - Database error

**Example:**
```bash
curl "http://localhost:8080/api/conversation/summary?sessionId=session_abc123" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

### POST /api/conversation/summarize

Manually trigger conversation summarization for a session.

**Authentication:** Required  
**Rate Limit:** 10/minute (conversationApi)

**Request:**
```json
{
  "sessionId": "string"
}
```

**Response (200):**
```json
{
  "success": true,
  "message": "Summarization completed",
  "messageCount": 25,
  "tokenCount": 3420
}
```

**Errors:**
- `400` - Missing sessionId or no messages to summarize
- `429` - Rate limit exceeded
- `500` - Summarization failed

**Example:**
```bash
curl -X POST http://localhost:8080/api/conversation/summarize \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "sessionId": "session_abc123"
  }'
```

---

### GET /api/conversation/history

Get recent conversation messages with optional limit.

**Authentication:** Required  
**Rate Limit:** 10/minute (conversationApi)

**Query Parameters:**
- `sessionId` (string, required) - Session identifier
- `limit` (int, optional) - Number of messages (1-20, default: 10)

**Response (200):**
```json
{
  "sessionId": "session_abc123",
  "messageCount": 10,
  "tokenCount": 1500,
  "messages": [
    {
      "id": "guid",
      "timestamp": "2026-02-09T10:30:00Z",
      "role": "user",
      "toolName": null,
      "message": "What is the status?",
      "tokenCount": 5
    }
  ]
}
```

**Errors:**
- `400` - Missing sessionId or invalid limit
- `429` - Rate limit exceeded
- `500` - Database error

**Example:**
```bash
curl "http://localhost:8080/api/conversation/history?sessionId=session_abc123&limit=10" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

### GET /api/conversation/context

Get formatted context (system prompt + summaries + recent messages) for a session.

**Authentication:** Required  
**Rate Limit:** 10/minute (conversationApi)

**Query Parameters:**
- `sessionId` (string, required) - Session identifier

**Response (200):**
```json
{
  "sessionId": "session_abc123",
  "context": "# System Instructions\nYou are...\n\n## Conversation Summary\n...\n\n## Recent Messages\n..."
}
```

**Errors:**
- `400` - Missing sessionId
- `429` - Rate limit exceeded
- `500` - Service error

**Example:**
```bash
curl "http://localhost:8080/api/conversation/context?sessionId=session_abc123" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**Notes:**
- Returns the exact context string sent to AI models
- Includes system prompt, summaries (if any), and recent messages
- Token budget applied according to configuration

---

### DELETE /api/conversation/clear

Clear all conversation history for the current user.

**Authentication:** Required  
**Rate Limit:** 10/minute (conversationApi)

**Response (200):**
```json
{
  "success": true,
  "message": "Conversation history cleared",
  "messagesDeleted": 50,
  "summariesDeleted": 2
}
```

**Errors:**
- `401` - Missing bearer token
- `429` - Rate limit exceeded
- `500` - Database error

**Example:**
```bash
curl -X DELETE http://localhost:8080/api/conversation/clear \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**Notes:**
- Hard deletes all messages and summaries from database
- Also clears in-memory conversation context
- Cannot be undone

---

## Session Controller

### GET /api/session/current

Get or create a session for the authenticated user.

**Authentication:** Required  
**Rate Limit:** None

**Response (200):**
```json
{
  "sessionId": "session_abc123",
  "userId": "user_123",
  "isAnonymous": false,
  "createdAt": "2026-02-09T10:00:00Z",
  "lastAccessedAt": "2026-02-09T10:30:00Z"
}
```

**Errors:**
- `401` - Missing bearer token
- `500` - Service error

**Example:**
```bash
curl http://localhost:8080/api/session/current \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**Notes:**
- Session automatically created if it doesn't exist
- Session ID derived from bearer token hash
- Session persists across server restarts

---

### DELETE /api/session/current

Clear the current user's session and all associated data.

**Authentication:** Required  
**Rate Limit:** None

**Response (200):**
```json
{
  "success": true,
  "message": "Session cleared successfully"
}
```

**Errors:**
- `401` - Missing bearer token
- `404` - Session not found
- `500` - Service error

**Example:**
```bash
curl -X DELETE http://localhost:8080/api/session/current \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**Notes:**
- Cascade deletes all conversation messages and summaries
- In-memory context is cleared
- Cannot be undone

---

## MCP Endpoint

### POST /sse

Server-Sent Events endpoint for MCP (Model Context Protocol) tool invocations.

**Authentication:** Required  
**Rate Limit:** 60/minute (toolApi)

**Request Format:**
MCP tool call format via SSE. See [TOOLS.md](TOOLS.md) for individual tool schemas.

**Response:**
Server-Sent Events stream with tool execution results.

**Errors:**
- `401` - Missing or invalid bearer token
- `403` - Security validation failed
- `429` - Rate limit exceeded (60 requests/minute)
- `500` - Tool execution error

**Notes:**
- All tool executions are validated by SecurityValidationService
- Tool calls and responses logged to conversation history
- Blocked queries logged to audit log
- See [TOOLS.md](TOOLS.md) for complete tool catalog

---

## Rate Limiting

### Headers

When rate limited, the response includes:
```
HTTP/1.1 429 Too Many Requests
Retry-After: 60
```

### Policies

| Policy | Endpoints | Limit | Window |
|--------|-----------|-------|--------|
| `toolApi` | `/sse` | 60 requests | 1 minute |
| `conversationApi` | `/api/conversation/*` | 10 requests | 1 minute |

### Best Practices

- Implement exponential backoff when receiving 429 errors
- Cache responses where appropriate
- Use pagination to reduce request frequency
- Monitor rate limit headers in responses

---

## Error Response Format

All error responses follow this format:

```json
{
  "error": "error_code",
  "message": "Human-readable error description"
}
```

### Common Error Codes

| Code | Description |
|------|-------------|
| `unauthorized` | Missing or invalid authentication |
| `invalid_request` | Malformed request body or parameters |
| `rate_limit_exceeded` | Too many requests |
| `session_not_found` | Session ID not found |
| `api_error` | External API error |
| `database_error` | Database operation failed |
| `validation_error` | Security validation failed |
