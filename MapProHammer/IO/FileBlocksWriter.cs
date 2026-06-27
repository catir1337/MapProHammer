// Copyright (c) 2026 Catir1337
// SPDX-License-Identifier: GPL-3.0-only
using System.Collections.Generic;

namespace MapProHammer.IO
{

    public class FileBlocksWriter
    {
        private readonly List<byte[]> _blocks = new();

        public FileBlocksWriter()
        {

            var sig = new MapBinaryWriter();
            sig.WriteInt32(11223344);
            sig.WriteString("MapPro_FileBlocks");
            _blocks.Add(sig.ToArray());
        }

        public int AddBlock(byte[] data)
        {
            _blocks.Add(data);
            return _blocks.Count - 1;
        }

        public int AddBlock(MapBinaryWriter w) => AddBlock(w.ToArray());

        public int BlockCount => _blocks.Count;

        public byte[] Build()
        {

            var header = new MapBinaryWriter();
            header.WriteInt32(_blocks.Count);
            foreach (var b in _blocks)
                header.WriteInt32(b.Length);

            byte[] headerBytes = header.ToArray();

            var file = new MapBinaryWriter();
            file.WriteInt32(112233);
            file.WriteInt32(headerBytes.Length);
            file.WriteBytes(headerBytes);
            foreach (var b in _blocks)
                file.WriteBytes(b);

            return file.ToArray();
        }
    }
}
