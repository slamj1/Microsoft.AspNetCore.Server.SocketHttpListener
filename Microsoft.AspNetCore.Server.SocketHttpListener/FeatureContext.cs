using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using SocketHttpListener.Net;
using IPAddress = System.Net.IPAddress;
using HttpListenerContext = SocketHttpListener.Net.HttpListenerContext;

namespace Microsoft.AspNetCore.Server.SocketHttpListener
{
	class FeatureContext :
		IHttpConnectionFeature,
		IHttpRequestFeature,
		IHttpResponseFeature,
		IHttpSendFileFeature
	{
		private readonly HttpListenerContext _context;
		private bool _hasStarted;
		private IHeaderDictionary _requestHeaders;
		private IHeaderDictionary _responseHeaders;

		private List<Tuple<Func<object, Task>, object>> _onStartingActions;
		private List<Tuple<Func<object, Task>, object>> _onCompletedActions;
		private bool _responseStarted;
		private bool _completed;
		private IPAddress _remoteIpAddress;
		private IPAddress _localIpAddress;
		private int _remotePort;
		private int _localPort;
		private readonly Stream _buffer;
		private Stream _responseStream;
		private Stream _requestStream;
		private string _queryString;
		private string _rawTarget;
		private string _path;
		private string _pathBase;
		private string _method;
		private string _scheme;
		private string _protocol;
		private string _connectionId;
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

			_requestHeaders = new HeaderDictionary();
			foreach (var key in context.Request.Headers.AllKeys)
			{
				_requestHeaders.Add(key, new StringValues(context.Request.Headers.GetValues(key)));
			}

			_responseHeaders = new HeaderDictionary();
			foreach (var key in context.Response.Headers.AllKeys)
			{
				_responseHeaders.Add(key, new StringValues(context.Response.Headers.GetValues(key)));
			}

			_method = context.Request.HttpMethod;
			_path = Uri.UnescapeDataString(context.Request.Url.AbsolutePath);
			_pathBase = string.Empty;
			_queryString = context.Request.Url.Query;
			_rawTarget = context.Request.RawUrl;
			_scheme = context.Request.Url.Scheme;
			_protocol = $"HTTP/{context.Request.ProtocolVersion}";

			_connectionId = context.Request.RequestTraceIdentifier.ToString();
			_localIpAddress = context.Request.LocalEndPoint.Address;
			_localPort = context.Request.LocalEndPoint.Port;
			_remoteIpAddress = context.Request.RemoteEndPoint.Address;
			_remotePort = context.Request.RemoteEndPoint.Port;

			_buffer = new MemoryStream();
			_requestStream = context.Request.InputStream;
			_responseStream = new ResponseStream(_buffer, OnStart);

			_onFinished = async () =>
			{
				_context.Response.Headers = MakeWebHeaders();
				_context.Response.ContentLength64 = _buffer.Length;

				_buffer.Seek(0, SeekOrigin.Begin);
				await _buffer.CopyToAsync(_context.Response.OutputStream);

				_context.Response.OutputStream.Flush();
				_context.Response.Close();
			};
		}

		internal IFeatureCollection Features { get; }

		string IHttpConnectionFeature.ConnectionId
		{
			get { return _connectionId; }
			set { _connectionId = value; }
		}

		IPAddress IHttpConnectionFeature.RemoteIpAddress
		{
			get { return _remoteIpAddress; }
			set { _remoteIpAddress = value; }
		}

		IPAddress IHttpConnectionFeature.LocalIpAddress
		{
			get { return _localIpAddress; }
			set { _localIpAddress = value; }
		}

		int IHttpConnectionFeature.RemotePort
		{
			get { return _remotePort; }
			set { _remotePort = value; }
		}

		int IHttpConnectionFeature.LocalPort
		{
			get { return _localPort; }
			set { _localPort = value; }
		}

		/// <summary>
		/// The HTTP-version as defined in RFC 7230. E.g. "HTTP/1.1"
		/// </summary>
		string IHttpRequestFeature.Protocol
		{
			get { return _protocol; }
			set { _protocol = value; }
		}

		string IHttpRequestFeature.Scheme
		{
			get { return _scheme; }
			set { _scheme = value; }
		}

		string IHttpRequestFeature.Method
		{
			get { return _method; }
			set { _method = value; }
		}

		string IHttpRequestFeature.PathBase
		{
			get { return _pathBase; }
			set { _pathBase = value; }
		}

		/// <summary>
		/// The portion of the request path that identifies the requested resource.
		/// The value is un-escaped.
		/// The value may be string.Empty if PathBase contains the full path.
		/// </summary>
		string IHttpRequestFeature.Path
		{
			get { return _path; }
			set { _path = value; }
		}

		/// <summary>
		/// The query portion of the request-target as defined in RFC 7230.
		/// The value may be string.Empty.
		/// If not empty then the leading '?' will be included.
		/// The value is in its original form, without un-escaping.
		/// </summary>
		string IHttpRequestFeature.QueryString
		{
			get { return _queryString; }
			set { _queryString = value; }
		}

		/// <summary>
		/// The request target as it was sent in the HTTP request.
		/// This property contains the raw path and full query,
		/// as well as other request targets such as * for OPTIONS requests
		/// (https://tools.ietf.org/html/rfc7230#section-5.3).
		/// </summary>
		string IHttpRequestFeature.RawTarget
		{
			get { return _rawTarget; }
			set { _rawTarget = value; }
		}

		void IHttpResponseFeature.OnStarting(Func<object, Task> callback, object state)
		{
			if (callback == null)
				throw new ArgumentNullException(nameof(callback));

			if (_onStartingActions == null)
				throw new InvalidOperationException("Cannot register new callbacks, the response has already started.");

			_onStartingActions.Add(new Tuple<Func<object, Task>, object>(callback, state));
		}

		void IHttpResponseFeature.OnCompleted(Func<object, Task> callback, object state)
		{
			if (callback == null)
				throw new ArgumentNullException(nameof(callback));

			if (_onCompletedActions == null)
				throw new InvalidOperationException("Cannot register new callbacks, the response has already completed.");

			_onCompletedActions.Add(new Tuple<Func<object, Task>, object>(callback, state));
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
			set { _responseHeaders = value; }
		}

		Stream IHttpResponseFeature.Body
		{
			get { return _responseStream; }
			set { _responseStream = value; }
		}

		bool IHttpResponseFeature.HasStarted => _hasStarted;

		IHeaderDictionary IHttpRequestFeature.Headers
		{
			get { return _requestHeaders; }
			set { _requestHeaders = value; }
		}

		Stream IHttpRequestFeature.Body
		{
			get { return _requestStream; }
			set { _requestStream = value; }
		}

		async Task IHttpSendFileFeature.SendFileAsync(
			string path, 
			long offset, 
			long? count, 
			CancellationToken cancellation)
		{
			const int bufferSize = 64 * 1024;

			await OnStart();

			if (!count.HasValue)
			{
				var fi = new FileInfo(path);
				count = fi.Length;
			}

			_onFinished = async () =>
			{
				_context.Response.Headers = MakeWebHeaders();
				_context.Response.ContentLength64 = count.Value;

				using (var fs = File.OpenRead(path))
				{
					fs.Seek(offset, SeekOrigin.Begin);

					var reader = new LimitedReader(fs, count.Value);
					await reader.CopyToAsync(_context.Response.OutputStream, bufferSize, cancellation);
				}

				_context.Response.OutputStream.Flush();
				_context.Response.Close();
			};
		}

		WebHeaderCollection MakeWebHeaders()
		{
			var ret = new WebHeaderCollection();
			foreach (var item in _responseHeaders)
			{
				foreach (var value in item.Value)
				{
					ret.Add(item.Key, value);
				}
			}
			return ret;
		}

		internal async Task OnFinish()
		{
			await _onFinished();
		}

		internal async Task OnStart()
		{
			if (_responseStarted)
				return;

			_responseStarted = true;
			_hasStarted = true;
			await NotifyOnStartingAsync();
		}

		private async Task NotifyOnStartingAsync()
		{
			var actions = _onStartingActions;
			_onStartingActions = null;
			if (actions == null)
				return;

			actions.Reverse();
			foreach (var pair in actions)
			{
				await pair.Item1(pair.Item2);
			}
		}

		internal Task OnCompleted()
		{
			if (_completed)
				return Task.FromResult(0);

			_completed = true;
			return NotifyOnCompletedAsync();
		}

		private async Task NotifyOnCompletedAsync()
		{
			var actions = _onCompletedActions;
			_onCompletedActions = null;
			if (actions == null)
				return;

			actions.Reverse();
			foreach (var pair in actions)
			{
				await pair.Item1(pair.Item2);
			}
		}
	}
}
