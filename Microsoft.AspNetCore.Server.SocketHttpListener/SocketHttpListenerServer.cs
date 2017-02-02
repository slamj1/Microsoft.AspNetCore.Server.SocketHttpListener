using System;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocketHttpListener.Net;

namespace Microsoft.AspNetCore.Server.SocketHttpListener
{
	public class SocketHttpListenerServer : IServer
	{
		private readonly HttpListener _listener;
		private readonly ILogger _logger;

		public IFeatureCollection Features { get; } = new FeatureCollection();

		public SocketHttpListenerServer(
			IOptions<SocketHttpListenerOptions> options,
			ILoggerFactory loggerFactory)
		{
			_logger = loggerFactory.CreateLogger(typeof(SocketHttpListenerServer).FullName);
			_listener = new HttpListener(
				new SocketHttpListenerLogger(loggerFactory),
				options.Value.Certificate);

			Features.Set<IServerAddressesFeature>(new ServerAddressesFeature());
		}

		public void Start<TContext>(IHttpApplication<TContext> application)
		{
			if (application == null)
				throw new ArgumentNullException(nameof(application));

			_listener.OnContext = async context =>
			{
				var featureContext = new FeatureContext(context);
				var appContext = application.CreateContext(featureContext.Features);
				try
				{
					try
					{
						await application.ProcessRequestAsync(appContext);
						await featureContext.OnStart();
						application.DisposeContext(appContext, null);
						await featureContext.OnFinish();
					}
					finally
					{
						await featureContext.OnCompleted();
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(0, ex, "ProcessRequestAsync");

					//context.Response.Abort();
					context.Response.Headers.Clear();
					context.Response.StatusCode = 500;
					context.Response.ContentLength64 = 0;
					context.Response.Close();

					application.DisposeContext(appContext, ex);
				}
			};

			_listener.Prefixes.Clear();
			foreach (var address in Features.Get<IServerAddressesFeature>().Addresses)
			{
				var withPath = address.EndsWith("/") ? address : address + "/";
				_listener.Prefixes.Add(withPath);
			}
			_listener.Start();
		}

		public void Dispose()
		{
			_listener.Stop();
		}
	}
}
