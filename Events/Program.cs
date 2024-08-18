using Events.Models;
using Events.Models.ClickHouseMinimalApi.Models;
using Octonica.ClickHouseClient;
using StackExchange.Redis;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// Cargar configuración de appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// Configurar Redis
ConfigureRedis(builder);

// Registrar la cadena de conexión en el contenedor de dependencias
ConfigureClickHouse(builder);

var app = builder.Build();

// Endpoint para obtener los eventos por fecha
app.MapGet("/events", async (ClickHouseConnection connection, IConnectionMultiplexer redis, string? date) =>
{
    if (string.IsNullOrEmpty(date))
    {
        return Results.BadRequest(new Response<Event> { Message = "DATE_REQUIRED" });
    }

    if (!TryParseDate(date, out DateTime parsedDate))
    {
        return Results.BadRequest(new Response<Event> { Message = "INVALID_DATE_ERROR" });
    }

    var cacheKey = $"events:{date}";
    var events = await GetEventsFromCache(cacheKey, redis);

    if (events == null)
    {
        events = await FetchEventsFromDatabase(connection, parsedDate);
        await CacheEvents(cacheKey, events, redis);
    }

    return Results.Ok(new Response<List<Event>>(events));
});

// Iniciar la aplicación
app.Run();

void ConfigureRedis(WebApplicationBuilder builder)
{
    var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection");
    if (string.IsNullOrEmpty(redisConnectionString))
        throw new InvalidOperationException("La cadena de conexión a Redis no está configurada. Compruebe si el archivo existe!");

    var redis = ConnectionMultiplexer.Connect(redisConnectionString);
    builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
}

void ConfigureClickHouse(WebApplicationBuilder builder)
{
    builder.Services.AddSingleton(sp =>
    {
        var connectionString = builder.Configuration.GetConnectionString("ClickHouseConnection");
        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException("La cadena de conexión a ClickHouse no está configurada. Compruebe si el archivo existe!");

        return new ClickHouseConnection(connectionString);
    });
}

bool TryParseDate(string date, out DateTime parsedDate)
{
    return DateTime.TryParseExact(date, "yyyyddMM", null, System.Globalization.DateTimeStyles.None, out parsedDate);
}

async Task<List<Event>?> GetEventsFromCache(string cacheKey, IConnectionMultiplexer redis)
{
    var redisDb = redis.GetDatabase();
    var cachedEvents = await redisDb.StringGetAsync(cacheKey);

    return cachedEvents.HasValue
        ? System.Text.Json.JsonSerializer.Deserialize<List<Event>>(cachedEvents)
        : null;
}

async Task<List<Event>> FetchEventsFromDatabase(ClickHouseConnection connection, DateTime date)
{
    var events = new List<Event>();
    await connection.OpenAsync();

    var query = $"SELECT * FROM events.events WHERE date = '{date:yyyy-MM-dd}'";
    var command = connection.CreateCommand(query);

    using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        events.Add(new Event
        {
            Id = reader.GetFieldValue<uint>("id"),
            Type = reader.GetFieldValue<uint>("type"),
            Lat = reader.GetFieldValue<float>("lat"),
            Lon = reader.GetFieldValue<float>("lon"),
            DescriptionEs = reader.GetFieldValue<string>("description_es"),
            DescriptionEn = reader.GetFieldValue<string>("description_en"),
            Date = reader.GetFieldValue<DateTime>("date"),
            MediaUrl = reader.GetFieldValue<string[]>("media.url").ToList(),
            MediaType = reader.GetFieldValue<string[]>("media.type").ToList(),
            MediaDescriptionEs = reader.GetFieldValue<string[]>("media.description_es").ToList(),
            MediaDescriptionEn = reader.GetFieldValue<string[]>("media.description_en").ToList()
        });
    }

    return events;
}

async Task CacheEvents(string cacheKey, List<Event> events, IConnectionMultiplexer redis)
{
    var redisDb = redis.GetDatabase();
    var serializedEvents = System.Text.Json.JsonSerializer.Serialize(events);
    await redisDb.StringSetAsync(cacheKey, serializedEvents, TimeSpan.FromMinutes(30));
}
