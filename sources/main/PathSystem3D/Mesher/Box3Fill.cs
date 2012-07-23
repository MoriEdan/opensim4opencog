using System;
using System.Collections.Generic;
using System.Threading;
using PathSystem3D.Navigation;
using OpenMetaverse;
using THIRDPARTY.OpenSim.Region.Physics.Meshing;
using THIRDPARTY.PrimMesher;

namespace PathSystem3D.Mesher
{
    public class Box3Fill : IComparable<Box3Fill>, IEquatable<Box3Fill>, CollisionObject
    {

        public bool IsSolid
        {
            get { return true; }
        }

        public bool Same(Box3Fill other)
        {
            if (Object.ReferenceEquals(other, null)) return false;
            if (Object.ReferenceEquals(this, other)) return true;// Object.ReferenceEquals(o2, null);
            if (other.MaxX == MaxX &&
                other.MaxY == MaxY &&
                other.MaxZ == MaxZ &&
                other.MinX == MinX &&
                other.MinY == MinY &&
                other.MinZ == MinZ) return true; 

            return false;
        }
        #region IEquatable<Box3Fill> Members

        public bool Equals(Box3Fill o2)
        {
            if (Object.ReferenceEquals(o2, null)) return false;
            if (Object.ReferenceEquals(this, o2)) return true;// Object.ReferenceEquals(o2, null);
            /*
            if (other.MaxX == MaxX &&
                other.MaxY == MaxY &&
                other.MaxZ == MaxZ &&
                other.MinX == MinX &&
                other.MinY == MinY &&
                other.MinZ == MinZ) return true;  */

            return false;
        }

        #endregion

        public override bool Equals(object obj)
        {
            if (obj is Box3Fill)
            {
                return Equals((Box3Fill)obj);
            }
            return false;
        }

        public void AddPos(Vector3 offset)
        {
            MinX += offset.X;
            MaxX += offset.X;
            MinY += offset.Y;
            MaxY += offset.Y;
            MinZ += offset.Z;
            MaxZ += offset.Z;
            NeedsRounding = true;
            if (RoundBoxes) Round();
        }

        private void Round()
        {
            if (!NeedsRounding) return;
            NeedsRounding = false;
            MinX = MinRound(MinX);
            MinY = MinRound(MinY);
            MaxX = MinRound(MaxX);
            MaxY = MinRound(MaxY);
            RoundZ();
        }

        private void RoundZ()
        {
            MinZ = MinRound(MinZ, 1);
            MaxZ = MinRound(MaxZ, 1);
        }

        private static float MinRound(float t)
        {
            return (float) (Math.Round(t*5)/5);
        }
        private static float MinRound(float t, int by)
        {
            return (float) Math.Round(t, by);
        }

        public static bool operator ==(Box3Fill o1, Box3Fill o2)
        {
            if (Object.ReferenceEquals(o1, null)) return Object.ReferenceEquals(o2, null);
            if (Object.ReferenceEquals(o2, null)) return false;
            if (Object.ReferenceEquals(o1, o2)) return true;// Object.ReferenceEquals(o2, null);
            return false;// o1.Equals(o2);
        }

        public static bool operator !=(Box3Fill o1, Box3Fill o2)
        {
            return !o1.Equals(o2);
        }


        public float MinX;// = float.MaxValue;
        public float MaxX;// = float.MinValue;
        public float MinY;// = float.MaxValue;
        public float MaxY;// = float.MinValue;
        const bool RoundBoxes = true;
        public bool NeedsRounding = RoundBoxes;
        public float MinZ { get; set; }// = float.MaxValue;
        public float MaxZ { get; set; }// = float.MinValue;

        public Box3Fill(Triangle t1, Triangle t2, Vector3 padXYZ)
        {
            MinX = t1.v1.X;
            MaxX = t1.v1.X;
            MinY = t1.v1.Y;
            MaxY = t1.v1.Y;
            MinZ = t1.v1.Z;
            MaxZ = t1.v1.Z;
            AddVertex(t1.v2, padXYZ);
            AddVertex(t1.v3, padXYZ);
            AddTriangle(t2, padXYZ);
        }

        /// <summary>
        /// Construct an infinately small box
        /// </summary>
        //public Box3Fill(bool b) { Reset(); }
        /// <summary>
        ///  Make the box infinatly small
        /// </summary>        
        public Box3Fill(bool b)
        {
            MinX = float.MaxValue;
            MaxX = float.MinValue;
            MinY = float.MaxValue;
            MaxY = float.MinValue;
            MinZ = float.MaxValue;
            MaxZ = float.MinValue;
        }

        public void Reset()
        {
            MinX = float.MaxValue;
            MaxX = float.MinValue;
            MinY = float.MaxValue;
            MaxY = float.MinValue;
            MinZ = float.MaxValue;
            MaxZ = float.MinValue;
        }

        //const float PadXYZ = 0.33f;// SimPathStore.StepSize*0.75f;
        //public const float PADZ = 0.1f;// SimPathStore.StepSize*0.75f;

        public override int GetHashCode()
        {

            return MinEdge.GetHashCode() ^ MaxEdge.GetHashCode();
        }

        public override string ToString()
        {
            return "(" + MinEdge + " - " + MaxEdge + ")";
        }

        public void SetBoxOccupied(CallbackXYBox p, float detail)
        {
            for (float x = MinX; x <= MaxX; x += detail)
            {
                for (float y = MinY; y <= MaxY; y += detail)
                {
                    p(x, y, this);
                }
            }
            p(MaxX, MaxY, this);
        }

        public void SetOccupied(CallbackXY p, SimZMinMaxLevel MinMaxZ, float detail)
        {
            // detail /= 2f;
            //float MinX = this.MinX + offset.X;
            //float MaxX = this.MaxX + offset.X;
            //float MinY = this.MinY + offset.Y;
            //float MaxY = this.MaxY + offset.Y;
            //float MinZ = this.MinZ + offset.Z;
            //float MaxZ = this.MaxZ + offset.Z;

            float SimZMinLevel, SimZMaxLevel;

            // = SimPathStore.StepSize;
            for (float x = MinX; x <= MaxX; x += detail)
            {
                for (float y = MinY; y <= MaxY; y += detail)
                {
                    MinMaxZ(x, y, out SimZMinLevel, out SimZMaxLevel);
                    if (SimZMinLevel > MaxZ || SimZMaxLevel < MinZ)
                    {
                        // this box is not between the Z levels
                        continue;
                    }
                    p(x, y, MinZ, MaxZ);
                }
            }
            /// the for/next loop probably missed this last point
            MinMaxZ(MaxX, MaxY, out SimZMinLevel, out SimZMaxLevel);
            if (SimZMinLevel > MaxZ || SimZMaxLevel < MinZ)
            {
                // this box is not between the Z levels
                return;
            }
            p(MaxX, MaxY, MinZ, MaxZ);
        }

        public void SetOccupied(CallbackXY p, float SimZMinLevel, float SimZMaxLevel, float detail)
        {
            //float MinX = this.MinX + offset.X;
            //float MaxX = this.MaxX + offset.X;
            //float MinY = this.MinY + offset.Y;
            //float MaxY = this.MaxY + offset.Y;
            //float MinZ = this.MinZ + offset.Z;
            //float MaxZ = this.MaxZ + offset.Z;


            if (SimZMinLevel > MaxZ || SimZMaxLevel < MinZ)
            {
                // this box is not between the Z levels
                return;
            }

            // = SimPathStore.StepSize;
            for (float x = MinX; x <= MaxX; x += detail)
            {
                for (float y = MinY; y <= MaxY; y += detail)
                {
                    p(x, y, MinZ, MaxZ);
                }
            }
            /// the for/next loop probably missed this last point
            p(MaxX, MaxY, MinZ, MaxZ);
        }


        public string ToString(Vector3 offset)
        {
            string s = "(" + (Vector3)(MinEdge + offset) + " - " + (Vector3)(MaxEdge + offset) + " mass= " + Mass + ")";
            return s;
        }

        /// <summary>
        /// Make sure box is big enough for this vertex
        /// </summary>
        /// <param name="v"></param>
        /// <returns>true if the box has grown</returns>
        internal void AddVertex(Vertex v, Vector3 padXYZ)
        {
            AddPoint(v.X, v.Y, v.Z, padXYZ);
        }

        internal void AddPoint(float x, float y, float z, Vector3 padXYZ)
        {
            NeedsRounding = true;
            // bool changed = false;
            if (x < MinX)
            {
                MinX = x - padXYZ.X;
                //  changed = true;
            }
            if (y < MinY)
            {
                MinY = y - padXYZ.Y;
                // changed = true;
            }
            if (z < MinZ)
            {
                MinZ = z - padXYZ.Z;
                //changed = true;
            }

            if (x > MaxX)
            {
                MaxX = x + padXYZ.X;
                // changed = true;
            }
            if (y > MaxY)
            {
                MaxY = y + padXYZ.Y;
                // changed = true;
            }
            if (z > MaxZ)
            {
                MaxZ = z;// +padXYZ.Z;
                //changed = true;
            }
            //return changed;
        }

        /// <summary>
        /// Add Triangle (this just pushes the size of the box outward if needed)
        /// </summary>
        /// <param name="t"></param>
        /// <returns>true if the boxsize was increased</returns>
        public void AddTriangle(Triangle t, Vector3 padXYZ)
        {
            AddVertex(t.v1, padXYZ);
            AddVertex(t.v2, padXYZ);
            AddVertex(t.v3, padXYZ);
        }

        public Vector3 MinEdge
        {
            get
            {
                return new Vector3(MinX, MinY, MinZ);
            }
        }
        public Vector3 MaxEdge
        {
            get
            {
                return new Vector3(MaxX, MaxY, MaxZ);
            }
        }

        public bool IsInsideXY(float x, float y)
        {
            if (
             (x < MinX) ||
             (y < MinY) ||
             (x > MaxX) ||
             (y > MaxY)) return false;
            return true;
        }

        public bool IsInside(float x, float y, float z)
        {
            if (
             (x < MinX) ||
             (y < MinY) ||
             (z < MinZ) ||
             (x > MaxX) ||
             (y > MaxY) ||
             (z > MaxZ)) return false;
            return true;
        }

        public float Mass
        {
            get { return Math.Abs(MaxX - MinX) * Math.Abs(MaxY - MinY) * Math.Abs(MaxZ - MinZ); }
        }

        public float EdgeSize
        {
            get
            {
                return Math.Abs(MaxX - MinX) + Math.Abs(MaxY - MinY) + Math.Abs(MaxZ - MinZ);
            }
        }
        public float MaxEdgeSize                
        {
            get
            {
                return Math.Max(Math.Max(Math.Abs(MaxX - MinX) , Math.Abs(MaxY - MinY)),Math.Abs(MaxZ - MinZ));
            }
        }

        public float MinEdgeSize
        {
            get
            {
                return Math.Min(Math.Min(Math.Abs(MaxX - MinX), Math.Abs(MaxY - MinY)), Math.Abs(MaxZ - MinZ));
            }
        }

        public bool IsCompletelyInside(Box3Fill inner)
        {
            if ((inner.MaxX > MaxX) ||
             (inner.MinX < MinX) ||
             (inner.MaxY > MaxY) ||
             (inner.MinY < MinY) ||
             (inner.MaxZ > MaxZ) ||
             (inner.MinZ < MinZ)) return false;
            return true;
        }

        public bool IsCompletelyZInside(CollisionObject inner)
        {
            if ((inner.MaxZ > MaxZ) ||
             (inner.MinZ < MinZ)) return false;
            return true;
        }

        #region IComparable<Box3Fill> Members

        public int CompareTo(Box3Fill other)
        {
            return Bigger0(this, other);
        }

        #endregion

        public static List<Box3Fill> Simplify(List<Box3Fill> simpl)
        {
            try
            {
                return Simplify1(simpl);
            }
            catch (Exception)
            {
                return simpl;
            }
            int bc = simpl.Count;
            int t0 = Environment.TickCount;
            List<Box3Fill> s0 = Simplify0(new List<Box3Fill>(simpl));
            int t1 = Environment.TickCount;
            List<Box3Fill> s1 = Simplify1(new List<Box3Fill>(simpl));
            int t2 = Environment.TickCount;
            int c0 = s0.Count;
            int c1 = s1.Count;
            CollisionPlane.Debug("Simplify {0} S0={1}/{2}  S1={3}/{4}  ", bc, t1 - t0, c0, t2 - t1, c1);
            if (c1 < c0) return s1;
            return s0;
        }

        public static List<CollisionObject> SimplifyZ(List<CollisionObject> simpl)
        {
            if (RoundBoxes) foreach (Box3Fill box3Fill in simpl)
            {
                box3Fill.Round();
            }
            int simplCount = simpl.Count;
            if (simplCount < 2) return simpl;
            int c = simplCount * 3 / 4;
            simpl.Sort(BiggerZ);
            List<CollisionObject> retval = new List<CollisionObject>(c);
            int len = simpl.Count;
            int len1 = len - 1;
            for (int i = 0; i < len; i++)
            {
                var bi = simpl[i];
                if (bi == null) continue;
                //bi.MakeAtLeast(0.2f);
                bool foundInside = false;
                for (int ii = len1; ii > i; ii--)
                {
                    var bii = simpl[ii];
                    if (bii == null) continue;

                    if (((Box3Fill)bii).IsCompletelyZInside(bi))
                    {
                        foundInside = true;
                        break;
                    }
                }
                if (!foundInside)
                {
                    retval.Add(bi);
                }
            }
            return retval;
        }

        public static List<Box3Fill> Simplify1(List<Box3Fill> simpl)
        {
            if (RoundBoxes) foreach (Box3Fill box3Fill in simpl)
            {
                box3Fill.Round();
            }
            int simplCount = simpl.Count;
            if (simplCount < 2) return simpl;
            int c = simplCount * 3 / 4;
            simpl.Sort(Bigger1);
            List<Box3Fill> retval = new List<Box3Fill>(c);
            int len = simpl.Count;
            int len1 = len - 1;
            for (int i = 0; i < len; i++)
            {
                Box3Fill bi = simpl[i];
                if (bi == null) continue;
                //bi.MakeAtLeast(0.2f);
                bool foundInside = false;
                for (int ii = len1; ii > i; ii--)
                {
                    Box3Fill bii = simpl[ii];
                    if (bii == null) continue;

                    if (bii.IsCompletelyInside(bi))
                    {
                        foundInside = true;
                        break;
                    }
                }
                if (!foundInside)
                {
                    retval.Add(bi);
                }
            }
            return retval;
        }
        private void MakeAtLeast(float f)
        {
            MakeAtLeast(f, ref MaxX, ref MinX);
            MakeAtLeast(f, ref MaxY, ref MinY);
            float size = MaxZ - MinZ;
            if (size < f)
            {
                MinZ = MaxZ - f;
            }
        }

        private void MakeAtLeast(float f, ref float MaxXYZ, ref float MinXYZ)
        {
            float size = MaxXYZ - MinXYZ;
            if (size<f)
            {
                MinXYZ = MaxXYZ - f;
            }
        }

        static int Bigger1(Box3Fill b1, Box3Fill b2)
        {
            float f1 = b1.Mass;
            float f2 = b2.Mass;
            if (f1 == f2)
                return 0;
            return f1 < f2 ? -1 : 1;
        }

        static int BiggerZ(CollisionObject b1, CollisionObject b2)
        {
            float f1 = b1.MaxZ - b1.MinZ;
            float f2 = b2.MaxZ - b2.MinZ;
            if (f1 == f2)
                return 0;
            return f1 < f2 ? -1 : 1;
        }

        public static List<Box3Fill> Simplify0(List<Box3Fill> simpl)
        {
            int c = simpl.Count*3/4;
            simpl.Sort(Bigger0);
            List<Box3Fill> retval = new List<Box3Fill>(c);
            int len = simpl.Count;
            int len1 = len - 1;
            for (int i = 0; i < len; i++)
            {
                Box3Fill bi = simpl[i];
                if (bi == null) continue;
                bool foundInside = false;
                for (int ii = len1; ii > i; ii--)
                {
                    Box3Fill bii = simpl[ii];
                    if (bii == null) continue;

                    if (bii.IsCompletelyInside(bi))
                    {
                        foundInside = true;
                        break;
                    }
                }
                if (!foundInside)
                {
                    retval.Add(bi);
                }
            }
            return retval;
        }

        static int Bigger0(Box3Fill b1, Box3Fill b2)
        {
            if (b1.Same(b2)) return 0;
            if (b1.MinX > b2.MinX)
            {
                return -1;
            }
            if (b1.MinY > b2.MinY)
            {
                return -1;
            }
            if (b1.MinZ > b2.MinZ)
            {
                return -1;
            }
            if (b1.MaxX < b2.MaxX)
            {
                return -1;
            }
            if (b1.MaxY < b2.MaxY)
            {
                return -1;
            }
            if (b1.MaxZ < b2.MaxZ)
            {
                return -1;
            }
            float f1 = b1.Mass;
            float f2 = b2.Mass;
            if (f1 == f2)
                return 1;
            return f1 < f2 ? -1 : 1;
        }



        public bool IsZInside(float low, float high)
        {
            if (low > MaxZ || high < MinZ) return false;
            return true;
        }

        internal void Expand(Box3Fill B)
        {
            AddPoint(B.MaxEdge);
            AddPoint(B.MinEdge);
        }

        public void AddPoint(Vector3 vector3)
        {
            AddPoint(vector3.X, vector3.Y, vector3.Z, Vector3.Zero);
        }

        #region CollisionObject Members


        public void RemeshObject(Box3Fill changed)
        {
            throw new NotImplementedException();
        }

        public bool SomethingMaxZ(float xf, float yf, float low, float high, out float maxZ)
        {
            bool found = false;
            maxZ = MinZ;
            if (!IsInsideXY(xf, yf)) return false;
            if (IsZInside(low, high))
            {
                found = true;
                if (MaxZ > maxZ) maxZ = MaxZ;
            }
            return found;
        }

        #endregion

        internal void Constrain(Box3Fill OuterBox)
        {
            if (MinX < OuterBox.MinX) MinX = OuterBox.MinX;
            if (MinY < OuterBox.MinY) MinY = OuterBox.MinY;
            if (MinZ < OuterBox.MinZ) MinZ = OuterBox.MinZ;
            if (MaxX > OuterBox.MaxX) MaxX = OuterBox.MaxX;
            if (MaxY > OuterBox.MaxY) MaxY = OuterBox.MaxY;
            if (MaxZ > OuterBox.MaxZ) MaxZ = OuterBox.MaxZ;
        }
    }
}
