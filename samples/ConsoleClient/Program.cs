/*
Copyright (C) 2013 Andrew 'the vk' Maraev

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

namespace ConsoleClient
{
	class Program
	{
		static void Main(string[] args)
		{
			var tcpClient = new TcpClient();
			tcpClient.Connect(IPAddress.Loopback, 9000);

			var webTyphoon = new WebTyphoon.WebTyphoon();

			var connection = webTyphoon.ConnectAsClient(tcpClient.GetStream(), new Uri("ws://localhost:9000/"), null, null);

			connection.TextMessageRecieved += (s, e) => Console.WriteLine(e.Text);

			for (var i = 0; i < 1024; ++i)
			{
				connection.SendText(String.Format("Message #{0}", i));
			}

			Console.WriteLine("Press key to exit...");
			Console.ReadKey(true);
		}
	}
}
