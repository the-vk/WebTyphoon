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
using System.Text;

namespace WebTyphoon
{
    public enum OpCode : byte
    {
        ContinuationFrame = 0x0,
        TextFrame = 0x1,
        BinaryFrame = 0x2,
        //0x3 - 0x7 - Reserved
        ConnectionClose = 0x8,
        Ping = 0x9,
        Pong = 0xA,
        //0xB - 0xF - Reserved
    }

    public class WebSocketFragment
    {
        private const byte FinBit = 0x80;
        private const byte RSV1Bit = 0x40;
        private const byte RSV2Bit = 0x20;
        private const byte RSV3Bit = 0x10;
        private const byte OpcodeBit = 0x0F;
        private const byte MaskBit = 0x80;
        private const byte PayloadlenBit = 0x7F;

        private const byte MaskLength = 4;

        private readonly byte[] _raw;

        public bool Fin
        {
            get { return GetBit(_raw[0], FinBit) != 0; }
            set { _raw[0] = SetBit(_raw[0], FinBit, value); }
        }

        public bool RSV1
        {
            get { return GetBit(_raw[0], RSV1Bit) != 0; }
            set { _raw[0] = SetBit(_raw[0], RSV1Bit, value); }
        }

        public bool RSV2
        {
            get { return GetBit(_raw[0], RSV2Bit) != 0; }
            set { _raw[0] = SetBit(_raw[0], RSV2Bit, value); }
        }

        public bool RSV3
        {
            get { return GetBit(_raw[0], RSV3Bit) != 0; }
            set { _raw[0] = SetBit(_raw[0], RSV3Bit, value); }
        }

        public OpCode OpCode
        {
            get { return (OpCode)(_raw[0] & OpcodeBit); }
            set { _raw[0] |=(byte)((byte) value & OpcodeBit); }
        }

        public bool Mask
        {
            get { return GetBit(_raw[1], MaskBit) != 0; }
            set { _raw[1] = SetBit(_raw[1], MaskBit, value); }
        }

        public ulong PayloadLength
        {
            get
            {
                ulong result = 0;
                var payloadLen = _raw[1] & PayloadlenBit;
                if (payloadLen <= 125) result = (ulong)payloadLen;
                if(payloadLen == 126)
                {
                    result = (ulong)(_raw[2] << 8 | _raw[3]);
                }
                if(payloadLen == 127)
                {
                    result = (ulong)(_raw[2] << 56 | 
                                     _raw[3] << 48 | 
                                     _raw[4] << 40 | 
                                     _raw[5] << 32 | 
                                     _raw[6] << 24 | 
                                     _raw[7] << 16 | 
                                     _raw[8] << 8 | 
                                     _raw[9]);
                }
                return result;
            }
            set
            {
                if (value <= 125)
                {
                    _raw[1] |= (byte)(value & PayloadlenBit);
                    return;
                }
                if(value <= 65535)
                {
                    _raw[1] |= 126;
                    _raw[2] = (byte)((value & 0xFF00) >> 8);
                    _raw[3] = (byte)(value & 0x00FF);
                    return;
                }
                _raw[1] |= 127;
                _raw[2] = (byte)((value & 0xFF00000000000000) >> 56);
                _raw[3] = (byte)((value & 0x00FF000000000000) >> 48);
                _raw[4] = (byte)((value & 0x0000FF0000000000) >> 40);
                _raw[5] = (byte)((value & 0x000000FF00000000) >> 32);
                _raw[6] = (byte)((value & 0x00000000FF000000) >> 24);
                _raw[7] = (byte)((value & 0x0000000000FF0000) >> 16);
                _raw[8] = (byte)((value & 0x000000000000FF00) >> 8);
                _raw[9] = (byte)((value & 0x00000000000000FF));
            }
        }

        private int MaskOffset
        {
            get
            {
                var payloadLen = _raw[1] & PayloadlenBit;
                if(payloadLen <= 125) return 2;
                if(payloadLen == 126) return 4;
                if(payloadLen == 127) return 10;
                return -1;
            }
        }

        public byte[] MaskKey
        {
            get
            {
                if (!Mask) return null;
                var maskOffset = MaskOffset;
                var mask = new byte[MaskLength];
                Array.Copy(_raw, maskOffset, mask, 0, mask.Length);
                return mask;
            }
            set 
            { 
                var maskOffset = MaskOffset;
                Array.Copy(value, 0, _raw, maskOffset, MaskLength);
            }
        }

        private int PayloadOffset
        {
            get { return MaskOffset + (Mask ? 4 : 0); }
        }

        public byte[] PayloadBinary
        {
            get 
            { 
                var payload = new byte[PayloadLength]; 
                Array.Copy(_raw, PayloadOffset, payload, 0, payload.Length);
                return Mask ? ApplyMask(payload) : payload;
            }
            set
            {
                if(Mask)
                {
                    Array.Copy(ApplyMask(value), 0, _raw, PayloadOffset, value.Length);    
                }
                else
                {
                    Array.Copy(ApplyMask(value), 0, _raw, PayloadOffset, value.Length);
                }
            }
        }

        public string PayloadString
        {
            get
            {
                var payloadBinary = PayloadBinary;
                return Encoding.UTF8.GetString(payloadBinary);
            }
            set
            {
                var payloadBinary = Encoding.UTF8.GetBytes(value);
                PayloadBinary = payloadBinary;
            }
        }

        public WebSocketFragment(byte[] buffer)
        {
            _raw = buffer;
        }

        public WebSocketFragment(bool fin, OpCode opCode, byte[] payload = null, byte[] mask = null,  bool rsv1 = false, bool rsv2 = false, bool rsv3 = false)
        {
            ulong length = 2;
            if(mask != null)
            {
                length += 4;
            }
            if (payload != null)
            {
                length += (ulong) payload.LongLength;
            }

            _raw = new byte[length];

            Fin = fin;
            RSV1 = rsv1;
            RSV2 = rsv2;
            RSV3 = rsv3;
            OpCode = opCode;
            if(mask != null)
            {
                Mask = true;
                MaskKey = mask;
            }

            if (payload != null)
            {
                PayloadLength = (ulong) payload.LongLength;
                PayloadBinary = payload;
            }
        }

        public WebSocketFragment(bool fin, OpCode opCode, string payloadString = null, byte[] mask = null, bool rsv1 = false, bool rsv2 = false, bool rsv3 = false):
            this(fin, opCode, payloadString != null ? Encoding.UTF8.GetBytes(payloadString) : null, mask, rsv1, rsv2, rsv3)
        {
        }

        internal byte[] GetBuffer()
        {
            return _raw;
        }

        private byte[] ApplyMask(byte[] data)
        {
            var result = new byte[data.Length];
            var mask = MaskKey;
            for(var i = 0; i < result.Length; ++i)
            {
                var maskOctet = mask[i & 0x3];
                result[i] = (byte)(data[i] ^ maskOctet);
            }

            return result;
        }

        private static byte GetBit(byte b, byte mask)
        {
            return (byte)(b & mask);
        }

        private static byte SetBit(byte b, byte mask, bool value)
        {
            if (value)
                return (byte)(b | (byte)(0xFF & mask));
            return (byte) (b & (0xFF & ~(mask)));
        }
    }
}
