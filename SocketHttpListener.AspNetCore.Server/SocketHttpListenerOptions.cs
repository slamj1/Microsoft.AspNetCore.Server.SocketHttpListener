using System.Security.Cryptography.X509Certificates;

namespace SocketHttpListener.AspNetCore.Server
{
	public class SocketHttpListenerOptions
	{
		public X509Certificate2 Certificate { get; set; }
	}
}
