﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetworkSocket.WebSocket
{
    /// <summary>
    /// 表示WebSocket请求帧
    /// </summary>
    public class FrameRequest
    {
        /// <summary>
        /// 获取是否已完成
        /// </summary>
        public virtual bool Fin { get; private set; }

        /// <summary>
        /// 获取保存位
        /// </summary>
        public virtual ByteBits Rsv { get; private set; }

        /// <summary>
        /// 获取帧类型
        /// </summary>
        public virtual FrameCodes Frame { get; private set; }

        /// <summary>
        /// 获取是否有掩码
        /// </summary>
        public virtual bool Mask { get; private set; }

        /// <summary>
        /// 获取内容长度
        /// </summary>
        public virtual int ContentLength { get; private set; }

        /// <summary>
        /// 获取掩码
        /// </summary>
        public virtual byte[] MaskingKey { get; private set; }

        /// <summary>
        /// 获取请求帧的内容
        /// </summary>
        public virtual byte[] Content { get; private set; }


        /// <summary>
        /// 解析请求的数据
        /// 返回请求数据包
        /// </summary>
        /// <param name="stream">所有收到的数据</param>  
        /// <param name="requiredMask">是否要求必须Mask</param>
        /// <exception cref="NotSupportedException"></exception>
        /// <returns></returns>
        public unsafe static FrameRequest Parse(IStreamReader stream, bool requiredMask = true)
        {
            if (stream.Length < 2)
            {
                return null;
            }

            ByteBits byte0 = stream[0];
            var fin = byte0[0];
            var frameCode = (FrameCodes)(byte)byte0.Take(4, 4);

            if (fin == false || frameCode == FrameCodes.Continuation)
            {
                return null;
            }

            var rsv = byte0.Take(1, 3);
            ByteBits byte1 = stream[1];
            var mask = byte1[0];

            if (requiredMask && mask == false)
            {
                throw new NotSupportedException("mask is required");
            }

            if (Enum.IsDefined(typeof(FrameCodes), frameCode) == false || rsv != 0)
            {
                throw new NotSupportedException();
            }

            var contentSize = 0;
            var contentLength = (int)byte1.Take(1, 7);
            stream.Position = 2;

            if (contentLength == 127)
            {
                contentSize = 8;
                contentLength = (int)stream.ReadUInt64();
            }
            else if (contentLength == 126)
            {
                contentSize = 2;
                contentLength = (int)stream.ReadUInt16();
            }

            var maskSize = mask ? 4 : 0;
            var packetLength = 2 + maskSize + contentSize + contentLength;
            if (stream.Length < packetLength)
            {
                return null;
            }

            var maskingKey = mask ? stream.ReadArray(4) : null;
            var content = stream.ReadArray(contentLength);
            stream.Clear(packetLength);

            if (mask && contentLength > 0)
            {
                fixed (byte* pcontent = &content[0], pmask = &maskingKey[0])
                {
                    for (var i = 0; i < contentLength; i++)
                    {
                        *(pcontent + i) = (byte)(*(pcontent + i) ^ *(pmask + i % 4));
                    }
                }
            }

            return new FrameRequest
            {
                Fin = fin,
                Rsv = rsv,
                Mask = mask,
                Frame = frameCode,
                ContentLength = contentLength,
                MaskingKey = maskingKey,
                Content = content
            };
        }
    }
}
