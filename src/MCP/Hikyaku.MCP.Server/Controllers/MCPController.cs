using Hikyaku.Kaido.MCP.Server.dto;
using Hikyaku.Kaido.MCP.Server.JsonRPC;
using Hikyaku.Kaido.MCP.Server.MCP;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hikyaku.Kaido.MCP.Server.Controllers;

[ApiController]
public class MCPController(IOptions<ServerInfo> serverInfo) : ControllerBase
{
  [Route("mcp")]
  public async Task<IActionResult> Handshake(Request request)
  {
    switch (request.Method)
    {
      case "initialize": return Initialize(request);
      case "tools/list": return ToolsList(request);
      case "tools/call": return ToolsList(request);
    }

    return BadRequest("Unsupported method");
  }


  public IActionResult Initialize(Request request)
  {
    // Standard successful response for Agent handshake
    return Ok(
      request.SuccessfulResponse(
        new MCP.InitializeResponse()
        {
          Capabilities = new Capabilities(),
          ServerInfo = serverInfo.Value
        }
      ));
  }


  public IActionResult ToolsList(Request request)
  {
    var weatherTool = new McpTool
    {
      Name = "get_meteo_citta",
      Description = "Ottiene le informazioni meteo correnti per una specifica città italiana.",
      InputSchema = new
      {
        type = "object",
        properties = new
        {
          citta = new { type = "string", description = "Il nome della città (es. Roma, Milano)" },
          giorni = new { type = "integer", description = "Numero di giorni di previsione (opzionale)" }
        },
        required = new[] { "citta" }
      }
    };


    return Ok(new
    {
      jsonrpc = "2.0",
      id = request.Id,
      result = new
      {
        Tools = new { weatherTool }
      }
    });
  }
}