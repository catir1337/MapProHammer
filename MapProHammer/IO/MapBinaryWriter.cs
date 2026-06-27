using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace MapProHammer.IO
{

    public class MapBinaryWriter
    {
        private readonly List<byte> _buf = new();

        public int Length => _buf.Count;

        public byte[] ToArray() => _buf.ToArray();

        public void WriteInt32(int v)     => _buf.AddRange(BitConverter.GetBytes(v));
        public void WriteUInt16(ushort v) => _buf.AddRange(BitConverter.GetBytes(v));
        public void WriteFloat(float v)   => _buf.AddRange(BitConverter.GetBytes(v));
        public void WriteByte(byte v)     => _buf.Add(v);
        public void WriteBool(bool v)     => _buf.Add(v ? (byte)1 : (byte)0);

        public void WriteFloat01Byte(float v) =>
            _buf.Add((byte)Math.Clamp((int)(v * 255f), 0, 255));

        public void WriteString(string? s)
        {
            if (string.IsNullOrEmpty(s)) { WriteInt32(0); return; }
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            WriteInt32(bytes.Length);
            _buf.AddRange(bytes);
        }

        public void WriteVec3(Vector3 v) { WriteFloat(v.X); WriteFloat(v.Y); WriteFloat(v.Z); }
        public void WriteVec2(Vector2 v) { WriteFloat(v.X); WriteFloat(v.Y); }

        public void WriteBytes(byte[] bytes) => _buf.AddRange(bytes);
        public void WriteWriter(MapBinaryWriter other) => _buf.AddRange(other._buf);
    }
}
