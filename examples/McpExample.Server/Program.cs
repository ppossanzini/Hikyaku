using System.Security.Cryptography;
using System.Text;
using Hikyaku;
using Hikyaku.Kaido.MCP;
using Hikyaku.Kaido.MCP.Server.Extensions;
using McpExample;
using McpExample.Requests;
using McpExample.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHikyaku(configuration =>
  configuration.RegisterServicesFromAssembly(typeof(GetServerTimeTool).Assembly));

builder.Services.AddHikyakuMcpServer(mcp =>
{
  mcp.ServerInfo.Name = "McpExample";
  mcp.ServerInfo.Title = "Hikyaku MCP example server";

  // Exposure modes (pick one or combine):
  // mcp.RegisterAllComponents();                                  // 1) everything loaded in the AppDomain
  mcp.RegisterComponentsFromAssemblies(typeof(GetServerTimeTool).Assembly); // 2) specific assemblies
  // mcp.RegisterComponents(typeof(GetServerTimeTool), typeof(SumNumbersTool)); // 3) explicit types only

  // Callers must present a token as "Authorization: Bearer <key>" or "X-Api-Key" header.
  // The callout owns the validation: constant-time comparison here, but it could be a
  // database lookup, a token introspection call, etc. Async overloads are available.
  var expectedKey = Encoding.UTF8.GetBytes(builder.Configuration["Mcp:ApiKey"] ?? "dev-secret-token");
  mcp.Security.WithApiKey(apiKey =>
    CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(apiKey), expectedKey));

  // mcp.Security.RequireAuthenticatedUser();
});

// Enriches incoming requests with data from the HTTP call (claims, headers).
// Typed per request via IRequestEnrich<T>; open generics are supported too:

builder.Services.AddHttpContextAccessor();
builder.Services.AddMcpRequestEnricher<ClientNameEnricher>();
//builder.Services.AddMcpRequestEnricher(typeof(IRequestEnrich<>), typeof(ClientNameEnricher).Assembly);

var app = builder.Build();

app.MapHikyakuMcpServer("/mcp");

app.Run();
