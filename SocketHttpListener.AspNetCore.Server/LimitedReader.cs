using System;
using System.IO;

namespace SocketHttpListener.AspNetCore.Server
{
	internal class LimitedReader : Stream
	{
		private readonly Stream _chain;
		private long _remain;

		public LimitedReader(Stream chain, long max)
		{
			_chain = chain;
			_remain = max;
		}

		public override void Flush()
		{
			_chain.Flush();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			return _chain.Seek(offset, origin);
		}

		public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (_remain <= 0)
				return 0;

			if (count > _remain)
				count = (int)_remain;

			var read = _chain.Read(buffer, offset, count);
			_remain -= read;
			return read;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}

		public override bool CanRead { get; } = true;
		public override bool CanSeek { get; } = true;
		public override bool CanWrite { get; } = false;
		public override long Length => _chain.Length;
		public override long Position { get; set; }
	}
}