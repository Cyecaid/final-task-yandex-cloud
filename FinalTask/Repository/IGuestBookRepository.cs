using FinalTask.Models;

namespace FinalTask.Repository;

public interface IGuestbookRepository : IAsyncDisposable
{
    Task InitializeAsync();
    Task CreateSchemaAsync();
    Task<List<Message>> GetMessagesAsync(ulong limit = 20);
    Task AddMessageAsync(string user, string content);
}