using System.Numerics;
using MapProHammer.IO;
using MapProHammer.Model.ObjectData;

namespace MapProHammer.Model
{

    public class MapObject
    {
        public Vector3 Position;
        public Vector3 Scale = Vector3.One;
        public Vector3 Rotation;

        public int ObjInfoId;
        public int FileIdData = -1;

        public Vector3[]? ViewLine;

        public MapObjectType? ObjType;

        public IMapObjectData? Data;

        public void Read(MapBinaryReader r, MapFile map)
        {
            r.ReadVec3(ref Position);
            r.ReadVec3(ref Scale);
            r.ReadVec3(ref Rotation);

            ObjInfoId  = r.ReadInt32();
            FileIdData = r.ReadInt32();

            ViewLine = null;
            if (FileIdData > 0)
            {
                int lineLen = r.ReadByte();
                if (lineLen > 0)
                {
                    ViewLine = new Vector3[lineLen];
                    for (int i = 0; i < lineLen; i++)
                        r.ReadVec3(ref ViewLine[i]);
                }
            }

            map.ObjTypeById.TryGetValue(ObjInfoId, out ObjType);
        }

        public void LoadDataBlock(FileBlocksReader blocks)
        {
            if (FileIdData <= 0 || ObjType == null) return;
            try
            {
                var r = blocks.GetBlock(FileIdData);
                Data = ObjType.DetectKind() switch
                {
                    ObjectKind.SpawnCar   => new SpawnCarData(),
                    ObjectKind.SpawnHuman => new SpawnHumanData(),
                    ObjectKind.Wire       => new WireObjectData(),
                    ObjectKind.Decal      => new DecalObjectData(),
                    _                     => new RawObjectData()
                };
                Data.Read(r);
            }
            catch { Data = null; }
        }

        public void Write(MapBinaryWriter w)
        {
            w.WriteVec3(Position);
            w.WriteVec3(Scale);
            w.WriteVec3(Rotation);
            w.WriteInt32(ObjInfoId);
            w.WriteInt32(FileIdData);

            if (FileIdData > 0)
            {
                int lineLen = ViewLine?.Length ?? 0;
                w.WriteByte((byte)lineLen);
                if (ViewLine != null)
                    foreach (var v in ViewLine) w.WriteVec3(v);
            }
        }

        public override string ToString() =>
            $"{ObjType?.DisplayName ?? ObjInfoId.ToString()} @ ({Position.X:F1}, {Position.Y:F1}, {Position.Z:F1})";
    }
}
