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
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace WebTyphoon
{
	class WebSocketClientHandshaker
	{
		public Task<WebSocketConnection> Hanshake(NetworkStream stream, Uri uri, string protocol, string origin)
		{
			var result = new Task<WebSocketConnection>(() =>
			{
				var keyBytes = new byte[16];
				var random = new Random();
				random.NextBytes(keyBytes);

				var key = Convert.ToBase64String(keyBytes);

				var requestSb = new StringBuilder();
				requestSb.AppendFormat("GET {0} HTTP/1.1\n", uri.PathAndQuery);
				requestSb.AppendFormat("Host: {0}\n", uri.Host);
				requestSb.AppendLine("Upgrade: websocket");
				requestSb.AppendLine("Connection: Upgrade");
				requestSb.AppendFormat("Sec-WebSocket-Key: {0}\n", key);
				if (!String.IsNullOrEmpty(origin)) requestSb.AppendFormat("Origin: {0}\n", origin);
				if (!String.IsNullOrEmpty(protocol)) requestSb.AppendFormat("Sec-WebSocket-Protocol: {0}\n", protocol);
				requestSb.AppendLine("Sec-WebSocket-Version: 13");
				requestSb.AppendLine();
				var request = requestSb.ToString();

				var reader = new StreamReader(stream);
				var writer = new StreamWriter(stream);

				writer.Write(request);
				writer.Flush();

				var responseLines = new List<string>();

				try
				{
					string str;
					while (!String.IsNullOrEmpty(str = reader.ReadLine()))
					{
						responseLines.Add(str);
					}
				}
				catch (IOException)
				{
					return null;
				}

				var message = new HttpResponse(responseLines);

				if (message.ResponseCode != "101") return null;

				var response = message.Headers["Sec-WebSocket-Accept"];
				var expectedResponse = WebSocketHandshaker.GetSecWebSocketKeyResponse(key);
				if (response != expectedResponse) return null;

				var connection = new WebSocketConnection(stream);
				connection.StartRead();

				return connection;
			});
			result.Start();
			return result;
		}
	}
}
