using System.Net.Sockets;

namespace WebTyphoon
{
	class AsyncReadData
	{
		public NetworkStream Stream { get; set; }
		public byte[] Buffer { get; set; }
	}
}
