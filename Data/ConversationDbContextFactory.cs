using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace McpVersionVer2.Data;

public class ConversationDbContextFactory : IDesignTimeDbContextFactory<ConversationDbContext>
{
    public ConversationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ConversationDbContext>();
        optionsBuilder.UseSqlite("Data Source=./data/conversation.db");
        
        return new ConversationDbContext(optionsBuilder.Options);
    }
}
