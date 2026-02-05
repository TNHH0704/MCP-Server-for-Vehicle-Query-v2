# MCP Server for Vehicle Query - Integration Guide

A Model Context Protocol (MCP) server providing vehicle tracking, fleet management, and real-time status monitoring capabilities. Built with ASP.NET Core 8.0 and designed for seamless integration with AI chat applications.

## Table of Contents

- [Quick Start](#quick-start)
- [Architecture Overview](#architecture-overview)
- [Authentication Flow](#authentication-flow)
- [MCP Client Integration](#mcp-client-integration)
- [Tool Execution Pattern](#tool-execution-pattern)
- [Conversation Context Management](#conversation-context-management)
- [Configuration Reference](#configuration-reference)
- [API Documentation](#api-documentation)
- [Troubleshooting](#troubleshooting)

## Quick Start

### Prerequisites

- .NET 8.0 SDK or runtime
- Valid credentials for the vehicle tracking API
- Azure OpenAI API key (GitHub Models endpoint)

### Server Setup

1. Clone the repository and configure environment variables:

```bash
# Create .env file
echo "OPENAI_API_KEY=your_github_models_api_key" > .env
```

2. Update `appsettings.json` with your vehicle tracking API endpoint:

```json
{
  "ApiSettings": {
    "AuthApiUrl": "https://your-vehicle-api.com",
    "LoginEndpoint": "/api/login",
    "RefreshTokenEndpoint": "/api/refresh"
  }
}
```

3. Run the server:

```bash
dotnet run
```

The server will start on `http://localhost:8080`.

### Minimal Frontend Example

```html
<!DOCTYPE html>
<html>
<head>
    <title>Vehicle Chat</title>
</head>
<body>
    <div id="chat"></div>
    <input id="input" type="text" placeholder="Ask about vehicles...">
    <button onclick="sendMessage()">Send</button>

    <script type="module">
        import { Client } from "https://esm.sh/@modelcontextprotocol/sdk@0.6.0/client/index.js?bundle";
        
        let client, token;
        const messages = [];

        // Initialize MCP client
        async function init() {
            const transport = createSSETransport();
            client = new Client({ name: "VehicleChat", version: "1.0" }, { capabilities: {} });
            await client.connect(transport);
            
            // Login
            const result = await client.callTool({
                name: "login",
                arguments: { username: "your_user", password: "your_pass" }
            });
            
            const data = JSON.parse(result.content[0].text);
            token = data.data.accessToken;
            console.log("Connected and authenticated");
        }

        // Send message and get AI response
        async function sendMessage() {
            const userMsg = document.getElementById('input').value;
            messages.push({ role: "user", content: userMsg });
            
            const response = await fetch('http://localhost:8080/api/chat', {
                method: 'POST',
                headers: { 
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({
                    model: 'gpt-4o-mini',
                    messages: messages
                })
            });
            
            const data = await response.json();
            const assistantMsg = data.choices[0].message.content;
            messages.push({ role: "assistant", content: assistantMsg });
            
            document.getElementById('chat').innerHTML += `<p><b>You:</b> ${userMsg}</p>`;
            document.getElementById('chat').innerHTML += `<p><b>AI:</b> ${assistantMsg}</p>`;
        }

        function createSSETransport() {
            return {
                sessionId: null,
                onmessage: null,
                
                async start() {},
                
                async send(message) {
                    const response = await fetch('http://localhost:8080/sse', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                            ...(this.sessionId ? { 'Mcp-Session-Id': this.sessionId } : {})
                        },
                        body: JSON.stringify(message)
                    });
                    
                    const reader = response.body.getReader();
                    const decoder = new TextDecoder();
                    let buffer = '';
                    
                    while (true) {
                        const { done, value } = await reader.read();
                        if (done) break;
                        
                        buffer += decoder.decode(value, { stream: true });
                        const lines = buffer.split('\n');
                        buffer = lines.pop() || '';
                        
                        for (const line of lines) {
                            if (line.startsWith('data: ')) {
                                const data = line.slice(6).trim();
                                if (data && this.onmessage) {
                                    this.onmessage(JSON.parse(data));
                                }
                            }
                        }
                    }
                }
            };
        }

        init();
        window.sendMessage = sendMessage;
    </script>
</body>
</html>
```

## Architecture Overview

### Technology Stack

**Server:**
- ASP.NET Core 8.0
- Model Context Protocol SDK
- SQLite with Entity Framework Core
- SharpToken (GPT-4 tokenizer)
- DotNetEnv for environment configuration

**Transport:**
- HTTP/SSE (Server-Sent Events) for MCP protocol
- JSON-RPC 2.0 message format
- REST endpoints for chat proxy and conversation management

**AI Integration:**
- Azure OpenAI via GitHub Models endpoint
- Model: gpt-4o-mini
- Streaming responses with tool calling support

### Core Components

**Endpoints:**
- `POST /sse` - MCP protocol endpoint (tools/list, tools/call)
- `POST /api/chat` - Azure OpenAI proxy for chat completions
- `GET /api/conversation/summary` - Retrieve conversation summary
- `POST /api/conversation/summarize` - Trigger manual summarization
- `GET /api/conversation/history` - Get recent messages
- `POST /api/conversation/clear` - Clear session context
- `GET /api/session` - Create anonymous session ID

**Services:**
- AuthService - Handles authentication and token refresh
- VehicleService - Vehicle registry queries
- VehicleStatusService - Real-time vehicle tracking
- VehicleHistoryService - Historical GPS data with compression
- ConversationContextService - In-memory context with SQLite backup
- ConversationSummarizationService - Automatic conversation summarization
- SecurityValidationService - AI-powered query validation

**Middleware Pipeline:**
1. CORS (allow all origins with credentials)
2. Static Files (serves frontend SPA)
3. Session Header Middleware
4. Rate Limiting
5. Conversation Context Middleware

### Available MCP Tools

**Authentication:**
- `login` - Authenticate and receive JWT tokens
- `refresh_token` - Refresh expired access token

**Vehicle Registry:**
- `get_vehicle_info` - Query vehicle details by plate/ID/group
- `get_fleet_statistics` - Fleet summary and statistics
- `get_vehicles_with_expired_compliance` - Find compliance issues

**Live Status:**
- `get_vehicle_live_status` - Real-time position and status
- `get_daily_statistics` - Daily mileage and runtime stats
- `get_vehicle_daily_status` - Per-vehicle daily summaries

**History & Tracking:**
- `get_vehicle_history` - GPS waypoint history with compression
- `get_trip_summary` - Trip distance, speed, and duration

## Authentication Flow

### Token Management

The server uses JWT bearer tokens for authentication with automatic session mapping.

**Login Process:**

```javascript
// 1. Call login tool via MCP
const loginResult = await client.callTool({
    name: "login",
    arguments: {
        username: "your_username",
        password: "your_password"
    }
});

// 2. Parse token response
const response = JSON.parse(loginResult.content[0].text);
const accessToken = response.data.accessToken;
const refreshToken = response.data.refreshToken;

// 3. Store tokens
localStorage.setItem('fleet_token', accessToken);
localStorage.setItem('fleet_refresh', refreshToken);
```

**Response Format:**

```json
{
  "success": true,
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIs...",
    "refreshToken": "eyJhbGciOiJIUzI1NiIs...",
    "expiresIn": 3600
  }
}
```

### Token Injection

The frontend automatically injects bearer tokens into tool arguments:

```javascript
// Automatic token injection logic
function injectToken(args, token) {
    // Check for token-related fields
    for (const key of Object.keys(args)) {
        if (key.toLowerCase().includes('token') || 
            key.toLowerCase().includes('auth') || 
            key.toLowerCase().includes('bearer')) {
            args[key] = token;
            return;
        }
    }
    
    // Check nested objects
    for (const key in args) {
        if (args[key] && typeof args[key] === 'object') {
            injectToken(args[key], token);
        }
    }
    
    // Fallback: add bearerToken field
    if (!args.bearerToken) {
        args.bearerToken = token;
    }
}
```

### Session Management

Sessions are mapped to bearer tokens on the server:

**C# Server-Side:**

```csharp
public class InMemorySessionStorageService : ISessionStorageService
{
    private readonly ConcurrentDictionary<string, SessionData> _sessions = new();
    
    public string? GetSessionIdForToken(string bearerToken)
    {
        var tokenHash = ComputeSha256Hash(bearerToken);
        return _sessions.Values
            .FirstOrDefault(s => s.BearerTokenHash == tokenHash)?.SessionId;
    }
    
    public void MapTokenToSession(string bearerToken, string sessionId)
    {
        var tokenHash = ComputeSha256Hash(bearerToken);
        _sessions[sessionId] = new SessionData
        {
            SessionId = sessionId,
            BearerTokenHash = tokenHash,
            CreatedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow
        };
    }
}
```

**JavaScript Client-Side:**

```javascript
// Generate session ID
const sessionId = `session_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
localStorage.setItem('fleet_sessionId', sessionId);

// Include in conversation API calls
const response = await fetch(`/api/conversation/summary?sessionId=${sessionId}`, {
    headers: {
        'Authorization': `Bearer ${token}`
    }
});
```

### Token Refresh

Automatically refresh tokens before expiry:

```javascript
async function refreshToken() {
    const refreshToken = localStorage.getItem('fleet_refresh');
    
    const result = await client.callTool({
        name: "refresh_token",
        arguments: { refreshToken }
    });
    
    const response = JSON.parse(result.content[0].text);
    if (response.success) {
        localStorage.setItem('fleet_token', response.data.accessToken);
        localStorage.setItem('fleet_refresh', response.data.refreshToken);
    }
}
```

## MCP Client Integration

### Transport Implementation

Create a custom SSE transport for the MCP client:

```javascript
function createSSETransport() {
    return {
        sessionId: null,
        _onmessage: null,
        _onerror: null,
        _onclose: null,
        
        set onmessage(handler) { this._onmessage = handler; },
        get onmessage() { return this._onmessage; },
        set onerror(handler) { this._onerror = handler; },
        get onerror() { return this._onerror; },
        set onclose(handler) { this._onclose = handler; },
        get onclose() { return this._onclose; },
        
        async start() {
            console.log('Transport starting...');
        },
        
        async send(message) {
            try {
                const response = await fetch('http://localhost:8080/sse', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        ...(this.sessionId ? { 'Mcp-Session-Id': this.sessionId } : {})
                    },
                    body: JSON.stringify(message)
                });
                
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }
                
                // Read SSE stream
                const reader = response.body.getReader();
                const decoder = new TextDecoder();
                let buffer = '';
                
                while (true) {
                    const { done, value } = await reader.read();
                    if (done) break;
                    
                    buffer += decoder.decode(value, { stream: true });
                    const lines = buffer.split('\n');
                    buffer = lines.pop() || '';
                    
                    for (const line of lines) {
                        if (line.startsWith('data: ')) {
                            const data = line.slice(6).trim();
                            if (data) {
                                try {
                                    const jsonData = JSON.parse(data);
                                    if (this._onmessage) {
                                        this._onmessage(jsonData);
                                    }
                                } catch (e) {
                                    console.warn('Failed to parse SSE data:', e);
                                }
                            }
                        }
                    }
                }
            } catch (error) {
                if (this._onerror) {
                    this._onerror(error);
                }
                throw error;
            }
        },
        
        async close() {
            if (this._onclose) {
                this._onclose();
            }
        }
    };
}
```

### Client Initialization

```javascript
import { Client } from "https://esm.sh/@modelcontextprotocol/sdk@0.6.0/client/index.js?bundle";

let client = null;

async function initializeMCPClient() {
    if (client) return;
    
    const transport = createSSETransport();
    client = new Client(
        { name: "FleetClient", version: "1.0" },
        { capabilities: {} }
    );
    
    try {
        await client.connect(transport);
        console.log('MCP connection established');
        return true;
    } catch (error) {
        console.error('MCP connection failed:', error);
        client = null;
        return false;
    }
}
```

### Tool Discovery

Cache the list of available tools:

```javascript
let cachedTools = null;

async function getTools() {
    if (cachedTools) return cachedTools;
    
    const toolsList = await client.listTools();
    
    // Convert to OpenAI function calling format
    cachedTools = toolsList.tools.map(tool => ({
        type: "function",
        function: {
            name: tool.name,
            description: tool.description,
            parameters: tool.inputSchema
        }
    }));
    
    console.log(`Cached ${cachedTools.length} tools`);
    return cachedTools;
}
```

## Tool Execution Pattern

### Complete Flow

The tool execution pattern follows this sequence:

1. User sends message
2. AI receives message + available tools
3. AI decides to call one or more tools
4. Frontend executes tools via MCP
5. Tool results sent back to AI
6. AI generates final response
7. Display response to user

### Implementation

```javascript
const messages = [];
let turnDepth = 0;
const MAX_TURN_DEPTH = 5;

async function processTurn() {
    try {
        turnDepth++;
        if (turnDepth > MAX_TURN_DEPTH) {
            console.error('Max turn depth exceeded');
            return;
        }
        
        await initializeMCPClient();
        const tools = await getTools();
        const token = localStorage.getItem('fleet_token');
        
        // Call AI with tools
        const response = await fetch('http://localhost:8080/api/chat', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify({
                model: 'gpt-4o-mini',
                messages: messages,
                tools: tools,
                tool_choice: 'auto'
            })
        });
        
        if (!response.ok) {
            throw new Error(`Server Error (${response.status})`);
        }
        
        const data = await response.json();
        const assistantMsg = data.choices[0].message;
        messages.push(assistantMsg);
        
        // Handle tool calls
        if (assistantMsg.tool_calls) {
            for (const call of assistantMsg.tool_calls) {
                let args = typeof call.function.arguments === 'string' 
                    ? JSON.parse(call.function.arguments) 
                    : call.function.arguments;
                
                injectToken(args, token);
                
                // Execute tool via MCP
                const result = await client.callTool({
                    name: call.function.name,
                    arguments: args
                });
                
                messages.push({
                    role: "tool",
                    tool_call_id: call.id,
                    content: result.content[0].text
                });
            }
            
            await processTurn();
        } else {
            displayMessage('assistant', assistantMsg.content);
            turnDepth = 0;
        }
    } catch (error) {
        console.error('Error:', error);
        displayMessage('system', 'Error: ' + error.message);
        turnDepth = 0;
    }
}
```

## Conversation Context Management

### Overview

The server maintains conversation context with:
- In-memory sliding window (configurable size)
- SQLite persistence for durability
- Automatic summarization when thresholds are reached
- Token counting using SharpToken (GPT-4 tokenizer)

### Fetching Summaries

```javascript
async function fetchSummary() {
    const sessionId = localStorage.getItem('fleet_sessionId');
    const token = localStorage.getItem('fleet_token');
    
    const response = await fetch(
        `http://localhost:8080/api/conversation/summary?sessionId=${sessionId}`,
        { headers: { 'Authorization': `Bearer ${token}` } }
    );
    
    if (response.ok) {
        const data = await response.json();
        if (data.hasSummary) {
            console.log(`Summary: ${data.messageCount} messages, ${data.tokenCount} tokens`);
        }
    }
}
```

### Auto-Summarization Triggers

The server automatically triggers summarization when:
- Message count reaches threshold (default: 20 messages)
- Token count reaches threshold (default: 6000 tokens)

### Manual Summarization

```javascript
async function triggerSummarization() {
    const sessionId = localStorage.getItem('fleet_sessionId');
    const token = localStorage.getItem('fleet_token');
    
    const response = await fetch('http://localhost:8080/api/conversation/summarize', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${token}`
        },
        body: JSON.stringify({ sessionId })
    });
    
    return await response.json();
}
```

## Configuration Reference

### Environment Variables

```bash
# Required: Azure OpenAI API Key
OPENAI_API_KEY=github_pat_xxxxxxxxxxxxx
```

### appsettings.json

```json
{
  "ApiSettings": {
    "AuthApiUrl": "https://your-vehicle-api.com",
    "LoginEndpoint": "/api/login",
    "RefreshTokenEndpoint": "/api/refresh",
    "TokenRefreshBuffer": 300,
    "MaxRetryAttempts": 2
  },
  
  "OpenAI": {
    "Endpoint": "https://models.inference.ai.azure.com",
    "DeploymentName": "gpt-4o-mini",
    "MaxTokens": 1000,
    "Temperature": 0.1
  },
  
  "ConversationContext": {
    "WindowSize": 5,
    "MaxTokens": 8000,
    "MaxAge": "01:00:00",
    "SummaryEnabled": true,
    "SummaryThreshold": 20,
    "SummaryPreserveLastK": 10,
    "TokenBudgetForSummary": 6000,
    "MaxSummariesPerSession": 2
  }
}
```

### Rate Limiting

**Tool API (POST /sse):**
- 60 requests per minute per token
- 1-minute sliding window

**Conversation API (/api/conversation/*):**
- 10 requests per minute per token
- 1-minute sliding window

## API Documentation

### POST /sse

Execute MCP protocol operations.

**Headers:**
- `Content-Type: application/json`
- `Mcp-Session-Id: <session-id>` (optional)

**Request (tools/list):**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/list"
}
```

**Response:**
```
data: {"jsonrpc":"2.0","id":1,"result":{"tools":[...]}}
```

### POST /api/chat

Proxy to Azure OpenAI for chat completions.

**Headers:**
- `Content-Type: application/json`
- `Authorization: Bearer <token>`

**Request:**
```json
{
  "model": "gpt-4o-mini",
  "messages": [...],
  "tools": [...]
}
```

### GET /api/conversation/summary

Retrieve conversation summary.

**Query Parameters:**
- `sessionId` (required)

**Response:**
```json
{
  "hasSummary": true,
  "summary": "...",
  "messageCount": 25,
  "tokenCount": 6543
}
```

### POST /api/conversation/summarize

Trigger manual summarization.

**Body:**
```json
{
  "sessionId": "session_xxx"
}
```

### GET /api/conversation/history

Get recent messages.

**Query Parameters:**
- `sessionId` (required)
- `limit` (optional, max: 20)

## Troubleshooting

### 401 Unauthorized from /api/chat

**Cause:** Invalid OPENAI_API_KEY environment variable

**Solution:**
```bash
# Verify environment variable
echo $OPENAI_API_KEY

# Update .env file
echo "OPENAI_API_KEY=your_key" > .env

# Restart server
dotnet run
```

### 429 Too Many Requests

**Cause:** Rate limit exceeded

**Solution:** Implement exponential backoff

```javascript
async function callWithRetry(fn, maxRetries = 3) {
    for (let i = 0; i < maxRetries; i++) {
        try {
            return await fn();
        } catch (error) {
            if (error.status === 429 && i < maxRetries - 1) {
                await new Promise(resolve => 
                    setTimeout(resolve, Math.pow(2, i) * 1000)
                );
            } else {
                throw error;
            }
        }
    }
}
```

### MCP Connection Failed

**Solution:**
```bash
# Check if server is running
netstat -tlnp | grep 8080

# Restart server
dotnet run
```

### Token Injection Not Working

**Solution:**
```javascript
// Verify token is stored
console.log('Token:', localStorage.getItem('fleet_token'));

// Manual fallback
args.bearerToken = localStorage.getItem('fleet_token');
```

## License

This server is provided as-is for integration with vehicle tracking systems.