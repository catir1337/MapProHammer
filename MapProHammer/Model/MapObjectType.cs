// Copyright (c) 2026 Catir1337
// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using MapProHammer.IO;

namespace MapProHammer.Model
{

    public class MapObjectType
    {
        public int    Id;
        public string Guid      = string.Empty;
        public string ObjPath   = string.Empty;
        public bool   IsDecal;
        public bool   NeedLoadOnServer;
        public Dist6  Distances;

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(ObjPath))
                    return Path.GetFileNameWithoutExtension(ObjPath);
                return Guid.Length > 12 ? Guid[..12] + "…" : Guid;
            }
        }

        public ObjectKind DetectKind()
        {
            string name = (ObjPath + Guid).ToLowerInvariant();
            if (IsDecal)                          return ObjectKind.Decal;
            if (name.Contains("spawncar"))        return ObjectKind.SpawnCar;
            if (name.Contains("spawnhuman"))      return ObjectKind.SpawnHuman;
            if (name.Contains("wire"))            return ObjectKind.Wire;
            return ObjectKind.Generic;
        }

        public void Read(MapBinaryReader r)
        {
            Id              = r.ReadInt32();
            Guid            = r.ReadString();
            ObjPath         = r.ReadString();
            IsDecal         = r.ReadBool();
            NeedLoadOnServer= r.ReadBool();
            Distances.Read(r);
        }

        public void Write(MapBinaryWriter w)
        {
            w.WriteInt32(Id);
            w.WriteString(Guid);
            w.WriteString(ObjPath);
            w.WriteBool(IsDecal);
            w.WriteBool(NeedLoadOnServer);
            Distances.Write(w);
        }
    }

    public enum ObjectKind { Generic, Decal, SpawnCar, SpawnHuman, Wire }
}
