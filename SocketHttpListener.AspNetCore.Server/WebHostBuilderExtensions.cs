using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;

namespace SocketHttpListener.AspNetCore.Server
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
