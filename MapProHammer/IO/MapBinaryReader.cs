using System;
using System.Numerics;
using System.Text;

namespace MapProHammer.IO
{

    public class MapBinaryReader
    {
        private readonly byte[] _data;
        private int _pos;
        private readonly int _start;
        private readonly int _end;

        public MapBinaryReader(byte[] data, int start, int end)
        {
            _data  = data;
            _start = start;
            _pos   = start;
            _end   = end;
        }

        public MapBinaryReader(byte[] data) : this(data, 0, data.Length) { }

        public int Position  => _pos - _start;
        public int TotalSize => _end - _start;
        public int Available => _end - _pos;
        public bool IsEnd    => _pos >= _end;

        public int    ReadInt32()  { var v = BitConverter.ToInt32 (_data, _pos); _pos += 4; return v; }
        public uint   ReadUInt32() { var v = BitConverter.ToUInt32(_data, _pos); _pos += 4; return v; }
        public ushort ReadUInt16() { var v = BitConverter.ToUInt16(_data, _pos); _pos += 2; return v; }
        public float  ReadFloat()  { var v = BitConverter.ToSingle(_data, _pos); _pos += 4; return v; }
        public byte   ReadByte()   => _data[_pos++];
        public bool   ReadBool()   => _data[_pos++] != 0;

        public float ReadFloat01Byte() => _data[_pos++] / 255f;

        public string ReadString(int maxLen = 0)
        {
            int len = ReadInt32();
            if (len <= 0) return string.Empty;
            string s = Encoding.UTF8.GetString(_data, _pos, len);
            _pos += len;
            return s;
        }

        public Vector3 ReadVec3()
        {
            float x = ReadFloat(), y = ReadFloat(), z = ReadFloat();
            return new Vector3(x, y, z);
        }

        public void ReadVec3(ref Vector3 v) => v = ReadVec3();

        public Vector2 ReadVec2()
        {
            float x = ReadFloat(), y = ReadFloat();
            return new Vector2(x, y);
        }

        public byte[] ReadBytes(int count)
        {
            var buf = new byte[count];
            Array.Copy(_data, _pos, buf, 0, count);
            _pos += count;
            return buf;
        }

        public byte[] ReadRemainingBytes() => ReadBytes(_end - _pos);

        public void Skip(int bytes)           => _pos += bytes;
        public void SetPosition(int relPos)   => _pos = _start + relPos;
    }
}
