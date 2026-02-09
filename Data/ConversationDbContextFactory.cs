using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace McpVersionVer2.Data;

public sealed class ConversationDbContextFactory : IDesignTimeDbContextFactory<ConversationDbContext>, IDbContextFactory<ConversationDbContext>
{
    private readonly string? _connectionString;

    /// <summary>
    /// Parameterless constructor for EF Core design-time tools.
    /// Reads connection string from appsettings.json.
    /// </summary>
    public ConversationDbContextFactory()
    {
        _connectionString = null;
    }

    /// <summary>
    /// Constructor with connection string for runtime dependency injection.
    /// </summary>
    public ConversationDbContextFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Create DbContext for EF Core design-time tools.
    /// </summary>
    public ConversationDbContext CreateDbContext(string[] args)
    {
        var connectionString = GetConnectionString();
        var optionsBuilder = new DbContextOptionsBuilder<ConversationDbContext>();
        optionsBuilder.UseSqlServer(connectionString);
        return new ConversationDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// Create DbContext for runtime use.
    /// </summary>
    public ConversationDbContext CreateDbContext()
    {
        var connectionString = GetConnectionString();
        var optionsBuilder = new DbContextOptionsBuilder<ConversationDbContext>();
        optionsBuilder.UseSqlServer(connectionString);
        return new ConversationDbContext(optionsBuilder.Options);
    }

    private string GetConnectionString()
    {
        if (!string.IsNullOrEmpty(_connectionString))
        {
            return _connectionString;
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "DefaultConnection is not configured. " +
                "Please set the connection string in appsettings.json or via environment variable ConnectionStrings__DefaultConnection."
            );
        }
        
        return connectionString;
    }
}
