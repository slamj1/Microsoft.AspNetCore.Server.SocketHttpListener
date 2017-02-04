using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace WebApplication.Controllers
{
	public class StreamController : Controller
	{
		public IActionResult Download()
		{
			return new FileCallbackResult("application/octet-stream", async (stream, context) =>
			{
				var buf = new byte[1024];
				var random = new Random();

				for (var i = 0; i < 100 * 100 * 100; i++) // 100 MB
				{
					random.NextBytes(buf);
					await stream.WriteAsync(buf, 0, buf.Length);
				}
			});
		}
	}

	/// <summary>
	/// Represents an <see cref="ActionResult"/> that when executed will
	/// execute a callback to write the file content out as a stream.
	/// </summary>
	public class FileCallbackResult : FileResult
	{
		private readonly Func<Stream, ActionContext, Task> _callback;

		/// <summary>
		/// Creates a new <see cref="FileCallbackResult"/> instance.
		/// </summary>
		/// <param name="contentType">The Content-Type header of the response.</param>
		/// <param name="callback">The stream with the file.</param>
		public FileCallbackResult(string contentType, Func<Stream, ActionContext, Task> callback)
			: this(MediaTypeHeaderValue.Parse(contentType), callback)
		{
		}

		/// <summary>
		/// Creates a new <see cref="FileCallbackResult"/> instance.
		/// </summary>
		/// <param name="contentType">The Content-Type header of the response.</param>
		/// <param name="callback">The stream with the file.</param>
		public FileCallbackResult(MediaTypeHeaderValue contentType, Func<Stream, ActionContext, Task> callback)
			: base(contentType?.ToString())
		{
			if (callback == null)
			{
				throw new ArgumentNullException(nameof(callback));
			}

			_callback = callback;
		}

		/// <inheritdoc />
		public override async Task ExecuteResultAsync(ActionContext context)
		{
			if (context == null)
				throw new ArgumentNullException(nameof(context));

			var bufferingFeature = context.HttpContext.Features.Get<IHttpBufferingFeature>();
			bufferingFeature?.DisableResponseBuffering();

			context.HttpContext.Response.ContentType = ContentType;

			if (!string.IsNullOrEmpty(FileDownloadName))
			{
				var header = new ContentDispositionHeaderValue("attachment");
				header.SetHttpFileName(FileDownloadName);
				context.HttpContext.Response.Headers["Content-Disposition"] = header.ToString();
			}

			Console.WriteLine("Executing FileResult, sending file as {0}", FileDownloadName);

			try
			{
				await _callback(context.HttpContext.Response.Body, context);
			}
			catch (IOException ex) when (ex.InnerException is SocketException)
			{
				var sex = (SocketException) ex.InnerException;
				Console.WriteLine("{0}: {1}, {2}", ex.Message, sex.ErrorCode, sex.SocketErrorCode);
			}
		}
	}
}