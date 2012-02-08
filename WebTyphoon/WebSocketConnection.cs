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


        internal bool HasWork
        {
            get { return _stream.DataAvailable || _sendFragmentQueue.Count != 0; }
        }

        public WebSocketConnection(NetworkStream stream)
        {
            Status = WebSocketConnectionStatus.Open;

            _stream = stream;
            _dataBuffer = new MemoryStream();
            _fragmentsList = new List<WebSocketFragment>();
            _sendFragmentQueue = new Queue<WebSocketFragment>();
        }

        public void SendFragment(WebSocketFragment fragment)
        {
            if(Status != WebSocketConnectionStatus.Open) throw new InvalidOperationException("Connection is not open");

            WriteData(fragment);
        }

        internal void StartRead()
        {
            var state = new AsyncReadData {Stream = _stream, Buffer = new byte[InputBufferLength]};

            _stream.BeginRead(state.Buffer, 0, InputBufferLength, AsyncReadHandler, state);
        }

        private long fullLength = 0;

        private void AsyncReadHandler(IAsyncResult ar)
        {
            var s = (AsyncReadData) ar.AsyncState;
            try
            {
                var readBytes = s.Stream.EndRead(ar);
                fullLength += readBytes;
                ReadData(s.Buffer, readBytes);

                StartRead();
            }
            catch (Exception)
            {
                
            }
            
        }

        private void ReadData(byte[] buffer, int readBytes)
        {
            _dataBuffer.Write(buffer, 0, readBytes);

            CheckForFrame();
        }

        private bool CheckForFrame()
        {
            if (_dataBuffer.Length == 0) return false;

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
                        return false;
                    }
                }

                if ((dataLength - fragmentStart) < _currentFragmentLength)
                {
                    _dataBuffer = new MemoryStream();
                    _dataBuffer.Write(buffer, fragmentStart, (int)(dataLength - fragmentStart));
                    return false;
                }

                var fragmentBuffer = new byte[_currentFragmentLength];
                Array.Copy(buffer, fragmentStart, fragmentBuffer, 0, _currentFragmentLength);

                var fragment = new WebSocketFragment(fragmentBuffer);
                OnWebSocketFragmentRecieved(this, new WebSocketFragmentRecievedEventArgs(fragment));

                fragmentStart += _currentFragmentLength;

                _currentFragmentLength = 0;
            }

            return false;
        }

        private void WriteData(WebSocketFragment fragment)
        {
            var fragmentData = fragment.GetBuffer();

            _stream.BeginWrite(fragmentData, 0, fragmentData.Length, AsynWriteHandler, _stream);
        }

        private void AsynWriteHandler(IAsyncResult ar)
        {
            var str = (NetworkStream)ar.AsyncState;
            str.EndWrite(ar);
        }

        private void StopWriterThread()
        {
            Status = WebSocketConnectionStatus.Closed;
        }

        private void NotifyWebSocketFragmentRecieved(object data)
        {
            var e = (WebSocketFragmentRecievedEventArgs)data;
            if (WebSocketFragmentRecieved != null)
            {
                WebSocketFragmentRecieved(this, e);
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
                    FailConnection();
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
                            FailConnection("No starting fragment");
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

        protected void FailConnection()
        {
            CloseConnection("Websocket failed");
        }

        protected void FailConnection(string reason)
        {
            CloseConnection(reason);
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
            StopWriterThread();
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
