using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

//
// Setup IoC Container
//

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks()
   .AddDbContextCheck<ProductDbContext>(failureStatus: HealthStatus.Degraded);

AppSettings settings = new AppSettings();
builder.Configuration.Bind(settings);
builder.Services.AddSingleton(settings);

DiagnosticsConfig.ServiceName = builder.Environment.ApplicationName;
DiagnosticsConfig.ActivitySource = new ActivitySource(DiagnosticsConfig.ServiceName);

string? connectionString = builder.Configuration.GetConnectionString("ProductCatalog");
ArgumentNullException.ThrowIfNullOrEmpty(connectionString, nameof(connectionString));
builder.Services.AddDbContext<ProductDbContext>(options => options.UseNpgsql(connectionString).UseLowerCaseNamingConvention());

builder.Services.AddOpenTelemetry()
	.WithTracing(builder =>
	{
		builder.AddSource(DiagnosticsConfig.ActivitySource.Name);
		builder.ConfigureResource(resource => resource.AddService(DiagnosticsConfig.ServiceName));

		builder.AddAspNetCoreInstrumentation();
		builder.AddHttpClientInstrumentation();
		builder.AddSqlClientInstrumentation();
		builder.AddEntityFrameworkCoreInstrumentation();

		builder.AddConsoleExporter();
		builder.AddOtlpExporter(); // browse to Jaeger at http://localhost:16686/
	})
	.WithMetrics(builder =>
	{
		builder.ConfigureResource(resource => resource.AddService(DiagnosticsConfig.ServiceName));

		builder.AddAspNetCoreInstrumentation();
		builder.AddHttpClientInstrumentation();
		builder.AddRuntimeInstrumentation();

		builder.AddConsoleExporter();
		builder.AddPrometheusExporter(); // TODO: setup Prometheus and Grafana
		builder.AddOtlpExporter();
	});

builder.Logging.AddOpenTelemetry(options =>
{
	options.IncludeFormattedMessage = true;
	options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(DiagnosticsConfig.ServiceName));

	options.AddConsoleExporter();
});


var app = builder.Build();

//
// Configure the HTTP request pipeline.
//

if (app.Environment.IsDevelopment() || settings.SwaggerOn)
{
	app.UseSwagger();
	app.UseSwaggerUI();
}
else
{
	app.UseExceptionHandler("/error");
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.MapGet("/", () => new { message = "Ok" }); // Docker doesn't like the home page to error

app.MapGet("/products", ([FromServices] ProductDbContext db) => {
	using var activity = DiagnosticsConfig.ActivitySource.StartActivity("GetProductCatalog", ActivityKind.Server);
	return (
		from p in db.Products
		orderby p.Name
		select p
	).ToList();
});

app.MapHealthChecks("/health", new HealthCheckOptions
{
	ResponseWriter = async (context, report) =>
	{
		var jsonOptions = new JsonSerializerOptions
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		};

		var response = new
		{
			Status = report.Status.ToString(),
			HealthCheckDuration = report.TotalDuration,
			Info = report.Entries.Select(e => new
			{
				Key = e.Key,
				Description = e.Value.Description,
				Duration = e.Value.Duration,
				Status = Enum.GetName(typeof(HealthStatus), e.Value.Status),
				Error = e.Value.Exception?.Message,
				Data = e.Value.Data
			})
		};

		context.Response.ContentType = MediaTypeNames.Application.Json;
		await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
	}
});

app.Map("/error", () => Results.Problem());
app.Map("/{*url}", () => Results.NotFound(new { message = "Not Found", status = 404 }));

app.Run();

public class Product
{
	public int Id { get; set; }
	public required string Name { get; set; }
	public decimal Price { get; set; }
	public required string Description { get; set; }
}

public class ProductDbContext : DbContext
{
	public ProductDbContext(DbContextOptions<ProductDbContext> options) : base(options)
	{
	}

	public DbSet<Product> Products => Set<Product>();
}

public class AppSettings
{
	public bool SwaggerOn { get; set; }
}

public static class DiagnosticsConfig
{
	public static string ServiceName { get; set; } = null!;
	public static ActivitySource ActivitySource { get; set; } = null!;
}

// public for testing
public partial class Program { }
