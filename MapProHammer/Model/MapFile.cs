// Copyright (c) 2026 Catir1337
// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapProHammer.IO;

namespace MapProHammer.Model
{

    public class MapFile
    {
        public const int CurrentVersion = 15;

        public int Version { get; private set; }
        public int MaxID { get; set; }
        public int ObjInfosIDs { get; set; }

        public List<MapObjectType> ObjectTypes { get; } = new();
        public Dictionary<int, MapObjectType> ObjTypeById { get; } = new();
        public List<MapQuadNode> RootNodes { get; } = new();

        private string _filePath = string.Empty;

        public static MapFile Load(string path)
        {
            var map = new MapFile { _filePath = path };
            var blocks = new FileBlocksReader(path);
            blocks.ValidateSignature();
            map.ParseMainBlock(blocks);
            map.LoadAllDataBlocks(blocks);
            return map;
        }

        private void ParseMainBlock(FileBlocksReader blocks)
        {
            var r = blocks.GetLastBlock();

            Version = r.ReadInt32();
            MaxID = r.ReadInt32();
            ObjInfosIDs = r.ReadInt32();

            int typeCount = r.ReadInt32();
            for (int i = 0; i < typeCount; i++)
            {
                var t = new MapObjectType();
                t.Read(r);
                ObjectTypes.Add(t);
                ObjTypeById[t.Id] = t;
            }

            bool deferred = (Version == CurrentVersion);
            int rootCount = r.ReadInt32();
            for (int i = 0; i < rootCount; i++)
            {
                int x = r.ReadInt32();
                int z = r.ReadInt32();

                var node = new MapQuadNode
                {
                    X = x,
                    Z = z,
                    Depth = 0,
                    Size = 1000f
                };

                node.ReadRef(r);

                if (deferred)
                    node.LoadItemsBlock(blocks, this);
                else
                    node.LoadItemsBlock(blocks, this);

                RootNodes.Add(node);
            }
        }

        private void LoadAllDataBlocks(FileBlocksReader blocks)
        {
            foreach (var root in RootNodes)
                LoadDataBlocksRecursive(root, blocks);
        }

        private void LoadDataBlocksRecursive(MapQuadNode node, FileBlocksReader blocks)
        {
            foreach (var obj in node.Items)
                obj.LoadDataBlock(blocks);
            foreach (var child in node.Children)
                LoadDataBlocksRecursive(child, blocks);
        }

        public void Save(string? path = null)
        {
            path ??= _filePath;
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("Путь не указан");

            var fw = new FileBlocksWriter();

            foreach (var root in RootNodes)
                root.WriteBlocks(fw, null, this);

            var main = new MapBinaryWriter();
            main.WriteInt32(CurrentVersion);
            main.WriteInt32(MaxID);
            main.WriteInt32(ObjInfosIDs);

            main.WriteInt32(ObjectTypes.Count);
            foreach (var t in ObjectTypes) t.Write(main);

            main.WriteInt32(RootNodes.Count);
            foreach (var node in RootNodes)
            {
                main.WriteInt32(node.X);
                main.WriteInt32(node.Z);
                main.WriteInt32(node.FileBlockId);
            }

            fw.AddBlock(main);

            File.WriteAllBytes(path, fw.Build());
            _filePath = path;
        }

        public List<MapObject> GetAllObjects()
        {
            var result = new List<MapObject>();
            foreach (var root in RootNodes)
                root.CollectAllObjects(result);
            return result;
        }

        public int AllocID() => ++MaxID;

        /// <summary>
        /// Найти тип по ObjPath, или создать новый и зарегистрировать его в таблице.
        /// </summary>
        public MapObjectType GetOrCreateType(string objPath, string guid = "")
        {
            var existing = ObjectTypes.FirstOrDefault(t =>
                string.Equals(t.ObjPath, objPath, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;

            var newType = new MapObjectType
            {
                Id = ++ObjInfosIDs,
                Guid = guid,
                ObjPath = objPath,
                IsDecal = false,
                NeedLoadOnServer = false
            };

            ObjectTypes.Add(newType);
            ObjTypeById[newType.Id] = newType;
            return newType;
        }

        public void AddObject(MapObject obj)
        {
            int nx = MapQuadNode.FloatToIndex(obj.Position.X);
            int nz = MapQuadNode.FloatToIndex(obj.Position.Z);
            MapQuadNode? target = null;
            foreach (var root in RootNodes)
                if (root.X == nx && root.Z == nz) { target = root; break; }

            if (target == null)
            {
                target = new MapQuadNode { X = nx, Z = nz, Depth = 0, Size = 1000f };
                RootNodes.Add(target);
            }
            target.Items.Add(obj);
        }

        public void RemoveObject(MapObject obj)
        {
            foreach (var root in RootNodes)
                RemoveRecursive(root, obj);
        }

        private static bool RemoveRecursive(MapQuadNode node, MapObject obj)
        {
            if (node.Items.Remove(obj)) return true;
            foreach (var c in node.Children)
                if (RemoveRecursive(c, obj)) return true;
            return false;
        }

        public string FilePath => _filePath;
    }
}
