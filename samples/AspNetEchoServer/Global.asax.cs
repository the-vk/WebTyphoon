using System;
using System.Net;
using System.Net.Sockets;

namespace WebTyphoon.Samples.AspNetEchoServer
{
	public class Global : System.Web.HttpApplication
	{
		private WebTyphoon _webTyphoon;
		private TcpListener _tcpListener;

		protected void Application_Start(object sender, EventArgs e)
		{
			_webTyphoon = new WebTyphoon();

			_webTyphoon.AddUriBinding(new[] { "/" }, null, null, null, WebTyphoonConnectionAccepted);

			var ipEndpoint = new IPEndPoint(IPAddress.Any, 9000);
			_tcpListener = new TcpListener(ipEndpoint);
			_tcpListener.Start();

			_tcpListener.BeginAcceptTcpClient(BeginAcceptClientCallback, new Tuple<TcpListener, WebTyphoon>(_tcpListener, _webTyphoon));
		}

		protected void Application_End(object sender, EventArgs e)
		{
			_tcpListener.Stop();
		}

		static void BeginAcceptClientCallback(IAsyncResult ar)
		{
			var state = (Tuple<TcpListener, WebTyphoon>)ar.AsyncState;
			var tcpListener = state.Item1;
			var webTyphoon = state.Item2;

			var client = tcpListener.EndAcceptTcpClient(ar);

			webTyphoon.AcceptConnection(client.GetStream());

			tcpListener.BeginAcceptTcpClient(BeginAcceptClientCallback, ar.AsyncState);
		}

		static void Process(WebSocketConnection connection, WebSocketFragment fragment)
		{
			if (fragment.OpCode == OpCode.BinaryFrame || fragment.OpCode == OpCode.TextFrame)
				connection.SendText(fragment.PayloadString);
		}

		static void WebTyphoonConnectionAccepted(object sender, WebSocketConnectionEventArgs e)
		{
			var connection = e.Connection;
			connection.WebSocketFragmentRecieved += ConnectionWebSocketFragmentRecieved;
			connection.Closed += ConnectionClosedHandler;
		}

		static void ConnectionWebSocketFragmentRecieved(object sender, WebSocketFragmentRecievedEventArgs e)
		{
			var connection = (WebSocketConnection)sender;
			Process(connection, e.Fragment);
		}

		static void ConnectionClosedHandler(object sender, WebSocketConnectionStateChangeEventArgs e)
		{
			e.Connection.WebSocketFragmentRecieved -= ConnectionWebSocketFragmentRecieved;
		}
	}
}