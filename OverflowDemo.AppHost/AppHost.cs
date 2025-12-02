using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var compose = builder.AddDockerComposeEnvironment("production")
    .WithDashboard(dashboard => dashboard.WithHostPort(8080));

string RequiredDev(IConfiguration cfg, string key)
{
    var v = cfg[key];
    if (string.IsNullOrWhiteSpace(v))
        throw new InvalidOperationException($"Missing config value: {key}");
    return v;
}
string Placeholder(string name) => "${" + name + "}";

var keyCloak = builder.AddKeycloak("keycloak", 6001)
    .WithDataVolume("keycloaktc-data")
    .WithRealmImport("../infra/realms")
    .WithEnvironment("KC_HTTP_ENABLED", "true")
    .WithEnvironment("KC_HOSTNAME_STRICT", "false")
    .WithEnvironment("KC_PROXY_HEADERS", "xforwarded")
    .WithEnvironment("VIRTUAL_HOST", "id.overflow.local")
    .WithEnvironment("VIRTUAL_PORT", "8080");

var postgres = builder.AddPostgres("postgres", port: 5432)
    .WithDataVolume("postgrestc-data")
    .WithPgWeb();

var typesenseApiKey = builder.Environment.IsDevelopment()
    ? builder.Configuration["Parameters:typesense-api-key"]
      ?? throw new InvalidOperationException("Could not get typesense api key")
    : "${TYPESENSE_API_KEY}";

var typesense = builder.AddContainer("typesense", "typesense/typesense", "29.0")
    .WithArgs("--data-dir", "/data", "--enable-cors")
    .WithVolume("typesensetc-data", "/data")
    .WithEnvironment("TYPESENSE_API_KEY", typesenseApiKey)
    .WithHttpEndpoint(8108, 8108, name: "typesense");

var typesenseContainer = typesense.GetEndpoint("typesense");

var questionDb = postgres.AddDatabase("questiondb");
var profileDb = postgres.AddDatabase("profileDb");
var statDb = postgres.AddDatabase("statDb");
var voteDb = postgres.AddDatabase("voteDb");

var rabbitmq = builder.AddRabbitMQ("messaging")
    .WithDataVolume("rabbitmqtc-data")
    .WithManagementPlugin(port: 15672);


var groqValue   = builder.Environment.IsDevelopment()
    ? RequiredDev(builder.Configuration, "Groq:ApiKey")
    : Placeholder("GROQ_API_KEY");

var geminiValue = builder.Environment.IsDevelopment()
    ? RequiredDev(builder.Configuration, "Gemini:ApiKey")
    : Placeholder("GEMINI_API_KEY");

var githubValue = builder.Environment.IsDevelopment()
    ? RequiredDev(builder.Configuration, "Github:ApiKey")
    : Placeholder("GITHUB_API_KEY");

var questionService = builder.AddProject<Projects.QuestionService>("question-svc")
    .WithEnvironment("Groq__ApiKey", groqValue)
    .WithEnvironment("Gemini__ApiKey", geminiValue)
    .WithEnvironment("Github__ApiKey", githubValue)
    .WithReference(keyCloak)
    .WithReference(questionDb)
    .WithReference(rabbitmq)
    .WaitFor(keyCloak)
    .WaitFor(questionDb)
    .WaitFor(rabbitmq);

var searchService = builder.AddProject<Projects.SearchService>("search-svc")
    .WithReference(typesenseContainer)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WaitFor(typesense);

var profileService = builder.AddProject<Projects.ProfileService>("profile-svc")
    .WithReference(keyCloak)
    .WithReference(profileDb)
    .WithReference(rabbitmq)
    .WaitFor(keyCloak)
    .WaitFor(profileDb)
    .WaitFor(rabbitmq);

var statService = builder.AddProject<Projects.StatsService>("stat-svc")
    .WithReference(statDb)
    .WithReference(rabbitmq)
    .WaitFor(statDb)
    .WaitFor(rabbitmq);

var voteService = builder.AddProject<Projects.VoteService>("vote-svc")
    .WithReference(keyCloak)
    .WithReference(voteDb)
    .WithReference(rabbitmq)
    .WaitFor(keyCloak)
    .WaitFor(voteDb)
    .WaitFor(rabbitmq);

var yarp = builder.AddYarp("gateway")
    .WithConfiguration(yarpBuilder =>
    {
        yarpBuilder.AddRoute("/questions/{**catch-all}", questionService);
        yarpBuilder.AddRoute("/test/{**catch-all}", questionService);
        yarpBuilder.AddRoute("/tags/{**catch-all}", questionService);
        yarpBuilder.AddRoute("/aimodeles/{**catch-all}", questionService);
        yarpBuilder.AddRoute("/search/{**catch-all}", searchService);
        yarpBuilder.AddRoute("/profiles/{**catch-all}", profileService);
        yarpBuilder.AddRoute("/stats/{**catch-all}", statService);
        yarpBuilder.AddRoute("/votes/{**catch-all}", voteService);
        yarpBuilder.AddRoute("/votesai/{**catch-all}", voteService);
        yarpBuilder.AddRoute("/votesais/{**catch-all}", voteService);
    })
    .WithEnvironment("ASPNETCORE_URLS", "http://*:8001")
    .WithEndpoint(port: 8001, targetPort: 8001, scheme: "http", name: "gateway", isExternal: true)
    .WithEnvironment("VIRTUAL_HOST", "api.overflow.local")
    .WithEnvironment("VIRTUAL_PORT", "8001");


var webapp = builder.AddNpmApp("webapp", "../webapp", "dev")
    .WithReference(keyCloak)
    .WithHttpEndpoint(env: "PORT", port: 3000, targetPort: 4000)
    .WithEnvironment("VIRTUAL_HOST", "app.overflow.local")
    .WithEnvironment("VIRTUAL_PORT", "4000")
    .PublishAsDockerFile();

if (!builder.Environment.IsDevelopment())
{
    builder.AddContainer("nginx-proxy", "nginxproxy/nginx-proxy", "1.8")
        .WithEndpoint(80, 80, "nginx", isExternal: true)
        .WithEndpoint(443, 443, "nginx-ssl", isExternal: true)
        .WithBindMount("/var/run/docker.sock", "/tmp/docker.sock", true)
        .WithBindMount("../infra/devcerts", "/etc/nginx/certs", true);
}



builder.Build().Run();
