using Common;
using Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using QuestionService.Clients.AI;
using QuestionService.Data;
using QuestionService.Extensions;
using QuestionService.Services;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<TagService>();

builder.Services.AddKeyCloakAuthentication();

var connString = builder.Configuration.GetConnectionString("questiondb");

builder.Services.AddDbContext<QuestionDbContext>(options =>
{
    options.UseNpgsql(connString);
}, optionsLifetime: ServiceLifetime.Singleton);

await builder.UseWolverineWithRabbitMqAsync(opts =>
{

    opts.ApplicationAssembly = typeof(Program).Assembly;
    opts.PersistMessagesWithPostgresql(connString!);
    opts.UseEntityFrameworkCoreTransactions();
    //opts.PublishAllMessages().ToRabbitExchange("questions");
    opts.PublishMessage<QuestionCreated>().ToRabbitExchange("Contracts.QuestionCreated").UseDurableOutbox();
    opts.PublishMessage<QuestionUpdated>().ToRabbitExchange("Contracts.QuestionUpdated").UseDurableOutbox();
    opts.PublishMessage<QuestionDeleted>().ToRabbitExchange("Contracts.QuestionDeleted").UseDurableOutbox();
});


builder.Services.AddScoped<IGroqAIAnswer, GroqAIAnswer>();
builder.Services.AddScoped<IGeminiModelsClient, GeminiModelsClient>();
builder.Services.AddScoped<IGitHubModelsClient, GitHubModelsClient>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapDefaultEndpoints();
await app.MigrateDbContextAsync<QuestionDbContext>();

app.Run();


