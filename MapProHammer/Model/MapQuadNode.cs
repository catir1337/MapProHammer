// Copyright (c) 2026 Catir1337
// SPDX-License-Identifier: GPL-3.0-only
using System.Collections.Generic;
using MapProHammer.IO;

namespace MapProHammer.Model
{

    public class MapQuadNode
    {
        public int   X, Z;
        public int   Depth;
        public float Size;

        public List<MapObject>   Items    = new();
        public List<MapQuadNode> Children = new();

        public int FileBlockId = -1;

        public void ReadRef(MapBinaryReader r)
        {
            FileBlockId = r.ReadInt32();
        }

        public void LoadItemsBlock(FileBlocksReader blocks, MapFile map)
        {
            if (FileBlockId <= 0) return;
            try
            {
                var r = blocks.GetBlock(FileBlockId);
                ReadItemsFromReader(r, blocks, map);
            }
            catch { }
        }

        private void ReadItemsFromReader(MapBinaryReader r, FileBlocksReader blocks, MapFile map)
        {
            int itemCount = r.ReadInt32();
            for (int i = 0; i < itemCount; i++)
            {
                var obj = new MapObject();
                obj.Read(r, map);
                Items.Add(obj);
            }

            int childCount = r.ReadInt32();
            for (int i = 0; i < childCount; i++)
            {
                int cx = r.ReadInt32();
                int cz = r.ReadInt32();

                var child = GetOrCreateChild(cx, cz);
                child.ReadRef(r);
                child.LoadItemsBlock(blocks, map);
            }
        }

        private MapQuadNode GetOrCreateChild(int cx, int cz)
        {
            foreach (var c in Children)
                if (c.X == cx && c.Z == cz) return c;

            var node = new MapQuadNode
            {
                X = cx, Z = cz,
                Depth = Depth + 1,
                Size  = Size / 2f
            };
            Children.Add(node);
            return node;
        }

        public void WriteBlocks(FileBlocksWriter fw, FileBlocksReader? oldBlocks, MapFile map)
        {

            foreach (var child in Children)
                child.WriteBlocks(fw, oldBlocks, map);

            foreach (var obj in Items)
            {
                if (obj.Data != null)
                {
                    byte[] dataBytes = obj.Data.Write();
                    obj.FileIdData = fw.AddBlock(dataBytes);
                }
            }

            var w = new MapBinaryWriter();
            w.WriteInt32(Items.Count);
            foreach (var obj in Items) obj.Write(w);

            w.WriteInt32(Children.Count);
            foreach (var child in Children)
            {
                w.WriteInt32(child.X);
                w.WriteInt32(child.Z);
                w.WriteInt32(child.FileBlockId);
            }

            FileBlockId = fw.AddBlock(w);
        }

        public void CollectAllObjects(List<MapObject> result)
        {
            result.AddRange(Items);
            foreach (var c in Children) c.CollectAllObjects(result);
        }

        public static int FloatToIndex(float f, float size = 1000f)
        {
            float v = f / size + 0.5f;
            if (v < 0f) v -= 1f;
            return (int)v;
        }
    }
}
