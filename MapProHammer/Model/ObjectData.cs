// Copyright (c) 2026 Catir1337
// SPDX-License-Identifier: GPL-3.0-only
using System.Numerics;
using MapProHammer.IO;

namespace MapProHammer.Model.ObjectData
{

    public interface IMapObjectData
    {
        string TypeName { get; }
        void   Read(MapBinaryReader r);
        byte[] Write();
    }

    public class SpawnCarData : IMapObjectData
    {
        public string TypeName => "SpawnCar";

        public byte Time;

        public byte MinK;
        public byte MaxK = 150;

        public byte Sport, Selo, Russian, Jip, Kamaz;

        public void Read(MapBinaryReader r)
        {
            Time   = r.ReadByte();
            MinK   = r.ReadByte();
            MaxK   = r.ReadByte();
            Sport  = r.ReadByte();
            Selo   = r.ReadByte();
            Russian= r.ReadByte();
            Jip    = r.ReadByte();
            Kamaz  = r.ReadByte();
        }

        public byte[] Write()
        {
            var w = new MapBinaryWriter();
            w.WriteByte(Time); w.WriteByte(MinK);   w.WriteByte(MaxK);
            w.WriteByte(Sport);w.WriteByte(Selo);   w.WriteByte(Russian);
            w.WriteByte(Jip);  w.WriteByte(Kamaz);
            return w.ToArray();
        }
    }

    public class SpawnHumanData : IMapObjectData
    {
        public string TypeName => "SpawnHuman";

        public byte Time;

        public byte Famaly;

        public byte Naked, Suit, Worker, Prostitutka;

        public void Read(MapBinaryReader r)
        {
            Time       = r.ReadByte();
            Famaly     = r.ReadByte();
            Naked      = r.ReadByte();
            Suit       = r.ReadByte();
            Worker     = r.ReadByte();
            Prostitutka= r.ReadByte();
        }

        public byte[] Write()
        {
            var w = new MapBinaryWriter();
            w.WriteByte(Time); w.WriteByte(Famaly);     w.WriteByte(Naked);
            w.WriteByte(Suit); w.WriteByte(Worker);     w.WriteByte(Prostitutka);
            return w.ToArray();
        }
    }

    public class WireObjectData : IMapObjectData
    {
        public string TypeName => "Wire";

        public int   Id;
        public int[] ConnectToIds = System.Array.Empty<int>();

        public void Read(MapBinaryReader r)
        {
            Id = r.ReadInt32();
            int count = r.ReadInt32();
            ConnectToIds = new int[count];
            for (int i = 0; i < count; i++) ConnectToIds[i] = r.ReadInt32();
        }

        public byte[] Write()
        {
            var w = new MapBinaryWriter();
            w.WriteInt32(Id);
            w.WriteInt32(ConnectToIds?.Length ?? 0);
            if (ConnectToIds != null)
                foreach (int id in ConnectToIds) w.WriteInt32(id);
            return w.ToArray();
        }
    }

    public class DecalObjectData : IMapObjectData
    {
        public string TypeName => "Decal";

        public float MaxAngle     = 80f;
        public float PushDistance = 0.009f;
        public int   AffectedLayers;
        public float Opacity      = 1f;
        public bool  IsSpline;
        public byte  OrderLayer   = 120;
        public byte  OpacityIn, OpacityOut;
        public Vector3[]? SplinePoints;

        public byte[] MeshRaw = System.Array.Empty<byte>();

        public void Read(MapBinaryReader r)
        {
            MaxAngle      = r.ReadFloat();
            PushDistance  = r.ReadFloat();
            AffectedLayers= r.ReadInt32();
            Opacity       = r.ReadFloat01Byte();
            IsSpline      = r.ReadBool();
            OrderLayer    = r.ReadByte();
            SplinePoints  = null;

            if (IsSpline)
            {
                OpacityIn  = r.ReadByte();
                OpacityOut = r.ReadByte();
                int count  = r.ReadInt32();
                SplinePoints = new Vector3[count];
                for (int i = 0; i < count; i++) SplinePoints[i] = r.ReadVec3();
            }

            MeshRaw = r.ReadRemainingBytes();
        }

        public byte[] Write()
        {
            var w = new MapBinaryWriter();
            w.WriteFloat(MaxAngle);
            w.WriteFloat(PushDistance);
            w.WriteInt32(AffectedLayers);
            w.WriteFloat01Byte(Opacity);
            w.WriteBool(IsSpline);
            w.WriteByte(OrderLayer);

            if (IsSpline)
            {
                w.WriteByte(OpacityIn);
                w.WriteByte(OpacityOut);
                w.WriteInt32(SplinePoints?.Length ?? 0);
                if (SplinePoints != null)
                    foreach (var p in SplinePoints) w.WriteVec3(p);
            }

            w.WriteBytes(MeshRaw);
            return w.ToArray();
        }
    }

    public class RawObjectData : IMapObjectData
    {
        public string TypeName => "Unknown";
        public byte[] Raw = System.Array.Empty<byte>();

        public void Read(MapBinaryReader r) => Raw = r.ReadRemainingBytes();
        public byte[] Write() => Raw;
    }
}
