using System;

namespace WebTyphoon
{
    public class WebSocketConnectionStateChangeEventArgs : EventArgs
    {
        public WebSocketConnection Connection { get; set; }
    }
}
