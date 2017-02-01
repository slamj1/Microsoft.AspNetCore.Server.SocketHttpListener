using System.Security.Cryptography.X509Certificates;

namespace Microsoft.AspNetCore.Server.SocketHttpListener
{
	public class SocketHttpListenerOptions
	{
		public X509Certificate2 Certificate { get; set; }
	}
}
