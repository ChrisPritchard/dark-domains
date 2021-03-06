
namespace HexMapTutorials
{
    using System;
    using System.IO;
    using UnityEngine;

    [Serializable]
    public struct HexCoordinates
    {
        public int X { get; private set; }
        public int Y => -X - Z;
        public int Z { get; private set; }

        public HexCoordinates(int x, int z)
        {
            if(HexMetrics.Wrapping)
            {
                var oX = x + z / 2;
                if(oX < 0)
                    x += HexMetrics.WrapSize;
                else if(oX >= HexMetrics.WrapSize)
                    x -= HexMetrics.WrapSize;
            }
            X = x;
            Z = z;
        }

        public override string ToString() => "(" + ToString(", ") + ")";

        public string ToString(string sep) => X + sep + Y + sep + Z;

        public static HexCoordinates FromOffsetCoordinates(int x, int z) => new HexCoordinates(x-z/2, z);

        public static HexCoordinates FromPosition(Vector3 position)
        {
            var x = position.x / HexMetrics.InnerDiameter;
            var y = -x;
            var offset = position.z / (HexMetrics.OuterRadius * 3f);
            x -= offset;
            y -= offset;

            var iX = Mathf.RoundToInt(x);
            var iY = Mathf.RoundToInt(y);
            var iZ = Mathf.RoundToInt(-x - y);

            if (iX + iY + iZ != 0)
            {
                var dX = Mathf.Abs(x - iX);
                var dY = Mathf.Abs(y - iY);
                var dZ = Mathf.Abs(-x -y - iZ);

                if (dX > dY && dX > dZ)
                    iX = -iY - iZ;
                else if (dZ > dY)
                    iZ = -iX - iY;
            }

            return new HexCoordinates(iX, iZ);
        }

        public bool IsTheSameAs(HexCoordinates other) => other.X == X && other.Y == Y && other.Z == Z;

        public int DistanceTo(HexCoordinates other)
        {
            var xy = 
                (X < other.X ? other.X - X : X - other.X) +
                (Y < other.Y ? other.Y - Y : Y - other.Y);

            if(HexMetrics.Wrapping)
            {
                other.X += HexMetrics.WrapSize;
                var xyWrapped = 
                    (X < other.X ? other.X - X : X - other.X) +
                    (Y < other.Y ? other.Y - Y : Y - other.Y);
                if(xyWrapped < xy)
                    xy = xyWrapped;
                else
                {
                    other.X -= 2 * HexMetrics.WrapSize;
                    xyWrapped = 
                        (X < other.X ? other.X - X : X - other.X) +
                        (Y < other.Y ? other.Y - Y : Y - other.Y);
                    if(xyWrapped < xy)
                        xy = xyWrapped;
                }
            }

            return (xy + (Z < other.Z ? other.Z - Z : Z - other.Z)) / 2;
        }

        public void Save(BinaryWriter writer)
        {
            writer.Write(X);
            writer.Write(Z);
        }

        public static HexCoordinates Load(BinaryReader reader)
        {
            var x = reader.ReadInt32();
            var z = reader.ReadInt32();
            return new HexCoordinates(x, z);
        }
    }
}