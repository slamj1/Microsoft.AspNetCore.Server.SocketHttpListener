using System;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Server.SocketHttpListener;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once CheckNamespace
namespace Microsoft.AspNetCore.Hosting
{
	public static class WebHostBuilderExtensions
	{
		public static IWebHostBuilder UseSocketHttpListener(this IWebHostBuilder builder)
		{
			return builder.ConfigureServices(services =>
			{
				services.AddSingleton<IServer, SocketHttpListenerServer>();
			});
		}

		public static IWebHostBuilder UseSocketHttpListener(
			this IWebHostBuilder builder,
			Action<SocketHttpListenerOptions> options)
		{
			return builder.UseSocketHttpListener().ConfigureServices(services =>
			{
				services.Configure(options);
			});
		}
	}
}
