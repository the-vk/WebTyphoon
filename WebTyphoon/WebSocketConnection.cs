/*
Copyright (C) 2012 Andrew 'the vk' Maraev

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
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace WebTyphoon
{
	public enum WebSocketConnectionStatus
	{
		Open,
		Closing,
		Closed
	}

	public class WebSocketConnection
	{
		private readonly NetworkStream _stream;

		private int _currentFragmentLength;
		private MemoryStream _dataBuffer;
		private readonly List<WebSocketFragment> _fragmentsList;
		private readonly Queue<WebSocketFragment> _sendFragmentQueue;

		private const int InputBufferLength = 102400;

		public WebSocketConnectionStatus Status { get; set; }

		public string Uri { get; internal set; }
		public IEnumerable<string> Protocols { get; internal set; }
		public string Origin { get; internal set; }

		public WebSocketConnection(NetworkStream stream)
		{
			Status = WebSocketConnectionStatus.Open;

			_stream = stream;
			_dataBuffer = new MemoryStream();
			_fragmentsList = new List<WebSocketFragment>();
			_sendFragmentQueue = new Queue<WebSocketFragment>();
		}

		public void SendText(string message)
		{
			var fragment = new WebSocketFragment(true, OpCode.TextFrame, message);
			SendFragment(fragment);
		}

		public void SendFragment(WebSocketFragment fragment)
		{
			if(Status != WebSocketConnectionStatus.Open) throw new InvalidOperationException("Connection is not open");

			WriteData(fragment);
		}

		internal void StartRead()
		{
			var state = new AsyncReadData {Stream = _stream, Buffer = new byte[InputBufferLength]};

			try
			{
				_stream.BeginRead(state.Buffer, 0, InputBufferLength, AsyncReadHandler, state);
			}
			catch (Exception ex)
			{
				Debug.WriteLine("Error while starting read fron network stream: {0}", ex);
				FailConnection(false);
			}
		}

		private void AsyncReadHandler(IAsyncResult ar)
		{
			var s = (AsyncReadData) ar.AsyncState;
			try
			{
				var readBytes = s.Stream.EndRead(ar);

				if (readBytes == 0)
				{
					CloseNetworkStream();
					return;
				}

				ReadData(s.Buffer, readBytes);

				StartRead();
			}
			catch (Exception ex)
			{
				Debug.WriteLine("Error during read from socket: {0}", ex);
				FailConnection("Read error", false);
			}
			
		}

		private void ReadData(byte[] buffer, int readBytes)
		{
			_dataBuffer.Write(buffer, 0, readBytes);

			CheckForFrame();
		}

		private void CheckForFrame()
		{
			if (_dataBuffer.Length == 0) return;

			byte[] buffer = _dataBuffer.GetBuffer();
			long dataLength = _dataBuffer.Length;
			var fragmentStart = 0;
			while (fragmentStart < dataLength)
			{
				if (_currentFragmentLength == 0)
				{
					if (dataLength >= 2)
					{
						_currentFragmentLength = 2;

						var payloadLength = buffer[fragmentStart + 1] & 0x7F;
						if (payloadLength <= 125)
						{
							_currentFragmentLength += payloadLength;
						}
						if (payloadLength == 126 && dataLength >= 4)
						{
							_currentFragmentLength += buffer[fragmentStart + 2] << 8 | buffer[fragmentStart + 3];
							_currentFragmentLength += 2;
						}
						if (payloadLength == 127 && dataLength >= 10)
						{
							_currentFragmentLength +=
								(buffer[fragmentStart + 2] << 56 |
								 buffer[fragmentStart + 3] << 48 |
								 buffer[fragmentStart + 4] << 40 |
								 buffer[fragmentStart + 5] << 32 |
								 buffer[fragmentStart + 6] << 24 |
								 buffer[fragmentStart + 7] << 16 |
								 buffer[fragmentStart + 8] << 8 |
								 buffer[fragmentStart + 9]);
							_currentFragmentLength += 8;
						}

						if ((buffer[fragmentStart + 1] & 0x80) != 0) _currentFragmentLength += 4;
					}
					else
					{
						return;
					}
				}

				if ((dataLength - fragmentStart) < _currentFragmentLength)
				{
					_dataBuffer = new MemoryStream();
					_dataBuffer.Write(buffer, fragmentStart, (int)(dataLength - fragmentStart));
					return;
				}

				var fragmentBuffer = new byte[_currentFragmentLength];
				Array.Copy(buffer, fragmentStart, fragmentBuffer, 0, _currentFragmentLength);

				var fragment = new WebSocketFragment(fragmentBuffer);
				OnWebSocketFragmentRecieved(this, new WebSocketFragmentRecievedEventArgs(fragment));

				fragmentStart += _currentFragmentLength;

				if (fragmentStart == dataLength)
				{
					_dataBuffer = new MemoryStream();
					_currentFragmentLength = 0;
					return;
				}

				_currentFragmentLength = 0;
			}
		}

		private void WriteData(WebSocketFragment fragment)
		{
			var fragmentData = fragment.GetBuffer();

			try
			{
				_stream.BeginWrite(fragmentData, 0, fragmentData.Length, AsyncWriteHandler, _stream);
			}
			catch (Exception ex)
			{
				Debug.WriteLine("Error while starting write to network stream: {0}", ex);
			}
		}

		private void AsyncWriteHandler(IAsyncResult ar)
		{
			try
			{
				var str = (NetworkStream)ar.AsyncState;
				str.EndWrite(ar);
			}
			catch (Exception ex)
			{
				Debug.WriteLine("Error during write to socket: {0}", ex);
				FailConnection(false);
			}
			
		}

		public event EventHandler<WebSocketMessageRecievedEventArgs> TextMessageRecieved;
		protected void OnTextMessageRecieved(object sender, WebSocketMessageRecievedEventArgs e)
		{
			if (TextMessageRecieved != null)
			{
				TextMessageRecieved(sender, e);
			}
		}

		public event EventHandler<WebSocketMessageRecievedEventArgs> BinaryMessageRecieved;
		protected void OnBinaryMessageRecieved(object sender, WebSocketMessageRecievedEventArgs e)
		{
			if (BinaryMessageRecieved != null)
			{
				BinaryMessageRecieved(sender, e);
			}
		}

		private void NotifyWebSocketFragmentRecieved(object data)
		{
			var e = (WebSocketFragmentRecievedEventArgs)data;
			if (WebSocketFragmentRecieved != null)
			{
				WebSocketFragmentRecieved(this, e);
			}

			if (e.Fragment.Fin)
			{
				var mre = new WebSocketMessageRecievedEventArgs();

				switch (e.Fragment.OpCode)
				{
					case OpCode.TextFrame:
						mre.Text = e.Fragment.PayloadString;
						OnTextMessageRecieved(this, mre);
						break;
					case OpCode.BinaryFrame:
						mre.Binary = e.Fragment.PayloadBinary;
						OnBinaryMessageRecieved(this, mre);
						break;
					default:
						return;
				}
			}
		}

		public event EventHandler<WebSocketFragmentRecievedEventArgs> WebSocketFragmentRecieved;
		protected void OnWebSocketFragmentRecieved(object sender, WebSocketFragmentRecievedEventArgs e)
		{
			ThreadPool.QueueUserWorkItem(NotifyWebSocketFragmentRecieved, e);

			if(!e.Fragment.Fin)
			{
				if (_fragmentsList.Count == 0 && e.Fragment.OpCode == OpCode.ContinuationFrame)
				{
					FailConnection(true);
					return;
				}
				_fragmentsList.Add(e.Fragment);
			}
			else
			{
				switch (e.Fragment.OpCode)
				{
					case OpCode.Ping:
						SendPongFrame(e.Fragment);
						break;
					case OpCode.ConnectionClose:
						CloseConnection(e.Fragment);
						break;
					case OpCode.ContinuationFrame:
						if(_fragmentsList.Count == 0)
						{
							FailConnection("No starting fragment", true);
							return;
						}
						var concatFragment = ConcatFragments();
						OnWebSocketFragmentRecieved(sender, new WebSocketFragmentRecievedEventArgs(concatFragment));
						break;
				}
			}
		}

		protected WebSocketFragment ConcatFragments()
		{
			var data = new MemoryStream();
			foreach (var f in _fragmentsList)
			{
				var payload = f.PayloadBinary;
				data.Write(payload, 0, payload.Length);
			}
			var firstFragment = _fragmentsList[0];
			var fragment = new WebSocketFragment(true, firstFragment.OpCode, data.GetBuffer(), null, firstFragment.RSV1,
												 firstFragment.RSV2, firstFragment.RSV3);

			_fragmentsList.Clear();
			return fragment;
		}

		protected void FailConnection(bool sendCloseFragment)
		{
			this.FailConnection("Websocket failed", sendCloseFragment);
		}

		protected void FailConnection(string reason, bool sendCloseFragment)
		{
			var e = new WebSocketConnectionFailedEventArgs()
			{
				Reason = reason
			};

			this.OnFailed(this, e);

			if (sendCloseFragment)
			{
				this.CloseConnection(reason);
			}			
		}

		protected void SendPongFrame(WebSocketFragment fragment)
		{
			var pongFragment = new WebSocketFragment(true, OpCode.Pong, fragment.PayloadString);
			SendFragment(pongFragment);
		}

		public void CloseConnection(string reason)
		{
			var fragment = new WebSocketFragment(true, OpCode.ConnectionClose, reason);
			SendFragment(fragment);

			Status = WebSocketConnectionStatus.Closing;
			OnClosing(this, new WebSocketConnectionStateChangeEventArgs {Connection = this});
		}

		protected void CloseConnection(WebSocketFragment fragment)
		{
			SendFragment(fragment);

			SendQueueEmpty += (s, e) => CloseNetworkStream();
		}

		private void CloseNetworkStream()
		{
			_stream.Close();
			Status = WebSocketConnectionStatus.Closed;
			OnClosed(this, new WebSocketConnectionStateChangeEventArgs { Connection = this });
		}

		public event EventHandler<WebSocketConnectionFailedEventArgs> Failed;
		protected void OnFailed(object sender, WebSocketConnectionFailedEventArgs e)
		{
			if (this.Failed != null)
			{
				this.Failed(sender, e);
			}
		}

		public event EventHandler<WebSocketConnectionStateChangeEventArgs> Closing;
		protected void OnClosing(object sender, WebSocketConnectionStateChangeEventArgs e)
		{
			if(Closing != null)
			{
				Closing(sender, e);
			}
		}

		public event EventHandler<WebSocketConnectionStateChangeEventArgs> Closed;
		protected void OnClosed(object sender, WebSocketConnectionStateChangeEventArgs e)
		{
			if(Closed != null)
			{
				Closed(sender, e);
			}
		}

		public event EventHandler<EventArgs> SendQueueEmpty;
		public void OnSendQueueEmpty(object sender, EventArgs e)
		{
			if(SendQueueEmpty != null)
			{
				SendQueueEmpty(sender, e);
			}
		}
	}
}
