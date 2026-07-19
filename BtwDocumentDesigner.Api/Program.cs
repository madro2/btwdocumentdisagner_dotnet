using BtwDocumentDesigner.Application.Interfaces;
using BtwDocumentDesigner.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.Configure<ElectronicDocumentSourceOptions>(
    builder.Configuration.GetSection(
        ElectronicDocumentSourceOptions.SectionName));

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => { c.EnableAnnotations();  });
builder.Services.AddControllers(options => options.InputFormatters.Insert(0, new BtwDocumentDesigner.Api.Formatters.RawStringInputFormatter()))
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });

// Options Pattern Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));

// Configure DbContext via IOptions
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    options.UseNpgsql(databaseOptions.ConnectionString);
});

// Dependency Injection mappings
builder.Services.AddScoped<IImageRepository, ImageRepository>();
builder.Services.AddScoped<IDesignRepository, DesignRepository>();
builder.Services.AddScoped<ISystemDefaultValueRepository, SystemDefaultValueRepository>();
builder.Services.AddScoped<PdfRenderingEngine>();

builder.Services.AddScoped<IDesignService, DesignService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IGeneratorService, GeneratorService>();
builder.Services.AddHttpClient<
    IElectronicDocumentService,
    BtwDocumentDesigner.Api.Services.ElectronicDocumentService>(
    (serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<ElectronicDocumentSourceOptions>>()
            .Value;
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException(
                "ElectronicDocuments:BaseUrl no contiene una URL absoluta válida.");
        }

        client.BaseAddress = baseUri;
        client.Timeout = TimeSpan.FromSeconds(45);
    });
builder.Services.AddHttpClient();

var app = builder.Build();

    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();

app.UseCors("AllowAll");

app.UseHttpsRedirection();

// Map ASP.NET Core Controllers
app.MapControllers();

app.MapGet("/", () => "Btw Document Designer API is running.");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    await BtwDocumentDesigner.Api.Data.DatabaseSeeder.SeedAsync(
        db,
        app.Environment);
}

app.Run();
