using System.Text.Json;
using McpVersionVer2.Services;
using McpVersionVer2.Security;

namespace McpVersionVer2.Helpers;
public static class ToolExecutionHelper
{
    /// <summary>
    /// Executes a validated tool request with standardized exception handling.
    /// </summary>
    public static async Task<string> ExecuteValidatedToolRequestWithContextAsync<T>(
        SecurityValidationService securityService,
        string queryContext,
        string domain,
        string bearerToken,
        IConversationContextService contextService,
        RequestContextService requestContext,
        Func<string, Task<T>> action,
        Func<T, string> successResponse)
    {
        try
        {
            return await securityService.ExecuteValidatedToolRequestWithContext(
                queryContext: queryContext,
                domain: domain,
                bearerToken: bearerToken,
                contextService: contextService,
                requestContext: requestContext,
                action: action,
                successResponse: successResponse);
        }
        catch (ToolValidationException ex)
        {
            return ex.ErrorResponse;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, AppJsonSerializerOptions.Default);
        }
    }
}