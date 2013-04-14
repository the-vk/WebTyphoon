/*
Copyright (C) 2012, 2013 Andrew 'the vk' Maraev

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Net;
using System.Net.Sockets;

namespace WebTyphoon.Samples.ConsoleEchoServer
{
    class Program
    {
        static void Main()
        {
            var webTyphoon = new WebTyphoon();

            webTyphoon.AddUriBinding(new[] { "/" }, null, null, null, WebTyphoonConnectionAccepted);

            var ipEndpoint = new IPEndPoint(IPAddress.Any, 9000);
            var tcpListener = new TcpListener(ipEndpoint);
            tcpListener.Start();

	        tcpListener.BeginAcceptTcpClient(BeginAcceptClientCallback, new Tuple<TcpListener, WebTyphoon>(tcpListener, webTyphoon));

            Console.WriteLine("Press any key to exit...");
	        Console.ReadKey(true);

			tcpListener.Stop();
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
	        var connection = (WebSocketConnection) sender;
			Process(connection, e.Fragment);
        }

		static void ConnectionClosedHandler(object sender, WebSocketConnectionStateChangeEventArgs e)
		{
			e.Connection.WebSocketFragmentRecieved -= ConnectionWebSocketFragmentRecieved;
		}
    }
}
