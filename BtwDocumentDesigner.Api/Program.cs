var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => { c.EnableAnnotations();  });
builder.Services.AddControllers(options => options.InputFormatters.Insert(0, new BtwDocumentDesigner.Api.Formatters.RawStringInputFormatter()));
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// Options Pattern Configuration
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
builder.Services.AddScoped<PdfRenderingEngine>();
builder.Services.AddSingleton<ContractValidationService>();

builder.Services.AddScoped<IDesignService, DesignService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IGeneratorService, GeneratorService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();

// Map ASP.NET Core Controllers
app.MapControllers();

app.MapGet("/", () => "Btw Document Designer API is running.");

app.Run();
