using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapPost("/api/weather", (WeatherForecastRequest request) =>
{
    return new WeatherForecastResponse
        (
            DateOnly.FromDateTime(DateTime.Now),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)],
            request.Location
        );
})
.WithName("GetWeatherForecast")
.WithSummary("Get the weather forecast for a specific location")
.WithDescription("This endpoint provides a weather forecast for the specified location, including temperature and summary.")
.WithMetadata(["weather", "forecast", "location"])
.WithTags(["Weather", "Forecast"])
.Produces<WeatherForecastResponse>();

app.Run();

internal record WeatherForecastRequest(string Location)
{
}

internal record WeatherForecastResponse(DateOnly Date, int TemperatureC, string? Summary, string Location)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
