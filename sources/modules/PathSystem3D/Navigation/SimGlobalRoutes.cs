// Copyleft 2009 Douglas R. Miles (Daxtron Labs) - <dmiles@daxtron.com>
// Copyright 2003 Eric Marchesin - <eric.marchesin@laposte.net>
//
// This source file(s) may be redistributed by any means PROVIDING they
// are not sold for profit without the authors expressed written consent,
// and providing that this notice and the authors name and all copyright
// notices remain intact.
// THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED. USE IT AT YOUR OWN RISK. THE AUTHOR ACCEPTS NO
// LIABILITY FOR ANY DATA DAMAGE/LOSS THAT THIS PRODUCT MAY CAUSE.
//-----------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using OpenMetaverse;

namespace PathSystem3D.Navigation
{
/// <summary>
	/// Graph structure. It is defined with :
	/// It is defined with both a list of nodes and a list of arcs.
	/// </summary>
    [Serializable]
    public class SimGlobalRoutes
    {
        public static int StepSize = 32;
        public static SimGlobalRoutes Instance = new SimGlobalRoutes("milspec-global.serz",1000,1000,1);
        IList<SimWaypoint> SimWaypoints;
        IList<SimRoute> SimRoutes;
        public int SimZ = 22;
        public int SimsSize;

        public int StartX;// = RegionX * 256;
        public int StartY;// = RegionY * 256;
        public int Bigness;// = SimsSize * 256;
        public int EndX;// = 255 - StepSize + Bigness + StartX;
        public int EndY;// = 255 - StepSize + Bigness + StartY;
        public int TotalX;// = (EndX - StartX) / StepSize;
        public int TotalY;// = (EndY - StartY) / StepSize;
        public SimWaypoint[,] saved;

        public int RegionX,RegionY;
        /// <summary>
        /// Constructor.
        /// </summary>       
        private SimGlobalRoutes(String simName,int regionX,int regionY, int size)
        {
            Instance = this;
            RegionX = regionX;
            SimsSize = size;
            RegionY = regionY;
            RegionFileName = simName;
            SimWaypoints = new List<SimWaypoint>();
            SimRoutes = new List<SimRoute>();
            StartX = RegionX * 256;
            StartY = RegionY * 256;
            Bigness = SimsSize * 256;

            EndX = Bigness + StartX;
            EndY = Bigness + StartY;
            TotalX = (EndX - StartX) / StepSize;
            TotalY = (EndY - StartY) / StepSize;
            saved = new SimWaypoint[TotalX+1, TotalY+1];
            CreateDefaultRoutes();
            LoadFromFile();
        }


        public void CreateDefaultRoutes()
        {
            Console.WriteLine("CreateDefaultRoutes <{0},{1}>-<{2},{3}> StepSize={4}", StartX, StartY, EndX, EndY, StepSize);
            return;
            double W = 0.75f;
            int made = 0;
            for (int x = StartX; x < EndX; x += StepSize)
            {
                for (int y = StartY; y < EndY; y += StepSize)
                {
                    made++;
                    SimWaypoint sw00 = CreateXYZ(x, y);
                    SimWaypoint sw01 = CreateXYZ(x, y + StepSize);
                    SimWaypoint sw10 = CreateXYZ(x + StepSize, y);
                    SimWaypoint sw11 = CreateXYZ(x + StepSize, y + StepSize);

                    /*

                     Draws two-way Routes StepSize meters appart

                             * - *  
                             | X   
                             *   * 
                      
                     
                    */
                    AddNewArcs(sw00, sw01, W); //dirrection  |
                    AddNewArcs(sw00, sw10, W); //dirrection  -
                    AddNewArcs(sw00, sw11, W); //dirrection  \
                    AddNewArcs(sw10, sw01, W); //dirrection  /
                }
            }
            Console.WriteLine("CreateDefaultRoutes Made {0} waypoints", made);
        }

        private SimWaypoint CreateXYZ(int x, int y)
        {
            int ix = IndexX(x);
            int iy = IndexY(y);
            if (saved[ix, iy] == null)
            {
                saved[ix, iy] = SimWaypointImpl.CreateGlobal(x, y, SimZ);
            }
            SimWaypoint wp = saved[ix, iy];
            AddNode(wp);
            return wp;
        }

        private int IndexX(int x)
        {
            x -= StartX;
            return x / StepSize;
        }
        private int IndexY(int y)
        {
            y -= StartY;
            return y / StepSize;
        }

        private void AddNewArcs(SimWaypoint s, SimWaypoint e, double W)
        {
            InternArc(s, e, W);
            InternArc(e, s, W);
        }

        public SimRoute InternArc(SimWaypoint s, SimWaypoint e, double W)
        {
            if (s == e) throw new ArgumentException("s and e the same!" + s);
            SimRoute fr = FindArc(s, e);
            if (fr == null)
            {
                fr = new SimRoute(s, e);
                SimRoutesAdd(fr);
            }
            fr.Weight = W;
            return fr;
        }

        public SimRoute Intern2Arc(SimWaypoint s, SimWaypoint e, double W)
        {
            InternArc(e, s, W);
            Console.WriteLine("Intern2Arc: " + s + " <-> " + e);
            return InternArc(s, e, W);
        }

        private SimRoute FindArc(SimWaypoint s, SimWaypoint e)
        {
            lock (SimRoutes) for (int i = SimRoutes.Count; i != 0; )
                {
                    SimRoute sr = SimRoutes[--i];
                    if (sr.IsSame(s, e))
                    {
                        return sr;
                    }

                }
            return null;
        }

        public SimRoute[] GetRoute(SimWaypoint StartNode, SimWaypoint EndNode, out bool IsFake)
        {
            SimMovement AS = new SimMovement(this);
            AS.Initialize(StartNode, EndNode);
            while (AS.NextStep()) { }
            if (AS.PathFound)
            {
                // Full Path
                IsFake = false;
                return AS.PathByArcs;
            }
            // Partial Path
            IsFake = true;

            //int Nb = AS._LeafToGoBackUp.NbArcsVisited;
            //SimRoute[] Path = new SimRoute[Nb];
            //Track Cur = _LeafToGoBackUp;
            //for (int i = Nb - 1; i >= 0; i--, Cur = Cur.Queue)
            //    Path[i] = Cur.Queue.EndNode.ArcGoingTo(Cur.EndNode);
            //return Path;

            //AS.Open.Length, AS.Closed.Length, AS.StepCounter
            SimRoute[] PathByArcs = AS.PathByArcs;
            if (PathByArcs == null || PathByArcs.Length == 0)
            {
                return FakeRoute(StartNode, EndNode);
            }
            List<SimRoute> list = new List<SimRoute>();
            list.AddRange(PathByArcs);
            SimRoute LastArc = PathByArcs[PathByArcs.Length - 1];
            list.AddRange(FakeRoute(LastArc.EndNode, EndNode));
            return list.ToArray();
        }

        private SimRoute[] FakeRoute(SimWaypoint StartNode, SimWaypoint EndNode)
        {
            SimRoute[] route = new SimRoute[1];//
            SimRoute fr = Intern2Arc(StartNode, EndNode, 1.2f);
            //fr.Passable = true;
            route[0] = fr;
            return route;
        }

        /// <summary>
        /// Gets the List interface of the nodes in the graph.
        /// </summary>
        public IList<SimWaypoint> Nodes { get { return SimWaypoints; } }

        /// <summary>
        /// Gets the List interface of the arcs in the graph.
        /// </summary>
        public IList<SimRoute> Arcs { get { return SimRoutes; } }

        /// <summary>
        /// Empties the graph.
        /// </summary>
        public void Clear()
        {
            SimWaypoints.Clear();
            SimRoutes.Clear();
        }

        /// <summary>
        /// Directly Adds a node to the graph.
        /// </summary>
        /// <param name="NewNode">The node to add.</param>
        /// <returns>'true' if it has actually been added / 'false' if the node is null or if it is already in the graph.</returns>
        public bool AddNode(SimWaypoint NewNode)
        {
            if (NewNode == null || SimWaypoints.Contains(NewNode)) return false;
            SimWaypointsAdd(NewNode);
            return true;
        }

        private void SimWaypointsAdd(SimWaypoint NewNode)
        {
            lock (SimWaypoints) SimWaypoints.Add(NewNode);
        }

        /// <summary>
        /// Creates a node, adds to the graph and returns its reference.
        /// </summary>
        /// <param name="x">X coordinate.</param>
        /// <param name="y">Y coordinate.</param>
        /// <param name="z">Z coordinate.</param>
        /// <returns>The reference of the new node / null if the node is already in the graph.</returns>
        public SimWaypoint AddNode(double x, double y, double z)
        {
            SimWaypoint NewNode = SimWaypointImpl.CreateGlobal(x, y, z);
            return AddNode(NewNode) ? NewNode : null;
        }

        /// <summary>
        /// Directly Adds an arc to the graph.
        /// </summary>
        /// <exception cref="ArgumentException">Cannot add an arc if one of its extremity nodes does not belong to the graph.</exception>
        /// <param name="NewArc">The arc to add.</param>
        /// <returns>'true' if it has actually been added / 'false' if the arc is null or if it is already in the graph.</returns>
        public bool AddArc(SimRoute NewArc)
        {
            if (NewArc == null || SimRoutes.Contains(NewArc)) return false;
            if (!SimWaypoints.Contains(NewArc.StartNode) || !SimWaypoints.Contains(NewArc.EndNode))
                throw new ArgumentException("Cannot add an arc if one of its extremity nodes does not belong to the graph.");
            SimRoutesAdd(NewArc);
            return true;
        }

        private void SimRoutesAdd(SimRoute NewArc)
        {
            lock (SimRoutes) SimRoutes.Add(NewArc);
        }

        /// <summary>
        /// Creates an arc between two nodes that are already registered in the graph, adds it to the graph and returns its reference.
        /// </summary>
        /// <exception cref="ArgumentException">Cannot add an arc if one of its extremity nodes does not belong to the graph.</exception>
        /// <param name="StartNode">Start node for the arc.</param>
        /// <param name="EndNode">End node for the arc.</param>
        /// <param name="Weight">Weight for the arc.</param>
        /// <returns>The reference of the new arc / null if the arc is already in the graph.</returns>
        public SimRoute AddArc(SimWaypoint StartNode, SimWaypoint EndNode, double Weight)
        {
            SimRoute NewArc = FindArc(StartNode, EndNode);
            if (NewArc == null)
            {
                NewArc = new SimRoute(StartNode, EndNode);
                NewArc.Weight = Weight;
                return AddArc(NewArc) ? NewArc : null;
            }
            else
            {
                NewArc.Weight = Weight;
                return AddArc(NewArc) ? NewArc : null;
            }
            ///SimRoute NewArc = new SimRoute(StartNode, EndNode);
        }

        /// <summary>
        /// Adds the two opposite arcs between both specified nodes to the graph.
        /// </summary>
        /// <exception cref="ArgumentException">Cannot add an arc if one of its extremity nodes does not belong to the graph.</exception>
        /// <param name="Node1"></param>
        /// <param name="Node2"></param>
        /// <param name="Weight"></param>
        public void Add2Arcs(SimWaypoint Node1, SimWaypoint Node2, double Weight)
        {
            AddArc(Node1, Node2, Weight);
            AddArc(Node2, Node1, Weight);
        }

        /// <summary>
        /// Removes a node from the graph as well as the linked arcs.
        /// </summary>
        /// <param name="NodeToRemove">The node to remove.</param>
        /// <returns>'true' if succeeded / 'false' otherwise.</returns>
        public bool RemoveNode(SimWaypoint NodeToRemove)
        {
            if (NodeToRemove == null) return false;
            try
            {
                foreach (SimRoute A in NodeToRemove.IncomingArcs)
                {
                    lock (A.StartNode.OutgoingArcs) A.StartNode.OutgoingArcs.Remove(A);
                    lock (SimRoutes) SimRoutes.Remove(A);
                }
                foreach (SimRoute A in NodeToRemove.OutgoingArcs)
                {
                    lock (A.EndNode.IncomingArcs) A.EndNode.IncomingArcs.Remove(A);
                    lock (SimRoutes) SimRoutes.Remove(A);
                }
                lock (SimWaypoints) SimWaypoints.Remove(NodeToRemove);
            }
            catch { return false; }
            return true;
        }

        /// <summary>
        /// Removes a node from the graph as well as the linked arcs.
        /// </summary>
        /// <param name="ArcToRemove">The arc to remove.</param>
        /// <returns>'true' if succeeded / 'false' otherwise.</returns>
        public bool RemoveArc(SimRoute ArcToRemove)
        {
            if (ArcToRemove == null) return false;
            try
            {
                lock (SimRoutes) SimRoutes.Remove(ArcToRemove);
                lock (ArcToRemove.StartNode.OutgoingArcs) ArcToRemove.StartNode.OutgoingArcs.Remove(ArcToRemove);
                lock (ArcToRemove.StartNode.IncomingArcs) ArcToRemove.EndNode.IncomingArcs.Remove(ArcToRemove);
            }
            catch { return false; }
            return true;
        }

        /// <summary>
        /// Determines the bounding box of the entire graph.
        /// </summary>
        /// <exception cref="InvalidOperationException">Impossible to determine the bounding box for this graph.</exception>
        /// <param name="MinPoint">The point of minimal coordinates for the box.</param>
        /// <param name="MaxPoint">The point of maximal coordinates for the box.</param>
        public void BoundingBox(out double[] MinPoint, out double[] MaxPoint)
        {
            try
            {
                SimWaypointImpl.BoundingBox(Nodes, out MinPoint, out MaxPoint);
            }
            catch (ArgumentException e)
            { throw new InvalidOperationException("Impossible to determine the bounding box for this graph.\n", e); }
        }

        /// <summary>
        /// This function will find the closest node from a geographical position in space.
        /// </summary>
        /// <param name="PtX">X coordinate of the point from which you want the closest node.</param>
        /// <param name="PtY">Y coordinate of the point from which you want the closest node.</param>
        /// <param name="PtZ">Z coordinate of the point from which you want the closest node.</param>
        /// <param name="Distance">The distance to the closest node.</param>
        /// <param name="IgnorePassableProperty">if 'false', then nodes whose property Passable is set to false will not be taken into account.</param>
        /// <returns>The closest node that has been found.</returns>
        public SimWaypoint ClosestNode(double PtX, double PtY, double PtZ, out double Distance, bool IgnorePassableProperty)
        {
            SimWaypoint NodeMin = null;
            double DistanceMin = -1;
            Vector3d P = new Vector3d(PtX, PtY, PtZ);
            lock (SimWaypoints) foreach (SimWaypoint N in SimWaypoints)
                {
                    if (IgnorePassableProperty && N.IsPassable == false) continue;
                    double DistanceTemp = Vector3d.Distance(N.GlobalPosition, P);
                    if (DistanceMin == -1 || DistanceMin > DistanceTemp)
                    {
                        DistanceMin = DistanceTemp;
                        NodeMin = N;
                    }
                }
            Distance = DistanceMin;
            return NodeMin;
        }

        /// <summary>
        /// This function will find the nodes from a geographical position in space.
        /// </summary>
        /// <param name="P">Waypoint node.</param>
        /// <param name="MinDistance">The min distance to the node.</param>
        /// <param name="MaxDistance">The max distance to the node.</param>
        /// <param name="IgnorePassableProperty">if 'false', then nodes whose property Passable is set to false will not be taken into account.</param>
        /// <returns>The nodes that has been found.</returns>
        public IList<SimWaypoint> ClosestNodes(SimWaypoint P, double DistanceMin, double DistanceMax, bool IgnorePassableProperty)
        {
            List<SimWaypoint> waypoints = new List<SimWaypoint>();
            lock (SimWaypoints) foreach (SimWaypoint N in SimWaypoints)
                {
                    if (IgnorePassableProperty && N.IsPassable == false) continue;
                    double Distance = N.Distance(P);
                    if (Distance < DistanceMin || DistanceMax < Distance) continue;
                    waypoints.Add(N);
                }
            return waypoints;
        }

        /// <summary>
        /// This function will find the closest arc from a geographical position in space using projection.
        /// </summary>
        /// <param name="PtX">X coordinate of the point from which you want the closest arc.</param>
        /// <param name="PtY">Y coordinate of the point from which you want the closest arc.</param>
        /// <param name="PtZ">Z coordinate of the point from which you want the closest arc.</param>
        /// <param name="Distance">The distance to the closest arc.</param>
        /// <param name="IgnorePassableProperty">if 'false', then arcs whose property Passable is set to false will not be taken into account.</param>
        /// <returns>The closest arc that has been found.</returns>
        public SimRoute ClosestArc(double PtX, double PtY, double PtZ, out double Distance, bool IgnorePassableProperty)
        {
            SimRoute ArcMin = null;
            double DistanceMin = -1;
            Vector3d P = new Vector3d(PtX, PtY, PtZ);
            lock (SimRoutes) foreach (SimRoute A in SimRoutes)
                {
                    if (IgnorePassableProperty && A.Passable == false) continue;
                    Vector3d Projection = ProjectOnLine(P, A.StartNode.Position, A.EndNode.Position);
                    double DistanceTemp = Vector3d.Distance(P, Projection);
                    if (DistanceMin == -1 || DistanceMin > DistanceTemp)
                    {
                        DistanceMin = DistanceTemp;
                        ArcMin = A;
                    }
                }
            Distance = DistanceMin;
            return ArcMin;
        }

        /// <summary>
        /// Returns the projection of a point on the line defined with two other points.
        /// When the projection is out of the segment, then the closest extremity is returned.
        /// </summary>
        /// <exception cref="ArgumentNullException">None of the arguments can be null.</exception>
        /// <exception cref="ArgumentException">P1 and P2 must be different.</exception>
        /// <param name="Pt">Point to project.</param>
        /// <param name="P1">First point of the line.</param>
        /// <param name="P2">Second point of the line.</param>
        /// <returns>The projected point if it is on the segment / The closest extremity otherwise.</returns>
        public static Vector3d ProjectOnLine(Vector3d Pt, Vector3d P1, Vector3d P2)
        {
            if (Pt == null || P1 == null || P2 == null) throw new ArgumentNullException("None of the arguments can be null.");
            if (P1.Equals(P2)) throw new ArgumentException("P1 and P2 must be different.");
            Vector3d VLine = MakeDiff(P1, P2);
            Vector3d V1Pt = MakeDiff(P1, Pt);
            Vector3d Translation = VLine * VectOR(VLine, V1Pt) / SquareNorm(VLine);
            Vector3d Projection = P1 + Translation;

            Vector3d V1Pjt = MakeDiff(P1, Projection);
            double D1 = VectOR(V1Pjt, VLine);
            if (D1 < 0) return P1;

            Vector3d V2Pjt = MakeDiff(P2, Projection);
            double D2 = VectOR(V2Pjt, VLine);
            if (D2 > 0) return P2;

            return Projection;
        }

        /// <summary>
        /// Scalar product between two vectors.
        /// </summary>
        /// <param name="V1">First vector.</param>
        /// <param name="V2">Second vector.</param>
        /// <returns>Value resulting from the scalar product.</returns>
        public static double VectOR(Vector3d V1, Vector3d V2)
        {
            double ScalarProduct = 0;
            ScalarProduct += V1.X * V2.X;
            ScalarProduct += V1.Y * V2.Y;
            ScalarProduct += V1.Z * V2.Z;
            return ScalarProduct;
        }

        /// <summary>
        /// Constructs a Vector3D with two points.
        /// </summary>
        /// <param name="P1">First point of the vector.</param>
        /// <param name="P2">Second point of the vector.</param>
        public static Vector3d MakeDiff(Vector3d P1, Vector3d P2)
        {
            Vector3d v3 = new Vector3d(0, 0, 0);
            v3.X = P2.X - P1.X; v3.Y = P2.Y - P1.Y; v3.Z = P2.Z - P1.Z;
            return v3;
        }

        /// <summary>
        ///  Gets the square norm of the vector.
        /// </summary>
        /// <param name="v3">vector.</param>
        static public double SquareNorm(Vector3d v3)
        {
            double Sum = 0;
            //               for (int i = 0; i < 3; i++) 
            Sum += v3.X * v3.X;
            Sum += v3.Y * v3.Y;
            Sum += v3.Z * v3.Z;
            return Sum;
        }


        //Dictionary<uint, PrimTracker> TrackedAgents = new Dictionary<uint, PrimTracker>();
        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="agentID"></param>
        ///// <param name="point"></param>
        ///// <param name="rotation"></param>
        //public void Update(uint agentID, Vector3d point, Quaternion rotation)
        //{
        //    // return;
        //    if (!TrackedAgents.ContainsKey(agentID))
        //    {
        //        TrackedAgents[agentID] = new PrimTracker(SimWaypointImpl.CreateGlobal(point), rotation, this);
        //    }
        //    else
        //    {
        //        PrimTracker tracker = TrackedAgents[agentID];
        //        tracker.Update(point, rotation);
        //    }
        //}

        readonly string RegionFileName;

        //public SimRoute GetBeginToEndMovement(Vector3d begin, Vector3d end, double maxDist)
        //{
        //    SimRoute b = GetBeginMovement(begin, maxDist);
        //    SimRoute e = GetEndMovement(end, maxDist);
        //    if (b == null || e == null) return null;
        //    SimRoute m = new SimRoute(b.EndNode, e.StartNode);
        //    List<SimRoute> list = new List<SimRoute>();
        //    list.Add(b);
        //    list.Add(m);
        //    list.Add(e);
        //    return new SimRouteMovement(list);
        //}

        //public SimRoute GetBeginMovement(Vector3d vect, double maxDist)
        //{
        //    SimRoute bestBegin = null;
        //    double distToBegin = maxDist;
        //    foreach (SimRoute move in SimRoutes)
        //    {
        //        double close = Vector3d.Distance(vect, move.StartNode.GetSimPosition());
        //        if (close < distToBegin)
        //        {
        //            bestBegin = move;
        //            distToBegin = close;
        //        }
        //        else
        //        {
        //            if (!move.IsOneDirrection)
        //            {
        //                close = Vector3d.Distance(vect, move.EndNode.GetSimPosition());
        //                if (close < distToBegin)
        //                {
        //                    bestBegin = move.Reverse();
        //                    distToBegin = close;
        //                }
        //            }
        //        }
        //    }
        //    return bestBegin;
        //}

        //public SimRoute GetEndMovement(Vector3d vect, double maxDist)
        //{
        //    SimRoute bestEnd = null;
        //    double distToEnd = maxDist;
        //    foreach (SimRoute move in SimRoutes)
        //    {
        //        double close = Vector3d.Distance(vect, move.EndNode.GetSimPosition());
        //        if (close < distToEnd)
        //        {
        //            bestEnd = move;
        //            distToEnd = close;
        //        }
        //        else
        //        {
        //            if (!move.IsOneDirrection)
        //            {
        //                close = Vector3d.Distance(vect, move.StartNode.GetSimPosition());
        //                if (close < distToEnd)
        //                {
        //                    bestEnd = move.Reverse();
        //                    distToEnd = close;
        //                }
        //            }
        //        }
        //    }
        //    return bestEnd;
        //}

        //public SimPathStore(string regionFileName)
        //{
        //    RegionFileName = regionFileName;
        //    LoadFromFile();
        //}
        /// <summary>
        /// 
        /// </summary>
        void SaveToFile()
        {
            FileInfo save = new FileInfo(RegionFileName);
            if (save.Exists)
            {
                save.Delete();
            }
            SaveFile(RegionFileName);
            //FileStream stream = save.Open(FileMode.OpenOrCreate, FileAccess.Write);
            //StreamWriter sw = new StreamWriter(stream);
            //foreach (SimRoute sm in SimRoutes)
            //{
            //    sw.WriteLine(sm.ToFileString());
            //}
            //sw.Close();
        }
        /// <summary>
        /// 
        /// </summary>
        void LoadFromFile()
        {
            FileInfo read = new FileInfo(RegionFileName);
            if (!read.Exists)
            {
              //  Logger.Log("Not loading file " + RegionFileName, OpenMetaverse.Helpers.LogLevel.Info);
                return;
            }
            //FileStream stream = read.Open(FileMode.Open, FileAccess.Read);
            //TextReader tr = new StreamReader(stream);
            //while (tr.Peek() != -1)
            //{
            //    string s = tr.ReadLine();
            //    LoadFromLine(s);
            //}
            //tr.Close();
            LoadFile(RegionFileName);
        }

        //private void LoadFromLine(string s)
        //{
        //    SimRoute sm = new SimRoute(s);
        //    AddArc(sm);
        //}


        //internal static void EnsureKnown(SimWaypoint wp)
        //{
        //    if (Instance.SimWaypoints.Contains(wp)) return;
        //    Instance.SimWaypointsAdd(wp);
        //}
        /// <summary>
        /// 
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        internal bool SaveFile(string filename)
        {
            FileStream StreamWrite = (new FileInfo(filename)).OpenWrite();
            if (StreamWrite != null)
            {
                BinaryFormatter BinaryWrite = new BinaryFormatter();
                BinaryWrite.Serialize(StreamWrite, this);
                StreamWrite.Close();
                //Logger.Log("Success saving file " + RegionFileName, OpenMetaverse.Helpers.LogLevel.Info);
                return true;
            }
            //Logger.Log("Error saving file " + RegionFileName, OpenMetaverse.Helpers.LogLevel.Error);
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pathName"></param>
        /// <returns></returns>
        internal bool LoadFile(string pathName)
        {
            Stream StreamRead = (new FileInfo(pathName)).OpenRead();
            if (StreamRead != null)
            {
                BinaryFormatter BinaryRead = new BinaryFormatter();
                SimGlobalRoutes G = (SimGlobalRoutes)BinaryRead.Deserialize(StreamRead);
                StreamRead.Close();
                Clear();
                //   RegionFileName = pathName;
                SimRoutes = G.SimRoutes;
                SimWaypoints = G.SimWaypoints;
              //  Logger.Log("Loaded file " + RegionFileName, OpenMetaverse.Helpers.LogLevel.Info);
                return true;
            }
           // Logger.Log("Error loading file " + RegionFileName, OpenMetaverse.Helpers.LogLevel.Error);
            return false;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="v3"></param>
        /// <returns></returns>
        public SimWaypoint CreateClosestWaypoint(Vector3d v3)
        {
            double Dist;
            SimWaypoint Closest = ClosestNode(v3.X, v3.Y, v3.Z, out Dist, false);
            SimWaypoint V3Waypoint = SimWaypointImpl.CreateGlobal(v3);
            if (Closest != V3Waypoint)
            {
                IList<SimWaypoint> more = ClosestNodes(V3Waypoint, Dist, Dist * 2, false);
                AddNode(V3Waypoint);
                Intern2Arc(Closest, V3Waypoint, 1f);
                foreach (SimWaypoint P in more)
                {
                    Intern2Arc(P, V3Waypoint, 1f);
                }
            }
            return V3Waypoint;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="v3"></param>
        /// <param name="radius"></param>
        /// <param name="numPoints"></param>
        /// <param name="Weight"></param>
        /// <returns></returns>
        public SimWaypoint CreateClosestWaypointBox(Vector3d v3, double radius, int numPoints, double Weight)
        {
            SimWaypoint node = SimWaypointImpl.CreateGlobal(v3);
            double radiansStep = Math.PI * 2 / numPoints;
            SimWaypoint Last = node;
            Dictionary<SimWaypoint, List<SimWaypoint>> newWaypoints = new Dictionary<SimWaypoint, List<SimWaypoint>>();
            for (int Step = 0; Step < numPoints; Step++)
            {
                double ThisAngle = Step * radiansStep;
                Vector3d vectNew = (new Vector3d((double)Math.Cos(ThisAngle), (double)Math.Sin(ThisAngle), 0) * radius) + v3;
                SimWaypoint nodeNew = SimWaypointImpl.CreateGlobal(v3);
                List<SimWaypoint> closeNodes = new List<SimWaypoint>();
                newWaypoints[nodeNew] = closeNodes;
                double Dist;
                closeNodes.Add(node);
                closeNodes.Add(Last);
                closeNodes.Add(ClosestNode(vectNew.X, vectNew.Y, vectNew.Z, out Dist, false));
                Last = nodeNew;

            }
            foreach (SimWaypoint P in newWaypoints.Keys)
            {
                AddNode(P);
                foreach (SimWaypoint V in newWaypoints[P])
                {
                    if (V == null) continue;
                    AddNode(V);
                    if (P != V) Intern2Arc(P, V, Weight);
                }
            }
            return node;
        }
    }

    //public class PrimTracker
    //{
    //    protected double MovedAllot = 3.0f;
    //    Vector3d WayPoint;
    //    Quaternion Orientation;
    //    SimGlobalRoutes Store;
    //    public PrimTracker(MeshableObject firstP, Quaternion firtsR, SimGlobalRoutes store)
    //    {
    //        WayPoint = firstP.GetSimPosition();
    //        Store = store;
    //        Orientation = firtsR;
    //    }

    //    public void Update(Vector3d point, Quaternion rotation)
    //    {
    //        double dist = Vector3d.Distance(WayPoint, point);
    //        if (dist > MovedAllot)
    //        {
    //            MakeMovement(point);
    //        }
    //        else
    //            if (RotationDiffernt(rotation, Orientation))
    //            {
    //                MakeMovement(point);
    //                Orientation = rotation;
    //            }
    //    }

    //    private void MakeMovement(Vector3d point)
    //    {
    //        if (Vector3d.Distance(WayPoint, point) > MovedAllot / 3)
    //        {
    //            Console.WriteLine("WAYPOINT " + WayPoint + " -> " + point);
    //            SimWaypoint tieIn1 = Store.CreateClosestWaypoint(point);
    //            SimWaypoint tieIn2 = Store.CreateClosestWaypoint(WayPoint);
    //            Store.Intern2Arc(tieIn1, tieIn2, 0.01f); //Cheap
    //            WayPoint = point;
    //        }
    //    }

    //    static bool RotationDiffernt(Quaternion rotation, Quaternion Orientation)
    //    {
    //        Quaternion diff = rotation - Orientation;
    //        return (diff.Length() > 0.2);
    //    }
    //}
}
