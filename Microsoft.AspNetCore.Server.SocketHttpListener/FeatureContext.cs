using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using IPAddress = System.Net.IPAddress;
using HttpListenerContext = SocketHttpListener.Net.HttpListenerContext;

namespace Microsoft.AspNetCore.Server.SocketHttpListener
{
	class FeatureContext :
		IHttpBufferingFeature,
		IHttpConnectionFeature,
		IHttpRequestFeature,
		IHttpResponseFeature,
		IHttpSendFileFeature
	{
		private const int BufferSize = 64 * 1024;

		private readonly HttpListenerContext _context;
		private readonly IHeaderDictionary _requestHeaders;
		private readonly IHeaderDictionary _responseHeaders;

		private readonly List<Tuple<Func<object, Task>, object>> _onStartingActions;
		private readonly List<Tuple<Func<object, Task>, object>> _onCompletedActions;
		private bool _responseStarted;
		private bool _completed;
		private string _requestScheme;
		private string _requestPath;
		private Stream _responseStream;
		private Stream _requestStream;
		private Func<Task> _onFinished;

		public FeatureContext(HttpListenerContext context)
		{
			_context = context;
			_onCompletedActions = new List<Tuple<Func<object, Task>, object>>();
			_onStartingActions = new List<Tuple<Func<object, Task>, object>>();

			Features = new FeatureCollection();
			Features.Set<IHttpRequestFeature>(this);
			Features.Set<IHttpResponseFeature>(this);
			Features.Set<IHttpConnectionFeature>(this);
			Features.Set<IHttpSendFileFeature>(this);
			Features.Set<IHttpBufferingFeature>(this);

			_requestScheme = context.Request.Url.Scheme;
			_requestPath = Uri.UnescapeDataString(context.Request.Url.AbsolutePath);
			_requestHeaders = new HeaderDictionary(context.Request.Headers);
			_responseHeaders = new HeaderDictionary(context.Response.Headers);

			var buffer = new MemoryStream();

			_requestStream = context.Request.InputStream;
			_responseStream = new ResponseStream(buffer, OnStart);

			_onFinished = async () =>
			{
				_context.Response.ContentLength64 = buffer.Length;

				buffer.Seek(0, SeekOrigin.Begin);
				await buffer.CopyToAsync(_context.Response.OutputStream);

				_context.Response.OutputStream.Flush();
			};
		}

		internal IFeatureCollection Features { get; }

		#region IHttpConnectionFeature

		string IHttpConnectionFeature.ConnectionId
		{
			get { return _context.Request.RequestTraceIdentifier.ToString(); }
			set { throw new NotSupportedException(); }
		}

		IPAddress IHttpConnectionFeature.RemoteIpAddress
		{
			get { return _context.Request.RemoteEndPoint.Address; }
			set { throw new NotSupportedException(); }
		}

		IPAddress IHttpConnectionFeature.LocalIpAddress
		{
			get { return _context.Request.LocalEndPoint.Address; }
			set { throw new NotSupportedException(); }
		}

		int IHttpConnectionFeature.RemotePort
		{
			get { return _context.Request.RemoteEndPoint.Port; }
			set { throw new NotSupportedException(); }
		}

		int IHttpConnectionFeature.LocalPort
		{
			get { return _context.Request.LocalEndPoint.Port; }
			set { throw new NotSupportedException(); }
		}

		#endregion

		#region IHttpRequestFeature

		/// <summary>
		/// The HTTP-version as defined in RFC 7230. E.g. "HTTP/1.1"
		/// </summary>
		string IHttpRequestFeature.Protocol
		{
			get { return $"HTTP/{_context.Request.ProtocolVersion}"; }
			set { throw new NotSupportedException(); }
		}

		string IHttpRequestFeature.Scheme
		{
			get { return _requestScheme; }
			set { _requestScheme = value; }
		}

		string IHttpRequestFeature.Method
		{
			get { return _context.Request.HttpMethod; }
			set { throw new NotSupportedException(); }
		}

		string IHttpRequestFeature.PathBase
		{
			get { return string.Empty; }
			set { throw new NotSupportedException(); }
		}

		/// <summary>
		/// The portion of the request path that identifies the requested resource.
		/// The value is un-escaped.
		/// The value may be string.Empty if PathBase contains the full path.
		/// </summary>
		string IHttpRequestFeature.Path
		{
			get { return _requestPath; }
			set { _requestPath = value; }
		}

		/// <summary>
		/// The query portion of the request-target as defined in RFC 7230.
		/// The value may be string.Empty.
		/// If not empty then the leading '?' will be included.
		/// The value is in its original form, without un-escaping.
		/// </summary>
		string IHttpRequestFeature.QueryString
		{
			get { return _context.Request.Url.Query; }
			set { throw new NotSupportedException(); }
		}

		/// <summary>
		/// The request target as it was sent in the HTTP request.
		/// This property contains the raw path and full query,
		/// as well as other request targets such as * for OPTIONS requests
		/// (https://tools.ietf.org/html/rfc7230#section-5.3).
		/// </summary>
		string IHttpRequestFeature.RawTarget
		{
			get { return _context.Request.RawUrl; }
			set { throw new NotSupportedException(); }
		}

		IHeaderDictionary IHttpRequestFeature.Headers
		{
			get { return _requestHeaders; }
			set { throw new NotSupportedException(); }
		}

		Stream IHttpRequestFeature.Body
		{
			get { return _requestStream; }
			set { _requestStream = value; }
		}

		#endregion

		#region IHttpResponseFeature

		void IHttpResponseFeature.OnStarting(Func<object, Task> callback, object state)
		{
			if (callback == null)
				throw new ArgumentNullException(nameof(callback));

			if (_responseStarted)
				throw new InvalidOperationException("Cannot register new callbacks, the response has already started.");

			_onStartingActions.Add(Tuple.Create(callback, state));
		}

		void IHttpResponseFeature.OnCompleted(Func<object, Task> callback, object state)
		{
			if (callback == null)
				throw new ArgumentNullException(nameof(callback));

			if (_completed)
				throw new InvalidOperationException("Cannot register new callbacks, the response has already completed.");

			_onCompletedActions.Add(Tuple.Create(callback, state));
		}

		int IHttpResponseFeature.StatusCode
		{
			get { return _context.Response.StatusCode; }
			set { _context.Response.StatusCode = value; }
		}

		string IHttpResponseFeature.ReasonPhrase
		{
			get { return _context.Response.StatusDescription; }
			set { _context.Response.StatusDescription = value; }
		}

		IHeaderDictionary IHttpResponseFeature.Headers
		{
			get { return _responseHeaders; }
			set { throw new NotSupportedException(); }
		}

		Stream IHttpResponseFeature.Body
		{
			get { return _responseStream; }
			set { _responseStream = value; }
		}

		bool IHttpResponseFeature.HasStarted => _responseStarted;

		#endregion

		#region IHttpSendFileFeature

		async Task IHttpSendFileFeature.SendFileAsync(
			string path,
			long offset,
			long? count,
			CancellationToken cancellation)
		{
			await OnStart();

			if (!count.HasValue)
			{
				var fi = new FileInfo(path);
				count = fi.Length;
			}

			_onFinished = async () =>
			{
				_context.Response.ContentLength64 = count.Value;

				using (var fs = File.OpenRead(path))
				{
					fs.Seek(offset, SeekOrigin.Begin);

					var reader = new LimitedReader(fs, count.Value);
					await reader.CopyToAsync(_context.Response.OutputStream, BufferSize, cancellation);
				}

				_context.Response.OutputStream.Flush();
			};
		}

		#endregion

		#region IHttpBufferingFeature

		void IHttpBufferingFeature.DisableRequestBuffering()
		{
			// ignored, we don't do buffering on requests
		}

		void IHttpBufferingFeature.DisableResponseBuffering()
		{
			_responseStream = new ResponseStream(_context.Response.OutputStream, OnStart);
			_onFinished = () => Task.FromResult(0);
		}

		#endregion

		internal async Task OnStart()
		{
			if (_responseStarted)
				return;

			_responseStarted = true;

			foreach (var pair in Enumerable.Reverse(_onStartingActions))
			{
				await pair.Item1(pair.Item2);
			}
		}

		internal async Task OnCompleted()
		{
			if (_completed)
				return;

			_completed = true;

			foreach (var pair in Enumerable.Reverse(_onCompletedActions))
			{
				await pair.Item1(pair.Item2);
			}

			await _onFinished();
		}
	}
}