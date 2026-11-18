using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Scalar.AspNetCore;

// Checkpoint 03 : PostgreSQL via Entity Framework Core
var builder = WebApplication.CreateBuilder(args);

// La chaîne de connexion vient d'appsettings.json, surchargée par la variable d'environnement
// ConnectionStrings__Default (c'est ce que fait Docker Compose).
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default est manquante.");

builder.Services.AddDbContext<TodoDb>(options => options.UseNpgsql(connectionString));
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

// Au démarrage : on crée la table si besoin. Si PostgreSQL est injoignable,
// on loggue une erreur claire et on sort avec le code 1 (visible avec « docker ps -a »).
var target = new NpgsqlConnectionStringBuilder(connectionString);
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TodoDb>();
    db.Database.EnsureCreated();

    if (!db.Todos.Any())
    {
        db.Todos.AddRange(
            new Todo { Title = "Installer Docker Desktop", CreatedAt = DateTime.Now },
            new Todo { Title = "Lancer le workshop", CreatedAt = DateTime.Now },
            new Todo { Title = "Ne pas paniquer", CreatedAt = DateTime.Now });
        db.SaveChanges();
    }

    app.Logger.LogInformation("Connecté à PostgreSQL : Host={Host}, Port={Port}, Database={Database}",
        target.Host, target.Port, target.Database);
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex,
        "Impossible de joindre PostgreSQL (Host={Host}, Port={Port}). " +
        "Dans un conteneur, 'localhost' désigne le conteneur lui-même : utilisez le nom du service (Host=postgres).",
        target.Host, target.Port);
    return 1;
}

app.MapGet("/", () => Results.Redirect("/scalar"));

app.MapGet("/info", () => new
{
    machine = Environment.MachineName,
    os = RuntimeInformation.OSDescription,
    runtime = RuntimeInformation.FrameworkDescription,
    inContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true",
    database = $"{target.Host}:{target.Port}/{target.Database}",
    now = DateTime.Now,
    timeZone = TimeZoneInfo.Local.Id
});

app.MapGet("/todos", async (TodoDb db) => await db.Todos.OrderBy(t => t.Id).ToListAsync());

app.MapPost("/todos", async (NewTodo input, TodoDb db) =>
{
    if (string.IsNullOrWhiteSpace(input.Title))
        return Results.BadRequest(new { error = "Le titre est obligatoire." });

    var todo = new Todo { Title = input.Title.Trim(), CreatedAt = DateTime.Now };
    db.Todos.Add(todo);
    await db.SaveChangesAsync();
    return Results.Created($"/todos/{todo.Id}", todo);
});

app.MapPatch("/todos/{id:int}/toggle", async (int id, TodoDb db) =>
{
    var todo = await db.Todos.FindAsync(id);
    if (todo is null)
        return Results.NotFound();

    todo.Done = !todo.Done;
    await db.SaveChangesAsync();
    return Results.Ok(todo);
});

app.MapDelete("/todos/{id:int}", async (int id, TodoDb db) =>
{
    var todo = await db.Todos.FindAsync(id);
    if (todo is null)
        return Results.NotFound();

    db.Todos.Remove(todo);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();
return 0;

class Todo
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool Done { get; set; }
    public DateTime CreatedAt { get; set; }
}

record NewTodo(string Title);

class TodoDb(DbContextOptions<TodoDb> options) : DbContext(options)
{
    public DbSet<Todo> Todos => Set<Todo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Todo>(e =>
        {
            e.Property(t => t.Title).HasMaxLength(200).IsRequired();
            // DateTime.Now (heure locale, Algérie = UTC+1) : colonne « sans fuseau » pour Npgsql
            e.Property(t => t.CreatedAt).HasColumnType("timestamp without time zone");
        });
    }
}
