using Events.Models;
using Events.Models.ClickHouseMinimalApi.Models;
using Octonica.ClickHouseClient;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// Cargar configuración de appsettings.json
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// Registrar la cadena de conexión en el contenedor de dependencias
builder.Services.AddSingleton(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("ClickHouseConnection");
    if (string.IsNullOrEmpty(connectionString))
        throw new InvalidOperationException("La cadena de conexión a ClickHouse no está configurada. Compruebe si el archivo existe!");
    return new ClickHouseConnection(connectionString);
});

var app = builder.Build();

// Endpoint para obtener los eventos


// Endpoint para obtener los eventos por fecha
app.MapGet("/events", async (ClickHouseConnection connection, string? date) =>
{
    var events = new List<Event>();

    await connection.OpenAsync();

    // Si la fecha está especificada, convertirla y usarla en la consulta
    string query = "SELECT * FROM events.events";
    if (!string.IsNullOrEmpty(date))
    {
        // Validar y convertir la fecha
        if (DateTime.TryParseExact(date, "yyyyddMM", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
        {
            string formattedDate = parsedDate.ToString("yyyy-MM-dd");
            query += $" WHERE date = '{formattedDate}'";
        }
        else
        {
            return Results.BadRequest(new Response<Event>
            {
                Message = "INVALID_DATE_ERROR"
            });

        }
    }

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
    var res = new Response<List<Event>>(events);
    return Results.Ok(res);
});

// Iniciar la aplicación
app.Run();
