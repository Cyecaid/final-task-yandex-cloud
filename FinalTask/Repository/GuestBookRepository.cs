using FinalTask.Models;
using Ydb.Sdk;
using Ydb.Sdk.Services.Table;
using Ydb.Sdk.Value;
using Ydb.Sdk.Yc;

namespace FinalTask.Repository;


public class YdbRepository : IGuestbookRepository
{
    private readonly Driver _driver;
    private readonly TableClient _tableClient;

    public YdbRepository(IConfiguration configuration)
    {
        var endpoint = configuration["YDB_ENDPOINT"] ?? "grpcs://ydb.serverless.yandexcloud.net:2135";
        var database = configuration["YDB_DATABASE"] ?? "";
        
        var metadataProvider = new MetadataProvider(); 

        var config = new DriverConfig(
            endpoint: endpoint,
            database: database,
            credentials: metadataProvider
        );

        _driver = new Driver(config);
        _tableClient = new TableClient(_driver);
    }

    public async Task InitializeAsync() 
        => await _driver.Initialize();

    public async Task CreateSchemaAsync()
    {
        const string query = 
            $"""
              CREATE TABLE messages (
                  id Utf8,
                  user_name Utf8,
                  content Utf8,
                  created_at Datetime,
                  PRIMARY KEY (id)
              );
            """;

        try
        {
            await _tableClient.SessionExec(async session =>
                await session.ExecuteSchemeQuery(query));
            Console.WriteLine("Table 'messages' created.");
        }
        catch (Exception ex)
        {
             await _tableClient.SessionExec(async session =>
                await session.ExecuteSchemeQuery("ALTER TABLE messages ADD COLUMN user_name Utf8;"));
        }
    }

    public async Task<List<Message>> GetMessagesAsync(ulong limit = 20)
    {
        const string query = 
            """
            DECLARE $limit AS Uint64;
            SELECT id, user_name, content, created_at FROM messages 
            ORDER BY created_at DESC LIMIT $limit;
            """;

        var parameters = new Dictionary<string, YdbValue>
        {
            { "$limit", YdbValue.MakeUint64(limit) }
        };

        var response = await _tableClient.SessionExec(async session =>
            await session.ExecuteDataQuery(
                query: query,
                txControl: TxControl.BeginSerializableRW().Commit(),
                parameters: parameters
            ));

        response.Status.EnsureSuccess();

        var queryRes = (ExecuteDataQueryResponse)response;
        var rows = queryRes.Result.ResultSets[0].Rows;
        
        return rows.Select(r => new Message(
            Id: (string?)r["id"] ?? Guid.NewGuid().ToString(),
            User: (string?)r["user_name"] ?? "Anonymous", 
            Content: (string?)r["content"] ?? "",
            CreatedAt: (DateTime?)r["created_at"] ?? DateTime.UtcNow
        )).ToList();
    }

    public async Task AddMessageAsync(string user, string content)
    {
        // Добавил user_name в параметры и insert
        const string query = 
            """
            DECLARE $id AS Utf8;
            DECLARE $user AS Utf8;
            DECLARE $content AS Utf8;
            DECLARE $created AS Datetime;
            
            UPSERT INTO messages (id, user_name, content, created_at)
            VALUES ($id, $user, $content, $created);
            """;

        var parameters = new Dictionary<string, YdbValue>
        {
            { "$id", YdbValue.MakeUtf8(Guid.NewGuid().ToString()) },
            { "$user", YdbValue.MakeUtf8(user) },
            { "$content", YdbValue.MakeUtf8(content) },
            { "$created", YdbValue.MakeDatetime(DateTime.UtcNow) }
        };

        var response = await _tableClient.SessionExec(async session =>
            await session.ExecuteDataQuery(
                query: query,
                txControl: TxControl.BeginSerializableRW().Commit(),
                parameters: parameters
            ));

        response.Status.EnsureSuccess();
    }

    public async ValueTask DisposeAsync()
    {
        _tableClient.Dispose();
        await _driver.DisposeAsync();
    }
}