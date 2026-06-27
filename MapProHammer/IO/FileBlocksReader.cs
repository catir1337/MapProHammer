using System;
using System.Collections.Generic;
using System.IO;

namespace MapProHammer.IO
{

    public class FileBlocksReader
    {
        private readonly byte[] _file;
        private readonly List<(int Offset, int Length)> _blocks = new();

        public FileBlocksReader(string filePath) : this(File.ReadAllBytes(filePath)) { }

        public FileBlocksReader(byte[] data)
        {
            _file = data;
            Parse();
        }

        private void Parse()
        {
            var r = new MapBinaryReader(_file);

            int magic = r.ReadInt32();
            if (magic != 112233)
                throw new InvalidDataException($"Неверный magic: {magic} (ожидается 112233)");

            int headerSize  = r.ReadInt32();
            int contentStart = 8 + headerSize;

            int blockCount = r.ReadInt32();
            int offset = contentStart;
            for (int i = 0; i < blockCount; i++)
            {
                int len = r.ReadInt32();
                _blocks.Add((offset, len));
                offset += len;
            }
        }

        public int BlockCount => _blocks.Count;

        public MapBinaryReader GetBlock(int index)
        {
            if (index < 0 || index >= _blocks.Count)
                throw new ArgumentOutOfRangeException(nameof(index),
                    $"Блок {index} вне диапазона [0, {_blocks.Count - 1}]");
            var (off, len) = _blocks[index];
            return new MapBinaryReader(_file, off, off + len);
        }

        public MapBinaryReader GetLastBlock() => GetBlock(_blocks.Count - 1);

        public void ValidateSignature()
        {
            var r = GetBlock(0);
            int sig = r.ReadInt32();
            if (sig != 11223344)
                throw new InvalidDataException($"Неверная сигнатура блока 0: {sig}");
            string name = r.ReadString(100);
            if (name != "MapPro_FileBlocks")
                throw new InvalidDataException($"Неверное имя блока 0: '{name}'");
        }
    }
}
