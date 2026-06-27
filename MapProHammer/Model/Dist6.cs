using MapProHammer.IO;

namespace MapProHammer.Model
{

    public struct Dist6
    {
        public float Up, Down, Left, Right, Forward, Back;

        public float this[int i]
        {
            get => i switch { 0=>Up, 1=>Down, 2=>Left, 3=>Right, 4=>Forward, 5=>Back, _=>0 };
            set
            {
                switch (i)
                {
                    case 0: Up      = value; break;
                    case 1: Down    = value; break;
                    case 2: Left    = value; break;
                    case 3: Right   = value; break;
                    case 4: Forward = value; break;
                    case 5: Back    = value; break;
                }
            }
        }

        public void Read(MapBinaryReader r)
        {
            for (int i = 0; i < 6; i++) this[i] = r.ReadFloat();
        }

        public void Write(MapBinaryWriter w)
        {
            for (int i = 0; i < 6; i++) w.WriteFloat(this[i]);
        }

        public override string ToString() =>
            $"U:{Up:F2} D:{Down:F2} L:{Left:F2} R:{Right:F2} F:{Forward:F2} B:{Back:F2}";
    }
}
