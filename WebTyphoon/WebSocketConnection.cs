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
        internal bool Processing { get; set; }

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
            lock(_sendFragmentQueue)
            {
                _sendFragmentQueue.Enqueue(fragment);
            }
        }

        private WebSocketFragment GetFragmentFromSendQueue()
        {
            lock(_sendFragmentQueue)
            {
                return _sendFragmentQueue.Dequeue();
            }
        }

        internal void Process()
        {
            ReadData();
            WriteData();
        }

        private void ReadData()
        {
            var buffer = new byte[10240];
            var dataAvailable = _stream.DataAvailable;
            if (dataAvailable)
            {
                long readByte = _stream.Read(buffer, 0, buffer.Length);
                _dataBuffer.Write(buffer, 0, (int)readByte);

                while (CheckForFrame())
                {
                }
            }
        }

        private bool CheckForFrame()
        {
            if (_dataBuffer.Length == 0) return false;

            byte[] buffer = _dataBuffer.GetBuffer();
            long dataLength = _dataBuffer.Length;
            if (_currentFragmentLength == 0)
            {
                if (dataLength >= 2)
                {
                    _currentFragmentLength = 2;

                    var payloadLength = buffer[1] & 0x7F;
                    if (payloadLength <= 125)
                    {
                        _currentFragmentLength += payloadLength;
                    }
                    if (payloadLength == 126 && dataLength >= 4)
                    {
                        _currentFragmentLength += buffer[2] << 8 | buffer[3];
                        _currentFragmentLength += 2;
                    }
                    if (payloadLength == 127 && dataLength >= 10)
                    {
                        _currentFragmentLength +=
                            (buffer[2] << 56 |
                             buffer[3] << 48 |
                             buffer[4] << 40 |
                             buffer[5] << 32 |
                             buffer[6] << 24 |
                             buffer[7] << 16 |
                             buffer[8] << 8 |
                             buffer[9]);
                        _currentFragmentLength += 8;
                    }

                    if ((buffer[1] & 0x80) != 0) _currentFragmentLength += 4;
                }
                else
                {
                    return false;
                }
            }

            if (dataLength < _currentFragmentLength)
            {
                return false;
            }

            buffer = _dataBuffer.GetBuffer();
            var fragmentBuffer = new byte[_currentFragmentLength];
            Array.Copy(buffer, 0, fragmentBuffer, 0, _currentFragmentLength);

            var fragment = new WebSocketFragment(fragmentBuffer);
            OnWebSocketFragmentRecieved(this, new WebSocketFragmentRecievedEventArgs(fragment));

            var bufferLength = _dataBuffer.Length;
            _dataBuffer = new MemoryStream();
            if (bufferLength > _currentFragmentLength)
            {
                _dataBuffer.Write(buffer, _currentFragmentLength, (int)(bufferLength - _currentFragmentLength));
            }
            _currentFragmentLength = 0;

            return true;
        }

        private void WriteData()
        {
            lock (_sendFragmentQueue)
            {
                while (_sendFragmentQueue.Count != 0)
                {
                    var fragment = GetFragmentFromSendQueue();
                    var fragmentData = fragment.GetBuffer();
                    _stream.Write(fragmentData, 0, fragmentData.Length);
                }
            }
            OnSendQueueEmpty(this, new EventArgs());
        }

        public event EventHandler<WebSocketFragmentRecievedEventArgs> WebSocketFragmentRecieved;
        protected void OnWebSocketFragmentRecieved(object sender, WebSocketFragmentRecievedEventArgs e)
        {
            if (WebSocketFragmentRecieved != null)
            {
                WebSocketFragmentRecieved(sender, e);
            }

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

            Status = WebSocketConnectionStatus.Closed;
            OnClosed(this, new WebSocketConnectionStateChangeEventArgs { Connection = this });
            SendQueueEmpty += (s, e) => CloseNetworkStream();
        }

        private void CloseNetworkStream()
        {
            _stream.Close();
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
