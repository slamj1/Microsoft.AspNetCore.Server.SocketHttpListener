using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Server.SocketHttpListener
{
	internal class ResponseStream : Stream
	{
		private readonly Stream _innerStream;
		private readonly Func<Task> _onStart;

		internal ResponseStream(Stream innerStream, Func<Task> onStart)
		{
			_innerStream = innerStream;
			_onStart = onStart;
		}

		public override bool CanRead => _innerStream.CanRead;

		public override bool CanSeek => _innerStream.CanSeek;

		public override bool CanWrite => _innerStream.CanWrite;

		public override long Length => _innerStream.Length;

		public override long Position
		{
			get { return _innerStream.Position; }
			set { _innerStream.Position = value; }
		}

		public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);

		public override void SetLength(long value) => _innerStream.SetLength(value);

		public override int Read(byte[] buffer, int offset, int count) => 
			_innerStream.Read(buffer, offset, count);

		public override void Flush()
		{
			_onStart().GetAwaiter().GetResult();
			_innerStream.Flush();
		}

		public override async Task FlushAsync(CancellationToken cancellationToken)
		{
			await _onStart();
			await _innerStream.FlushAsync(cancellationToken);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			_onStart().GetAwaiter().GetResult();
			_innerStream.Write(buffer, offset, count);
		}

		public override async Task WriteAsync(
			byte[] buffer, 
			int offset, 
			int count, 
			CancellationToken cancellationToken)
		{
			await _onStart();
			await _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
		}
	}
}
