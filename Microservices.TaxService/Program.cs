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
using System.Xml.Linq;
using System.Diagnostics.Metrics;

var builder = WebApplication.CreateBuilder(args);

//
// Setup IoC Container
//

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

AppSettings settings = new AppSettings();
builder.Configuration.Bind(settings);
builder.Services.AddSingleton(settings);

DiagnosticsConfig.ServiceName = builder.Environment.ApplicationName;
DiagnosticsConfig.ActivitySource = new ActivitySource(DiagnosticsConfig.ServiceName);
DiagnosticsConfig.Meter = new(DiagnosticsConfig.ServiceName);
DiagnosticsConfig.TaxCalculateCounter = DiagnosticsConfig.Meter.CreateCounter<long>("app.taxcalculate_counter");


builder.Services.AddOpenTelemetry()
	.WithTracing(builder =>
	{
		builder.AddSource(DiagnosticsConfig.ActivitySource.Name);
		builder.ConfigureResource(resource => resource.AddService(DiagnosticsConfig.ServiceName));

		builder.AddAspNetCoreInstrumentation();

		builder.AddConsoleExporter();
		builder.AddOtlpExporter(); // browse to Jaeger at http://localhost:16686/
	})
	.WithMetrics(builder =>
	{
		builder.ConfigureResource(resource => resource.AddService(DiagnosticsConfig.ServiceName));

		builder.AddAspNetCoreInstrumentation();
		builder.AddRuntimeInstrumentation();
		builder.AddMeter(DiagnosticsConfig.Meter.Name);

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

app.MapPost("/tax/calculate", ([FromBody] CalculateRequest req, [FromServices] AppSettings settings) =>
{
	using var activity = DiagnosticsConfig.ActivitySource.StartActivity("CalculateTax", ActivityKind.Server);
	activity?.AddTag("InvoiceSubtotal", req.InvoiceSubtotal.ToString());
	DiagnosticsConfig.TaxCalculateCounter.Add(1, new KeyValuePair<string, object?>("InvoiceSubtotal", req.InvoiceSubtotal));

	double tax = Math.Round(req.InvoiceSubtotal * settings.TaxRate);
	return new CalculateResponse
	{
		TaxAmount = tax,
		InvoiceTotal = req.InvoiceSubtotal + tax,
	};
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

public class CalculateRequest
{
	public double InvoiceSubtotal { get; set; }
	public string ZipCode { get; set; } = string.Empty;
}
public class CalculateResponse
{
	public double TaxAmount { get; set; }
	public double InvoiceTotal { get; set; }
}

public class AppSettings
{
	public bool SwaggerOn { get; set; }
	public double TaxRate { get; set; }
}

public static class DiagnosticsConfig
{
	public static string ServiceName { get; set; } = null!;
	public static ActivitySource ActivitySource { get; set; } = null!;
	public static Meter Meter { get; set; } = null!;
	public static Counter<long> TaxCalculateCounter { get; set; } = null!;
}

// public for testing
public partial class Program { }
