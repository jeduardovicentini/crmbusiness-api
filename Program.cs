
using CRMBusiness.Controllers;
using CRMBusiness.Services;
using CRMBusiness.Services.Interfaces;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.BigQuery.V2;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;
using Serilog;
using System.Runtime.Intrinsics.X86;

//Environment.SetEnvironmentVariable(
//    "GOOGLE_APPLICATION_CREDENTIALS",
//    @"D:\Projetos\CRMBusiness\Credentials\dutiprojects-1b940674b7a3.json"
//);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        "logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddHttpClient();

builder.Services.AddScoped<BigQueryService>();
builder.Services.AddScoped<FormsDataProcessingService>();
builder.Services.AddScoped<EccoFibrasService>();

builder.Services.AddScoped<IFormsDataProcessingService, FormsDataProcessingService>();

builder.Services.Configure<BigQuerySettings>(builder.Configuration.GetSection("BigQuerySettings"));

builder.WebHost.UseUrls("http://0.0.0.0:80");

// 🔹 REGISTRAR BIGQUERY
//builder.Services.AddSingleton<BigQueryClient>(sp =>
//{
//    var configuration = sp.GetRequiredService<IConfiguration>();
//    var projectId = configuration["BigQuerySettings:ProjectIdGoogle"];

//    Log.Information("Registrando BigQueryClient com ProjectId: {ProjectId}", projectId);


//    if (string.IsNullOrEmpty(projectId))
//        throw new Exception("Google:ProjectId não configurado no appsettings.json");

//    return BigQueryClient.Create(projectId);
//});

builder.Services.AddSingleton(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var projectId = configuration["BigQuerySettings:ProjectIdGoogle"];

    Log.Information("Registrando BigQueryClient com ProjectId: {ProjectId}", projectId);

    if (string.IsNullOrEmpty(projectId))
        throw new Exception("Google:ProjectId não configurado no appsettings.json");

    var jsonCredentials = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS_JSON");

    if (string.IsNullOrEmpty(jsonCredentials))
        throw new Exception("Credenciais do Google não configuradas");

    var credential = GoogleCredential.FromJson(jsonCredentials);

    return BigQueryClient.Create(projectId, credential);
});


// Configurar serviços do Hangfire
builder.Services.AddHangfire(config =>
{
    config.UsePostgreSqlStorage(
        builder.Configuration.GetConnectionString("HangfireConnection")
    );
});

// Habilitar o servidor do Hangfire
builder.Services.AddHangfireServer();

var app = builder.Build();


app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionHandlerPathFeature =
            context.Features.Get<IExceptionHandlerPathFeature>();

        if (exceptionHandlerPathFeature?.Error != null)
        {
            Log.Error(
                exceptionHandlerPathFeature.Error,
                "Erro não tratado na aplicação");
        }

        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("Erro interno. Consulte os logs.");
    });
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.UseStaticFiles();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


// ADICIONE ANTES do UseHangfireDashboard
app.UseAuthentication();
app.UseAuthorization();

// Depois configure o Hangfire Dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new AllowAllDashboardAuthorizationFilter() }
});

// Obter uma instância do serviço BigQueryService através do ServiceProvider
using (var scope = app.Services.CreateScope())
{
    var bigQueryService = scope.ServiceProvider.GetRequiredService<BigQueryService>();

    // Registrar um job recorrente no Hangfire
    RecurringJob.AddOrUpdate(
        "SincronizarOportunidadesStatusDiario_deboraribeirotricot",
        () => bigQueryService.SincronizarStatusAsync(
            "deboraribeirotricot_mnbicvun",
            bigQueryService.ObterProjetoInternoSettings("deboraribeirotricot")
        ),
        Cron.Daily() // Agendado para executar diariamente às 0h
    );

    var formsDataService = scope.ServiceProvider.GetRequiredService<FormsDataProcessingService>();

    RecurringJob.AddOrUpdate(
        "SincronizarFormsDiaro_deboraribeirotricot",
        () => formsDataService.SyncFormsDataAsync(
            "deboraribeirotricot_mnbicvun",
            formsDataService.ObterProjetoInternoSettings("deboraribeirotricot")
        ),
        Cron.Daily(23) // Executa diariamente às 23h 
    );
}

app.Run();