# Architecture Documentation

Technical architecture documentation for the MCP Server for Vehicle Fleet Management.

## System Overview

```mermaid
flowchart TB
    subgraph "Client Layer"
        Web[Web Frontend]
        Mobile[Mobile App]
        MCP[MCP Client]
    end
    
    subgraph "API Layer"
        Auth[AuthController]
        Chat[ChatController]
        Conv[ConversationController]
        Sess[SessionController]
        SSE[MCP Endpoint /sse]
    end
    
    subgraph "Middleware Layer"
        SM[SessionHeaderMiddleware]
        CCM[ConversationContextMiddleware]
        RL[RateLimiter]
    end
    
    subgraph "Business Logic Layer"
        AS[AuthService]
        VRS[VehicleResolverService]
        VS[VehicleService]
        VSS[VehicleStatusService]
        VHS[VehicleHistoryService]
        WS[WaypointService]
        CCS[ConversationContextService]
        CSS[ConversationSummarizationService]
        SSS[SessionStorageService]
        SVS[SecurityValidationService]
        ALS[AuditLogService]
    end
    
    subgraph "Tool Layer"
        AT[AuthTools]
        VIT[VehicleInfoTools]
        VLST[VehicleLiveStatusTools]
        VHT[VehicleHistoryTools]
    end
    
    subgraph "Data Layer"
        DB[(SQL Server Database)]
        Cache[Memory Cache]
    end
    
    subgraph "External Services"
        GitHubModels[GitHub Models API]
        VehicleAPI[Vehicle API]
        AuthAPI[Auth API]
    end
    
    Web --> Auth
    Web --> Chat
    Web --> Conv
    Mobile --> SSE
    MCP --> SSE
    
    Auth --> SM
    Chat --> SM
    Conv --> SM
    Sess --> SM
    SSE --> SM
    
    SM --> CCM
    CCM --> RL
    
    RL --> AS
    RL --> CCS
    RL --> AT
    RL --> VIT
    RL --> VLST
    RL --> VHT
    
    AS --> AuthAPI
    AS --> SSS
    
    VIT --> VRS
    VIT --> VS
    VLST --> VRS
    VLST --> VSS
    VHT --> VRS
    VHT --> VHS
    VHT --> WS
    
    Chat --> GitHubModels
    CSS --> GitHubModels
    
    VS --> VehicleAPI
    VSS --> VehicleAPI
    VHS --> VehicleAPI
    WS --> VehicleAPI
    
    AT --> SVS
    VIT --> SVS
    VLST --> SVS
    VHT --> SVS
    
    SVS --> ALS
    
    CCS --> DB
    SSS --> DB
    
    WS --> Cache
    VSS --> Cache
```

## Layer Architecture

### 1. Client Layer
- **Web Frontend** - HTML/CSS/JS SPA (in wwwroot/)
- **Mobile Apps** - External mobile clients
- **MCP Clients** - Model Context Protocol compatible clients

### 2. API Layer (Controllers)
- **AuthController** - JWT authentication endpoints
- **ChatController** - Chat proxy to Azure OpenAI
- **ConversationController** - Conversation history management
- **SessionController** - Session lifecycle management
- **MCP Endpoint** - Server-Sent Events for tool invocations

### 3. Middleware Layer
- **SessionHeaderMiddleware** - Injects/validates session IDs
- **ConversationContextMiddleware** - Extracts bearer tokens
- **RateLimitingMiddleware** - Enforces rate limits

### 4. Business Logic Layer (Services)
- **Authentication & Sessions**
  - AuthService, SessionStorageService, JwtHelper
- **Vehicle Operations**
  - VehicleResolverService, VehicleService, VehicleStatusService, VehicleHistoryService, WaypointService
- **Conversation**
  - ConversationContextService, ConversationSummarizationService
- **Security**
  - SecurityValidationService, AuditLogService

### 5. Tool Layer
- AuthTools, VehicleInfoTools, VehicleLiveStatusTools, VehicleHistoryTools
- All use standardized `ExecuteValidatedToolRequestWithContextAsync` pattern

### 6. Data Layer
- **SQL Server** - Primary database (sessions, messages, summaries)
- **File-based Audit Log** - Security events logged to ./logs/audit.log
- **Memory Cache** - Caching for waypoints and vehicle data

### 7. External Services
- **GitHub Models API** - Chat completions and summarization (via Azure.AI.OpenAI SDK)
- **Vehicle API** - External vehicle data provider
- **Auth API** - External authentication service

---

## Database Schema

```mermaid
flowchart TB
    SESSIONS["SESSIONS
    SessionId PK
    CreatedAt
    LastAccessedAt
    BearerTokenHash
    UserId
    IsAnonymous
    Metadata"]
    
    ENTRIES["CONVERSATION_ENTRIES
    Id PK
    SessionId FK
    Timestamp
    Role
    ToolName
    Message
    Metadata
    TokenCount"]
    
    SUMMARIES["CONVERSATION_SUMMARIES
    Id PK
    SessionId FK
    Summary
    SummarySequence
    MessageCount
    TokenCount
    CreatedAt"]
    
    SESSIONS -->|"1 to many has"| ENTRIES
    SESSIONS -->|"1 to many has"| SUMMARIES
```

### Table Descriptions

#### Sessions
Stores user session information and maps bearer tokens to conversation contexts.

**Indexes:**
- `PK_Sessions` on `SessionId`
- `IX_Sessions_LastAccessedAt` on `LastAccessedAt`
- `IX_Sessions_BearerTokenHash` on `BearerTokenHash` (filtered, non-null only)

**Relationships:**
- One-to-many with ConversationEntries (cascade delete)
- One-to-many with ConversationSummaries (cascade delete)

#### ConversationEntries
Stores individual conversation messages and tool invocations.

**Indexes:**
- `PK_ConversationEntries` on `Id`
- `IX_ConversationEntries_SessionId_Timestamp` on `SessionId`, `Timestamp`

**Role Values:**
- `user` - User messages
- `assistant` - AI responses (final answers)
- `tool_call` - Tool invocation records

**Notes:**
- Assistant messages with `ToolName` set are tool responses (filtered from UI)
- Token count tracked per message for budget management

#### ConversationSummaries
Stores AI-generated conversation summaries.

**Indexes:**
- `PK_ConversationSummaries` on `Id`
- `IX_ConversationSummaries_SessionId` on `SessionId`
- `UQ_ConversationSummaries_SessionId_Sequence` on `SessionId`, `SummarySequence` (unique)

**Notes:**
- SummarySequence increments for each new summary (1, 2, 3...)
- Maximum 2 summaries kept per session (configurable)
- Old messages deleted after summarization

---

## Key Flows

### 1. User Authentication Flow

```mermaid
flowchart TD
    Start([User]) --> A["POST /api/auth/login
    username, password"]
    A --> B[AuthController receives request]
    B --> C[AuthController calls AuthService.LoginAsync]
    C --> D["AuthService calls ExternalAuthAPI
    POST /auth/login"]
    D --> E["ExternalAuthAPI returns
    accessToken, refreshToken, expiresIn"]
    E --> F[AuthService calls SessionStorage.StoreTokenPair]
    F --> G[SessionStorage extracts userId from JWT]
    G --> H[Database: Find or create session]
    H --> I[Database returns SessionId]
    I --> J[AuthService returns AuthResponse]
    J --> K["AuthController returns to User
    accessToken, refreshToken, expiresIn"]
    K --> End(["User includes accessToken
    in subsequent requests"])
```

**Steps:**
1. User submits credentials to `/api/auth/login`
2. AuthService calls external authentication API
3. Receives JWT tokens (access + refresh)
4. SessionStorageService extracts userId from JWT
5. Creates or updates session in database with token hash
6. Returns tokens to user
7. User includes accessToken in subsequent requests

---

### 2. Message Processing Flow

```mermaid
flowchart TD
    Start([User]) --> A["POST /api/chat
    Bearer token + messages"]
    A --> B[SessionMiddleware processes request]
    B --> C[ConversationMiddleware adds session header]
    C --> D[ConversationMiddleware extracts bearer token]
    D --> E[ChatController checks context]
    E --> F[ConversationService loads recent messages]
    F --> G[Database returns message history]
    G --> H[ChatController forwards to GitHub Models API]
    H --> I{"Tool call
    required?"}
    I -->|Yes| J[GitHub Models invokes MCP tool via /sse]
    J --> K[MCPTools performs security validation]
    K --> L[MCPTools executes tool logic]
    L --> M[MCPTools returns tool result]
    M --> N[GitHub Models processes tool result]
    N --> O[AzureOpenAI generates final response]
    I -->|No| O
    O --> P[ChatController saves messages async]
    P --> Q["ConversationService persists to Database
    fire-and-forget"]
    P --> R[ChatController returns response to User]
    R --> End([User receives chat response])
```

**Steps:**
1. User sends chat message with bearer token
2. SessionHeaderMiddleware ensures session ID present
3. ConversationContextMiddleware extracts bearer token
4. ChatController loads conversation context
5. Proxies request to Azure OpenAI
6. If tool call needed, OpenAI invokes MCP tool via `/sse`
7. Tool executes after security validation
8. Tool result returned to OpenAI
9. OpenAI generates final response
10. User and assistant messages saved to database (async)
11. Response returned to user

---

### 3. Tool Execution Flow

```mermaid
flowchart TD
    Start([AzureOpenAI]) --> A["POST /sse tool call
    Bearer token + tool args"]
    A --> B[MCPEndpoint receives request]
    B --> C[ToolHelper.ExecuteValidatedToolRequest]
    C --> D["SecurityValidation.ValidateQueryAsync
    Check: SQL injection, XSS,
    prompt injection, domain validation"]
    D --> E{"Validation
    passed?"}
    E -->|No| F[ToolHelper receives validation error]
    F --> G[ConversationService logs blocked query]
    G --> H[Database saves audit entry]
    H --> I[MCPEndpoint returns error response]
    I --> J([AzureOpenAI receives validation error])
    E -->|Yes| K[SecurityValidation returns approved]
    K --> L[ToolHelper logs tool_call entry]
    L --> M[ConversationService saves tool invocation]
    M --> N[Database persists tool invocation]
    N --> O[ToolHelper executes tool action]
    O --> P[ToolLogic calls ExternalAPI]
    P --> Q[ExternalAPI returns vehicle data]
    Q --> R[ToolLogic returns tool result]
    R --> S["ToolHelper logs assistant entry
    Role: assistant, ToolName: set"]
    S --> T[ConversationService saves tool response]
    T --> U[Database persists tool response]
    U --> V[MCPEndpoint returns success response]
    V --> W([AzureOpenAI receives tool result])
```

**Steps:**
1. Azure OpenAI calls MCP tool via `/sse`
2. ToolExecutionHelper validates security
3. Checks for dangerous patterns and domain relevance
4. If validation fails:
   - Logs to audit log
   - Returns error to OpenAI
5. If validation succeeds:
   - Logs tool invocation (role: tool_call)
   - Executes tool business logic
   - Calls external vehicle API
   - Logs tool response (role: assistant, with toolName)
   - Returns result to OpenAI

---

### 4. Conversation Summarization Flow

```mermaid
flowchart TD
    Start([After adding message]) --> A[ConversationService checks message count]
    A --> B{"Count >
    threshold
    default 20?"}
    B -->|No| End([Continue normal operation])
    B -->|Yes| C[ConversationService calls SummarizationService.SummarizeAsync]
    C --> D[SummarizationService loads all messages]
    D --> E[Database returns all session messages]
    E --> F["SummarizationService splits messages
    To summarize: total - preserveLastK
    To preserve: last K messages"]
    F --> G[SummarizationService sends messages to AzureOpenAI]
    G --> H[AzureOpenAI generates summary]
    H --> I[AzureOpenAI returns summary text]
    I --> J["SummarizationService creates ConversationSummary
    with sequence number"]
    J --> K[Database stores summary]
    K --> L["SummarizationService deletes summarized messages
    Keeps recent K messages"]
    L --> M[Database persists changes]
    M --> N[SummarizationService returns to ConversationService]
    N --> End
```

**Steps:**
1. After each message, check message count
2. If count exceeds threshold (default 20):
   - Load all messages from database
   - Split into "to summarize" and "to preserve" (last K)
   - Send messages to GitHub Models API for summarization
   - Receive concise summary text
   - Save as ConversationSummaryEntity with sequence number
   - Delete old messages from database
   - Keep recent K messages for continuity
3. Next chat request includes summary in context

---

### 5. Token Refresh Flow

```mermaid
flowchart TD
    Start([Tool]) --> A[Call AuthService.GetValidAccessToken]
    A --> B[AuthService calls SessionStorage.GetTokenPair]
    B --> C[SessionStorage loads session data from Database]
    C --> D[Database returns accessToken, refreshToken]
    D --> E[SessionStorage returns token pair]
    E --> F["AuthService checks token expiration
    Expires within buffer? default 5 min"]
    F --> G{"Token
    expiring
    soon?"}
    G -->|Yes| H["AuthService calls ExternalAuthAPI
    POST /refresh with refreshToken"]
    H --> I["ExternalAuthAPI returns
    newAccessToken, newRefreshToken"]
    I --> J[AuthService calls SessionStorage.UpdateTokenPair]
    J --> K[SessionStorage updates session tokens in Database]
    K --> L[AuthService returns new access token to Tool]
    G -->|No| M[AuthService returns current access token to Tool]
    L --> N[Tool makes API call with token]
    M --> N
    N --> O{"API returns
    401?"}
    O -->|Yes| P["Tool retries with refresh
    Automatic retry logic"]
    P --> A
    O -->|No| End([API call successful])
```

**Steps:**
1. Tool requests valid access token from AuthService
2. AuthService loads token pair from SessionStorage
3. Checks token expiration time
4. If expiring within buffer (5 minutes):
   - Calls external API to refresh token
   - Receives new access and refresh tokens
   - Updates SessionStorage and database
   - Returns new token
5. If still valid, returns current token
6. Tool uses token for API call
7. If API returns 401, automatic retry with fresh token

---

### 6. Session-to-User Mapping Flow

```mermaid
flowchart TD
    Start([Client]) --> A["Call SessionStorage.GetOrCreateSessionId
    with bearerToken"]
    A --> B["SessionStorage hashes bearer token
    SHA256 hash"]
    B --> C["SessionStorage queries Database
    Find session by hash"]
    C --> D{"Session
    exists?"}
    D -->|Yes| E[Database returns existing session]
    E --> F[SessionStorage updates LastAccessedAt]
    F --> G[SessionStorage returns SessionId to Client]
    G --> End([Client uses SessionId])
    D -->|No| H[SessionStorage calls JwtHelper.GetUserIdFromToken]
    H --> I["JwtHelper decodes JWT
    Extract claims: sub, user_id, username"]
    I --> J[JwtHelper returns UserId]
    J --> K["SessionStorage generates new SessionId
    Guid format"]
    K --> L["SessionStorage creates SessionEntity
    Store: SessionId, BearerTokenHash,
    UserId, CreatedAt, LastAccessedAt"]
    L --> M[Database persists new session]
    M --> N[SessionStorage returns new SessionId to Client]
    N --> End
```

**Steps:**
1. Client makes authenticated request with bearer token
2. SessionStorageService hashes token (SHA256)
3. Queries database for session with matching hash
4. If session exists:
   - Returns existing SessionId
   - Updates LastAccessedAt timestamp
5. If session doesn't exist:
   - Extracts UserId from JWT claims
   - Generates new SessionId (GUID)
   - Creates SessionEntity in database
   - Returns new SessionId
6. Session persists across requests and server restarts

---

## Component Responsibilities

### Controllers (API Layer)
- **Input validation** - Validate request parameters and bodies
- **Authentication** - Verify bearer tokens (except auth endpoints)
- **Routing** - Map HTTP requests to service methods
- **Response formatting** - Transform service results to HTTP responses
- **Error handling** - Catch exceptions and return appropriate status codes

### Services (Business Logic Layer)
- **Business rules** - Implement domain logic and validation
- **External API calls** - Communicate with vehicle and auth APIs
- **Data transformation** - Map between DTOs and domain models
- **Caching** - Cache frequently accessed data (waypoints, vehicle info)
- **Token management** - Handle JWT lifecycle and refresh
- **Security validation** - Enforce guardrails and threat detection

### Tools (MCP Layer)
- **Parameter extraction** - Parse MCP tool arguments
- **Security delegation** - Use ToolExecutionHelper for validation
- **Domain logic** - Implement vehicle tracking operations
- **Response formatting** - Format results for AI consumption
- **Conversation tracking** - Log tool calls and responses

### Data Layer
- **Persistence** - Store and retrieve entities
- **Migrations** - Database schema versioning
- **Relationships** - Enforce foreign key constraints
- **Indexes** - Optimize query performance
- **Cascade deletes** - Clean up related data

---

## Security Architecture

### Authentication Flow
1. User credentials → External Auth API
2. JWT tokens returned (access + refresh)
3. Bearer token in `Authorization` header
4. Token mapped to session in database
5. Session contains userId for audit trail

### Authorization
- Bearer token required for all endpoints (except `/api/auth/*`)
- Rate limiting by token hash (anonymous per-token tracking)
- Session-based access to conversation data
- No role-based access control (RBAC) currently implemented

### Security Validation (Query Guardrails)
- **Pattern Detection**:
  - SQL injection patterns
  - XSS patterns
  - Command injection patterns
  - Prompt injection patterns
- **Domain Validation**:
  - Tool queries must match declared domain
  - Educational queries blocked
  - Off-topic queries blocked
- **AI Guardrails** (optional):
  - GitHub Models API validates query intent
  - Contextual threat analysis
  - Fallback to pattern matching if AI unavailable

### Audit Logging
- File-based audit log (`./logs/audit.log`)
- Blocked queries logged with:
  - UserId
  - Tool name
  - Reason for blocking
  - Query preview (truncated)
  - Timestamp
- Automatic log rotation (10MB max, 5 files)
- Configurable retention (default 7 days)

---

## Performance Considerations

### Caching Strategy
- **WaypointService**: Caches decompressed waypoints (sliding expiration)
- **VehicleStatusService**: Caches vehicle status data (configurable TTL)
- **SessionStorage**: In-memory cache of token pairs

### Database Optimization
- Indexes on frequently queried columns
- Composite indexes for multi-column queries
- Filtered indexes (e.g., BearerTokenHash non-null only)
- Cascade deletes reduce manual cleanup queries

### Async Operations
- Message persistence (fire-and-forget)
- Summarization triggered async
- Token refresh in background
- Audit logging non-blocking

### Rate Limiting
- Sliding window algorithm
- Per-token partitioning
- No queueing (immediate rejection at limit)
- Separate limits for tools (60/min) and conversation API (10/min)

---

## Configuration Management

### appsettings.json Structure
```json
{
  "ApiSettings": {
    "VehicleStatusUrl": "External API endpoints",
    "TokenRefreshBuffer": 300,
    "MaxRetryAttempts": 2
  },
  "ConversationContext": {
    "WindowSize": 10,
    "MaxTokens": 8000,
    "SummaryThreshold": 20
  },
  "ConnectionStrings": {
    "DefaultConnection": "SQL Server connection"
  },
  "Audit": {
    "RetentionDays": 7
  }
}
```

### Environment Variables
- `OPENAI_API_KEY` - Required for summarization and AI guardrails
- `DEFAULT_CONNECTION` - Alternative to appsettings.json connection string

### Design-Time vs Runtime
- **Design-Time**: ConversationDbContextFactory reads appsettings.json for migrations
- **Runtime**: ConversationDbContextFactory injected via DI with connection string
