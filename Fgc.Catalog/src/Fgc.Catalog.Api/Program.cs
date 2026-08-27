using Amazon.DynamoDBv2;
using Fgc.Catalog.Application.Consumers;
using Fgc.Catalog.Application.Interfaces;
using Fgc.Catalog.Application.Services;
using Fgc.Catalog.Infrastructure.Persistence;
using Fgc.Catalog.Infrastructure.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// ============================================================
// 1. CONFIGURAÇÕES DO MICROSSERVIÇO DE CATÁLOGO
// ============================================================
// A. Banco de Dados (Entity Framework Core)
builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("CatalogDb"))
);
// B. Injeção de Dependências (Repositories)
builder.Services.AddScoped<IGameRepository, GameRepository>();
builder.Services.AddScoped<IUserLibraryRepository, UserLibraryRepository>();

// B2. DynamoDB (log de eventos cross-service)
builder.Services.AddSingleton<IAmazonDynamoDB>(_ =>
{
    var serviceUrl = builder.Configuration["AWS:DynamoDB:ServiceUrl"];
    var config = new AmazonDynamoDBConfig();
    if (!string.IsNullOrEmpty(serviceUrl))
    {
        config.ServiceURL = serviceUrl;
    }
    else
    {
        // O client exige Region ou ServiceURL só para ser construído (falha até em ambientes que
        // nunca chegam a chamar a API, como testes de integração) - cai para us-east-1 quando
        // nem AWS_REGION nem o endpoint local estão configurados.
        config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(
            Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1");
    }
    return new AmazonDynamoDBClient(config);
});
builder.Services.AddScoped<IEventLogRepository, DynamoDbEventLogRepository>();

// B3. Redis (cache de GET /games)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:ConnectionString"];
    options.InstanceName = "FgcCatalog:";
});

// C. Injeção de Dependências (Services)
builder.Services.AddScoped<GameService>();
// D. Autenticação JWT
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            )
        };
    });
// Swagger com suporte a JWT
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Insira o token JWT no formato: Bearer {seu token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
// E. MassTransit / RabbitMQ
// Local (docker-compose): RabbitMq:UseSsl ausente -> broker self-hosted em texto plano, sem mudança.
// Produção: RabbitMq:UseSsl=true aponta para o endpoint AMQPS do Amazon MQ (porta 5671, TLS).
var rabbitHost = builder.Configuration["RabbitMq:Host"] ?? "localhost";
var rabbitUseSsl = builder.Configuration.GetValue<bool>("RabbitMq:UseSsl");
var rabbitPort = builder.Configuration.GetValue<int?>("RabbitMq:Port") ?? (rabbitUseSsl ? 5671 : 5672);
var rabbitVirtualHost = builder.Configuration["RabbitMq:VirtualHost"] ?? "/";
var rabbitUsername = builder.Configuration["RabbitMq:Username"] ?? "admin";
var rabbitPassword = builder.Configuration["RabbitMq:Password"] ?? "admin";

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<UserCreatedEventConsumer>();
    x.AddConsumer<PaymentProcessedEventConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        if (rabbitUseSsl)
        {
            cfg.Host(new Uri($"rabbitmqs://{rabbitHost}:{rabbitPort}{rabbitVirtualHost}"), h =>
            {
                h.Username(rabbitUsername);
                h.Password(rabbitPassword);
            });
        }
        else
        {
            cfg.Host(rabbitHost, rabbitVirtualHost, h =>
            {
                h.Username(rabbitUsername);
                h.Password(rabbitPassword);
            });
        }
        cfg.ReceiveEndpoint("catalog-user-created-queue", e =>
        {
            e.ConfigureConsumer<UserCreatedEventConsumer>(context);
        });
        cfg.ReceiveEndpoint("catalog-payment-processed-queue", e =>
        {
            e.ConfigureConsumer<PaymentProcessedEventConsumer>(context);
        });
    });
});
// ============================================================
var app = builder.Build();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseHttpMetrics();
// ============================================================
// 2. MIDDLEWARES DE SEGURANÇA
// ============================================================
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapMetrics();
app.MapGet("/health", () => Results.Ok());
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    context.Database.EnsureCreated();
}
// Só cria a tabela automaticamente contra um endpoint local (dynamodb-local) - nem em produção
// (isso é responsabilidade do IaC) nem contra a AWS real por engano em testes/dev sem Docker.
if (!string.IsNullOrEmpty(builder.Configuration["AWS:DynamoDB:ServiceUrl"]))
{
    await DynamoDbEventLogTableInitializer.EnsureTableExistsAsync(
        app.Services.GetRequiredService<IAmazonDynamoDB>());
}
app.Run();
public partial class Program { } // Para testes de integração