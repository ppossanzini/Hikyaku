using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

#nullable enable

namespace Hikyaku.Kaido.MCP.Server.Extensions;

public static class Extensions
{
	public static IMvcBuilder AddHikyakuMcpServer(this IMvcBuilder builder, Action<ServerInfo>? configure = null)
	{
		builder.Services.AddOptions<ServerInfo>().Configure(info =>
		{
			info.Name ??= Assembly.GetEntryAssembly()?.GetName().Name ?? Assembly.GetExecutingAssembly().GetName().Name ?? "Hikyaku MCP Server";
			info.Version ??= Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0";
			configure?.Invoke(info);
		});

		return builder;
	}
}