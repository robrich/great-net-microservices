var builder = WebApplication.CreateBuilder(args);

//
// Setup IoC Container
//

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks()
   .AddDbContextCheck<ShoppingCartDbContext>(failureStatus: HealthStatus.Degraded);

AppSettings settings = new AppSettings();
builder.Configuration.Bind(settings);
builder.Services.AddSingleton(settings);

DiagnosticsConfig.ServiceName = builder.Environment.ApplicationName;
DiagnosticsConfig.ActivitySource = new ActivitySource(DiagnosticsConfig.ServiceName);
DiagnosticsConfig.Meter = new(DiagnosticsConfig.ServiceName);
DiagnosticsConfig.OrderCreationCalculator = DiagnosticsConfig.Meter.CreateCounter<long>("app.ordercreate_counter");

string? connectionString = builder.Configuration.GetConnectionString("ShoppingCart");
ArgumentNullException.ThrowIfNullOrEmpty(connectionString, nameof(connectionString));
builder.Services.AddDbContext<ShoppingCartDbContext>(options => options.UseNpgsql(connectionString).UseLowerCaseNamingConvention());

builder.Services.AddHttpClient("TaxService", config =>
{
	config.BaseAddress = new Uri(settings.TaxService);
});

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

app.MapControllers();

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

// public for testing
public partial class Program { }
