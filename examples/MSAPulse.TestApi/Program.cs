using Microsoft.EntityFrameworkCore;
using MSAPulse.Infrastructure.Extensions;
using MSAPulse.TestApi.Data;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------------
// 1. CLEAR DEFAULT LOGGERS
// We remove default providers to prevent duplicate log entries in the console.
// MSAPulse (via Serilog) will handle all logging.
// -----------------------------------------------------------------------------
builder.Logging.ClearProviders();

// -----------------------------------------------------------------------------
// 2. REGISTER MSAPULSE SERVICES
// This registers the necessary services, options, and logging infrastructure.
// -----------------------------------------------------------------------------
builder.Services.AddMSAPulse(builder.Configuration, options =>
{
    options.IncludeExceptionDetails = true; // Useful for debugging in Development
});

// -----------------------------------------------------------------------------
// 3. CONFIGURE DATABASE & INTERCEPTOR
// Register the DbContext and attach the MSAPulse performance interceptor.
// -----------------------------------------------------------------------------
builder.Services.AddDbContext<TestDbContext>((sp, options) =>
{
    // Using SQLite for easy local testing without external dependencies.
    options.UseSqlite("Data Source=msapulse_test.db")
           .AddMSAPulseInterceptor<TestDbContext>(sp); // <--- Vital for capturing SQL metrics
});

// Standard ASP.NET Core services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// -----------------------------------------------------------------------------
// 4. INITIALIZE DATABASE (FOR DEMO/TESTING)
// Reset the database on every run to ensure a clean state for tests.
// -----------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();
    context.Database.EnsureDeleted(); // Clean up old data
    context.Database.EnsureCreated(); // Re-create tables
}

// -----------------------------------------------------------------------------
// 5. ENABLE MSAPULSE MIDDLEWARE
// IMPORTANT: This must be placed at the top of the pipeline to capture
// all requests, global exceptions, and correlation IDs correctly.
// -----------------------------------------------------------------------------
app.UseMSAPulse();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();