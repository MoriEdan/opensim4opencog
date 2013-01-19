using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Cogbot;
using Cogbot.Utilities;
using MushDLR223.Utilities;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using PathSystem3D.Mesher;
using PathSystem3D.Navigation;
using System.Reflection;
using MushDLR223.ScriptEngines;
using Cogbot.Actions;

#if (COGBOT_LIBOMV || USE_STHREADS)
using ThreadPoolUtil;
using Thread = ThreadPoolUtil.Thread;
using ThreadPool = ThreadPoolUtil.ThreadPool;
using Monitor = ThreadPoolUtil.Monitor;
#endif
using System.Threading;


namespace Cogbot.World
{
    //TheSims-like object
    public class SimObjectImpl : SimPosition, BotMentalAspect, SimObject, MeshableObject, IEquatable<SimObjectImpl>,
                                 NotContextualSingleton, MixinSubObjects
    {
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            //if (obj.GetType() != typeof (SimObjectImpl)) return false;
            if (obj is SimObjectImpl && Equals((SimObjectImpl) obj))
            {
                return true;
            }
            return false;
        }

        private bool _confirmedObject;

        public bool ConfirmedObject
        {
            get { return _confirmedObject || _propertiesCache != null; }
            set { _confirmedObject = value; }
        }

        public ObjectMovementUpdate ObjectMovementUpdateValue;

        public SimPosition UsePosition
        {
            get
            {
                var pos = this;
                var finalDistance = pos.GetSizeDistance();

                if (finalDistance > 6) finalDistance = 6;
                else if (finalDistance < 1)
                {
                    return this;
                }


                //var vFinalLocation = pos.GlobalPosition;
                //vFinalLocation.X += v3.X;
                //vFinalLocation.Y += v3.Y;
                return new SimOffsetPosition(this, new Vector3(0, finalDistance, 0), true);
            }
        }

        public static implicit operator Primitive(SimObjectImpl m)
        {
            Primitive p = m.Prim;
            p.GetType();
            return p;
        }

        public static implicit operator SimObjectImpl(Primitive m)
        {
            return (SimObjectImpl) WorldObjects.GridMaster.GetSimObject(m);
        }

        public float ZHeading
        {
            get { return (float) (double) WorldObjects.GetZHeading(SimRotation); }
        }

        public SimHeading GetHeading()
        {
            lock (HasPrimLock)
                if (!IsRegionAttached && HasPrim)
                {
                    Simulator sim = WorldSystem.GetSimulator(RegionHandle);
                    Debug("Requesting object for heading");
                    sim.Client.Objects.RequestObject(sim, LocalID);
                    EnsureParentRequested(sim);
                }
            return new SimHeading(this);
        }


        public void AddInfoMap(object properties, string name)
        {
            lock (FILock)
            {
                if (WorldObjects.MaintainSimObjectInfoMap)
                {
                    _infoMap = _infoMap ?? new Dictionary<object, NamedParam>();
                    List<NamedParam> from = WorldObjects.GetMemberValues("", properties);
                    foreach (var o in from)
                    {
                        AddInfoMapItem(o);
                    }
                }
            }
            if (!WorldObjects.SendSimObjectInfoMap) return;

            WorldSystem.SendOnUpdateDataAspect(this, name, null, properties);
            WorldSystem.SendNewRegionEvent(SimEventType.DATA_UPDATE, "On" + name + "Update", this);
        }

        protected Primitive.ObjectProperties _propertiesCache { get; set; }

        public BotMentalAspect GetObject(string name)
        {
            return WorldSystem.GetObject(name);
        }

        public ulong RegionHandle { get; set; }
        public UUID ID { get; set; }

        [ConvertTo]
        public Primitive.ObjectProperties Properties
        {
            get
            {
                if (_propertiesCache == null)
                {
                    if (!HasPrim) return null;
                    Primitive Prim = this.Prim;
                    if (Prim.Properties != null)
                    {
                        UpdateProperties(Prim.Properties);
                    }
                    else
                    {
                        WorldObjects.EnsureSelected(Prim, GetSimulator());
                    }
                }
                return _propertiesCache;
            }
            set
            {
                UpdateProperties(value);
                lock (HasPrimLock)
                {
                    if (_Prim0 != null)
                    {
                        if (value.Name == null)
                        {
                            if (_Prim0.Properties != null)
                            {
                                _Prim0.Properties.SetFamilyProperties(value);
                                return;
                            }
                            _Prim0.Properties = new Primitive.ObjectProperties();
                            _Prim0.Properties.SetFamilyProperties(value);
                            return;
                        }
                        _Prim0.Properties = value;
                    }
                }
            }
        }

        public float GetCubicMeters()
        {
            Vector3 v3 = GetSimScale();
            return v3.X*v3.Y*v3.Z;
        }

        public SimObject GetGroupLeader()
        {
            if (!IsRoot)
            {
                var Parent = this.Parent;
                if (Parent != this && Parent != null) return Parent.GetGroupLeader();
            }
            List<SimObject> e = GetNearByObjects(GetSizeDistance() + 1, false);
            e.Add(this);
            e.Sort(CompareSize);
            return e[0];
        }

        public FirstOrderTerm GetTerm()
        {
            throw new NotImplementedException();
        }

        #region SimMover Members

        public virtual void IndicateRoute(IEnumerable<Vector3d> list, Color color)
        {
            throw new NotImplementedException("not really a mover!");
        }

        public virtual bool OpenNearbyClosedPassages()
        {
            throw new NotImplementedException("not really a mover!");
        }

        public virtual void ThreadJump()
        {
            throw new NotImplementedException("not really a mover!");
        }

        public virtual void StopMoving()
        {
            StopMoving(false);
        }

        public virtual void StopMoving(bool fullStop)
        {
            throw new NotImplementedException("not really a mover!");
        }

        public virtual void StopMovingIsProblem()
        {
            throw new NotImplementedException("not really a mover!");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="finalTarget"></param>
        /// <param name="maxDistance"></param>
        /// <param name="maxSeconds"></param>
        /// <returns></returns>
        public virtual bool SimpleMoveTo(Vector3d finalTarget, double maxDistance, float maxSeconds, bool stopAtEnd)
        {
            double currentDist = DistanceNoZ(finalTarget, GlobalPosition);
            if (currentDist < maxDistance) return true;
            {
                SimWaypoint P = SimWaypointImpl.CreateGlobal(finalTarget);
                SetMoveTarget(P, (float) maxDistance);
            }
            for (int i = 0; i < maxSeconds; i++)
            {
                Application.DoEvents();
                currentDist = DistanceNoZ(finalTarget, GlobalPosition);
                if (currentDist > maxDistance)
                {
                    Thread.Sleep(1000);
                    continue;
                }
                else
                {
                    // StopMoving();
                    return true;
                }
            }
            StopMovingIsProblem();
            return false;
        }

        protected BotClient Client0;

        public virtual BotClient Client
        {
            get
            {
                if (Client0 != null)
                {
                    return Client0;
                }
                if (RegionHandle != 0)
                {
                    return ClientManager.GetBotByGridClient(GetSimulator().Client);
                }
                return WorldSystem.client;
            }
        }

        public virtual void Touch(SimObject simObject)
        {
            Client.Self.Touch(simObject.Prim.LocalID);
        }

        #endregion

        public SimPathStore PathStore
        {
            get
            {
                SimRegion R = GetSimRegion(); //
                if (R == null) return null;
                var ps = R.GetPathStore(SimPosition);
                if (IsAvatar && (this is SimMover) && IsControllable)
                {
                    ps.LastSimMover = (SimMover) this;
                }
                return ps;
            }
        }

        public virtual bool TurnToward(SimPosition targetPosition)
        {
            return TurnToward(targetPosition.GlobalPosition);
            //SendUpdate(0);
        }

        public virtual void SendUpdate(int ms)
        {
            Thread.Sleep(ms);
        }

        public virtual void SetMoveTarget(SimPosition target, double maxDistance)
        {
            //SimRegion R = target.GetSimRegion();
            //if (R != GetSimRegion())
            //{
            //    TeleportTo(R,target.GetSimPosition());
            //}
            Vector3d finalPos = target.GlobalPosition;
            Vector3d start = GlobalPosition;
            Vector3d offset = finalPos - start;
            double points = offset.Length();
            Vector3d offsetEach = offset/points;
            while (points > 1)
            {
                points -= 1;
                start += offsetEach;
                SetObjectPosition(start);
            }
            SetObjectPosition(finalPos);
        }


        public virtual bool SalientGoto(SimPosition pos)
        {
            return GotoTargetAStar(pos);
        }

        /// <summary>
        /// Used to be 9 now its 4 times
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public bool GotoTargetAStar(SimPosition pos)
        {
            if (!IsControllable)
            {
                throw Error("GotoTarget !IsControllable");
            }
            BotClient Client = WorldSystem.client;
            float maxDist = pos.GetSizeDistance();
            for (int i = 0; i < 4; i++)
            {
                IndicateTarget(pos, true);
                bool result = FollowPathTo(pos, maxDist);
                if (result)
                {
                    try
                    {
                        SetMoveTarget(pos, maxDist);
                    }
                    finally
                    {
                        IndicateTarget(pos, false);
                    }
                    return true;
                }
            }
            IndicateTarget(pos, false);
            return false;
        }

        protected void IndicateTarget(SimPosition pos, bool tf)
        {
            return;
            CMDFLAGS needResult = CMDFLAGS.Backgrounded;
            BotClient Client = WorldSystem.client;
            if (tf)
            {
                SimObject obj = pos as SimObject;
                if (obj != null)
                {
                    Client.ExecuteCommand("pointat " + obj.ID, this, Debug, needResult);
                }
                else
                {
                    var vFinalLocation = pos.UsePosition.GlobalPosition;
                    Client.ExecuteCommand("pointat " + vFinalLocation.ToRawString(), this, Debug, needResult);
                }
            }
            else Client.ExecuteCommand("pointat", this, Debug, needResult);
        }

        public virtual bool IsControllable
        {
            get
            {
                if (!IsRoot) return false;
                return HasPrim; // WorldSystem.client.Network.CurrentSim == GetSimRegion().TheSimulator;
            }
        }

        public bool FollowPathTo(SimPosition globalEnd, double distance)
        {
            if (!IsControllable)
            {
                throw Error("FollowPathTo !IsControllable");
            }
            SimAbstractMover move = SimAbstractMover.CreateSimPathMover((SimActor) this, globalEnd, distance);
            try
            {
                move.OnMoverStateChange += OnMoverStateChange;
                return move.Goto() == SimMoverState.COMPLETE;
            }
            finally
            {
                move.OnMoverStateChange -= OnMoverStateChange;
            }
        }

        protected virtual void OnMoverStateChange(SimMoverState obj)
        {
        }


        public virtual bool TeleportTo(SimPosition local)
        {
            if (!IsControllable)
            {
                throw Error("GotoTarget !Client.Self.AgentID == Prim.ID");
            }
            return TeleportTo(SimRegion.GetRegion(SimRegion.GetRegionHandle(local.PathStore)), local.SimPosition);
        }

        public virtual bool TeleportTo(Vector3d finalTarget)
        {
            if (!IsControllable)
            {
                throw Error("GotoTarget !Client.Self.AgentID == Prim.ID");
            }
            return TeleportTo(SimWaypointImpl.CreateGlobal(finalTarget));
        }

        public virtual bool TeleportTo(SimRegion R, Vector3 local)
        {
            StopMoving(true);
            return SetObjectPosition(R.LocalToGlobal(local));
        }

        public bool SetObjectPosition(Vector3d globalPos)
        {
            Vector3d start = GlobalPosition;
            Vector3d offset = globalPos - start;
            Vector3 lPos = SimPosition;
            lPos.X += (float) offset.X;
            lPos.Y += (float) offset.Y;
            lPos.Z += (float) offset.Z;
            return SetObjectPosition(lPos);
        }

        public virtual bool SetObjectPosition(Vector3 localPos)
        {
            if (!IsRoot)
            {
                Vector3 start = SimPosition;
                Vector3 offset = localPos - start;
                SimObject p = Parent;
                return p.SetObjectPosition(p.SimPosition + offset);
            }
            WorldSystem.SetObjectPosition(Prim, localPos);
            return true;
        }

        #region SimMover Members

        public virtual bool TurnToward(Vector3d targetPosition)
        {
            if (!IsControllable)
            {
                throw Error("GotoTarget !Client.Self.AgentID == Prim.ID");
            }
            Vector3 local = GetLocalTo(targetPosition);
            return TurnToward(local);
        }

        protected Vector3 GetLocalTo(Vector3d targetPosition)
        {
            Vector3d Current = GlobalPosition;
            Vector3d diff = targetPosition - Current;
            int maxReduce = 10;
            while (diff.Length() > 10 && maxReduce-- > 0)
            {
                diff.X *= 0.75f;
                diff.Y *= 0.75f;
                diff.Z *= 0.75f;
            }
            return SimPosition + new Vector3((float) diff.X, (float) diff.Y, (float) diff.Z);
        }

        #endregion

        public virtual bool TurnToward(Vector3 target)
        {
            if (!IsControllable)
            {
                throw Error("GotoTarget !Client.Self.AgentID == Prim.ID");
            }
            Quaternion parentRot = Quaternion.Identity;

            if (!IsRoot)
            {
                parentRot = Parent.SimRotation;
            }

            Quaternion between = Vector3.RotationBetween(Vector3.UnitX, Vector3.Normalize(target - SimPosition));
            Quaternion rot = between*(Quaternion.Identity/parentRot);

            SetObjectRotation(rot);
            return true;
        }

        public virtual bool SetObjectRotation(Quaternion localPos)
        {
            if (!IsRoot)
            {
                Quaternion start = SimRotation;
                Quaternion offset = localPos/start;
                SimObject p = Parent;
                return p.SetObjectRotation(p.SimRotation*offset);
            }
            WorldSystem.SetObjectRotation(Prim, localPos);
            return true;
        }

        // protected SimRegion PathStore;
        public Box3Fill OuterBox
        {
            get
            {
                var _Mesh = PathFinding._Mesh;
                if (_Mesh != null)
                {
                    return _Mesh.OuterBox;
                }
                return null;
            }
        }

        public uint LocalID
        {
            get
            {
                var Prim = this.Prim;
                if (Prim == null) return 0;
                return Prim.LocalID;
            }
        }

        public uint ParentID
        {
            get
            {
                var Prim = this.Prim;
                if (Prim == null) return 0;
                return Prim.ParentID;
            }
        }

        public virtual bool KilledPrim(Primitive primitive, Simulator simulator)
        {

            lock (_primRefs)
            {
                _primRefs.Remove(primitive);
                IsKilled = _primRefs.Count < 1;
                if (ReferenceEquals(_Prim0, primitive))
                {
                    _Prim0 = null;
                    RequestedParent = false;
                    Parent = null;
                }
                return WasKilled;
            }
        }

        private Dictionary<object, NamedParam> _infoMap = null; //new Dictionary<object, NamedParam>(); 

        public ICollection<NamedParam> GetInfoMap()
        {
            lock (FILock)
            {
                if (!WorldObjects.MaintainSimObjectInfoMap) return null;
                if (_infoMap == null) _infoMap = new Dictionary<object, NamedParam>();
                return new List<NamedParam>(_infoMap.Values);
            }
        }

        public void SetInfoMap(object target, string key, MemberInfo type, object value)
        {
            if (!WorldObjects.MaintainSimObjectInfoMap) return;
            if (value == null) value = new NullType(this, type);
            AddInfoMapItem(new NamedParam(target, type, key, null, value));
        }

        public void AddInfoMapItem(NamedParam ad)
        {
            lock (FILock)
            {
                _infoMap = _infoMap ?? new Dictionary<object, NamedParam>();
            }
            lock (_infoMap)
                _infoMap[ad.Key] = ad;
        }

        internal void PollForPrim(WorldObjects worldObjects, Simulator sim)
        {
            if (sim == null)
            {
                return;
            }
            Primitive A = worldObjects.GetLibOMVHostedPrim(ID, sim, false);
            if (A != null) this.SetFirstPrim(A);
        }

        private readonly List<Primitive> _primRefs = new List<Primitive>();

        public virtual void ResetPrim(Primitive prim, BotClient bc, Simulator sim)
        {
            if (prim == null) return;
            lock (HasPrimLock)
                if (_Prim0 == null)
                {
                    SetFirstPrim(prim);
                    return;
                }

            Primitive.ObjectProperties properties = prim.Properties;
            bool updateCarriesProperties = properties != null;
            if (updateCarriesProperties) _propertiesCache = properties;
            if (prim.RegionHandle != _Prim0.RegionHandle || !Object.ReferenceEquals(prim, _Prim0))
            {
                lock (_primRefs)
                {
                    bool found = false;
                    foreach (Primitive av in _primRefs)
                    {
                        if (Object.ReferenceEquals(av, prim))
                        {
                            found = true;
                        }
                    }
                    if (!found)
                    {
                        if (prim.ID != ID)
                        {
                            DLRConsole.DebugWriteLine("ERROR: Different UUID! {0}", prim);
                        }
                        _primRefs.Add(prim);
                        //DLRConsole.WriteLine("\n Different prims {0}", prim);
                    }
                }
                lock (HasPrimLock) _Prim0 = prim;
                if (needUpdate)
                {
                    if (updateCarriesProperties) UpdateProperties(properties);
                }
                ResetRegion(prim.RegionHandle);
            }
            if (sim != null) ResetRegion(sim.Handle);
        }

        public virtual void ResetRegion(ulong regionHandle)
        {
            RegionHandle = regionHandle;
            lock (HasPrimLock)
                if (!HasPrim || _Prim0.RegionHandle != regionHandle)
                {
                    lock (_primRefs)
                    {
                        foreach (Primitive av in _primRefs)
                        {
                            if (av.RegionHandle == regionHandle) _Prim0 = av;
                        }
                    }
                }
        }

        /// <summary>
        /// Right now only sees if TouchName has been defined - need a relable way to see if script is defined.
        /// </summary>
        public bool IsTouchDefined
        {
            get
            {
                Primitive Prim = this.Prim;
                if (Prim != null && (Prim.Flags & PrimFlags.Touch) != 0) return true;
                if (_propertiesCache != null)
                    return !String.IsNullOrEmpty(_propertiesCache.TouchName);
                return false;
            }
        }

        /// <summary>
        /// Need a more relable way to see if script is defined.
        /// </summary>
        public bool IsSitDefined
        {
            get
            {
                Primitive Prim = this.Prim;
                if (Prim != null && Prim.ClickAction == ClickAction.Sit) return true;
                if (_propertiesCache != null)
                    return !String.IsNullOrEmpty(_propertiesCache.SitName);
                return false;
            }
        }

        public bool IsSculpted
        {
            get
            {
                Primitive Prim = this.Prim;
                return Prim != null && Prim.Sculpt != null;
            }
        }

        private bool _Passable;
        private bool _PassableKnown = false;

        public virtual bool IsPassable
        {
            get
            {
                if (_PassableKnown) return _Passable;

                if (IsPhantom) return true;
                if (Affordances.IsTypeOf(SimTypeSystem.PASSABLE) != null)
                {
                    IsPassable = true;
                    return true;
                }

                IsPassable = false;
                return _Passable;
                // unused for now
                if (IsRoot || true) return false;
                if (!IsRoot && IsRegionAttached) return Parent.IsPassable;
                if (Parent == null) return true;
                return Parent.IsPassable;
            }
            set
            {
                if (_PassableKnown)
                {
                    if (value && !_Passable && !WorldSystem.CanPhantomize)
                    {
                        Debug("Wont set IsPassable because WorldObjects.CanPhantomize=false");
                        return;
                    }
                }
                _PassableKnown = true;
                _Passable = value;
                SimMesh _Mesh = PathFinding._Mesh;
                if (_Mesh != null && _Mesh.IsSolid == value)
                {
                    _Mesh.IsSolid = !value;
                }
            }
        }

        public virtual bool IsTemporary
        {
            get
            {
                Primitive p = _Prim0;
                return (p != null && ((p.Flags & PrimFlags.Temporary) != 0 || (p.Flags & PrimFlags.TemporaryOnRez) != 0));
            }
            set
            {
                if (IsTemporary == value) return;
                Primitive Prim = this.Prim;
                if (Prim == null)
                {
                    Debug("Wont set IsTemporary because Prim==null");
                    return;
                }
                if (value)
                {
                    WorldSystem.SetPrimFlags(Prim, (PrimFlags) (Prim.Flags | PrimFlags.Temporary));
                    PathFinding.MadeNonTemp = false;
                }
                else
                {
                    WorldSystem.SetPrimFlags(Prim, (PrimFlags) (Prim.Flags - PrimFlags.Temporary));
                    PathFinding.MadeNonTemp = true;
                }
            }
        }

        public virtual bool IsPhantom
        {
            get
            {
                if (PathFinding.MadePhantom) return true;
                Primitive Prim = this.Prim;
                if (Prim == null) return true;

                if (IsRoot || WorldObjects.IsOpenSim)
                {
                    return (Prim.Flags & PrimFlags.Phantom) == PrimFlags.Phantom;
                }
                if (!IsRoot && IsRegionAttached && Parent != this)
                {
                    return Parent.IsPhantom;
                }
                return (Prim.Flags & PrimFlags.Phantom) == PrimFlags.Phantom;
            }
            set
            {
                if (IsPhantom == value) return;
                if (!WorldSystem.CanPhantomize)
                {
                    Debug("Wont set IsPhantom because WorldObjects.CanPhantomize=false");
                    return;
                }
                Primitive Prim = this.Prim;
                if (Prim == null)
                {
                    Debug("Wont set IsPhantom because Prim==null");
                    return;
                }
                if (value)
                {
                    WorldSystem.SetPrimFlags(Prim, (PrimFlags) (Prim.Flags | PrimFlags.Phantom));
                    PathFinding.MadePhantom = true;
                }
                else
                {
                    WorldSystem.SetPrimFlags(Prim, (PrimFlags) (Prim.Flags - PrimFlags.Phantom));
                    PathFinding.MadePhantom = false;
                }
            }
        }

        public bool IsPhysical
        {
            get
            {
                if (!IsRoot) return Parent.IsPhysical;
                return (Prim.Flags & PrimFlags.Physics) == PrimFlags.Physics;
            }
            set
            {
                if (IsPhysical == value) return;
                if (value)
                {
                    WorldSystem.SetPrimFlags(Prim, (PrimFlags) (Prim.Flags | PrimFlags.Physics));
                    PathFinding.MadeNonPhysical = false;
                }
                else
                {
                    WorldSystem.SetPrimFlags(Prim, (PrimFlags) (Prim.Flags - PrimFlags.Physics));
                    PathFinding.MadeNonPhysical = true;
                }
            }
        }

        public bool InventoryEmpty
        {
            get { lock (HasPrimLock) return (HasPrim && (Prim.Flags & PrimFlags.InventoryEmpty) != 0); }
        }

        public bool TaskInventoryLikely
        {
            get
            {
                var Prim = this._Prim0;
                if (Prim == null) return false;
                bool mightHaveTaskInv = false;
                if (objectinventory != null && objectinventory.Count > 0)
                {
                    if (objectinventory[0].Name != "Contents") return true;
                    if (objectinventory.Count > 1) return true;
                }
                const PrimFlags maybeScriptsInside = PrimFlags.AllowInventoryDrop | PrimFlags.Scripted | PrimFlags.Touch;
                if ((maybeScriptsInside & Prim.Flags) != 0)
                {
                    mightHaveTaskInv = true;
                }
                if ((Prim.ClickAction == ClickAction.Sit))
                {
                    mightHaveTaskInv = true;
                }
                var props = Properties;
                if (props != null && (!string.IsNullOrEmpty(props.SitName) || !string.IsNullOrEmpty(props.TouchName)))
                {
                    mightHaveTaskInv = true;
                }
                if ((Prim.Flags & PrimFlags.InventoryEmpty) != 0)
                {
                    if (!mightHaveTaskInv) return false;
                    return false;
                }
                return mightHaveTaskInv;
            }
        }

        public bool Sandbox
        {
            get { lock (HasPrimLock) return HasPrim && (Prim.Flags & PrimFlags.Sandbox) != 0; }
        }

        public bool Temporary
        {
            get { lock (HasPrimLock) return HasPrim && (Prim.Flags & PrimFlags.Temporary) != 0; }
        }

        public virtual bool Flying
        {
            get { lock (HasPrimLock) return HasPrim && (Prim.Flags & PrimFlags.Flying) != 0; }
            set
            {
                if (Flying != value)
                {
                    lock (HasPrimLock)
                        if (IsControllable)
                        {
                            Prim.Flags = (Prim.Flags | PrimFlags.Flying);
                            //WorldSystem.client.Objects.SetFlags();
                        }
                }
            }
        }

        public bool AnimSource
        {
            get { lock (HasPrimLock) return HasPrim && (Prim.Flags & PrimFlags.AnimSource) != 0; }
        }

        public bool AllowInventoryDrop
        {
            get { lock (HasPrimLock) return HasPrim && (Prim.Flags & PrimFlags.AllowInventoryDrop) != 0; }
        }

        public bool IsAvatar
        {
            get { return this is SimAvatar || _Prim0 is Avatar; }
        }

        //Vector3d lastPos;
        //SimWaypoint swp;
        //public virtual SimWaypoint GetWaypoint()
        //{
        //    Vector3d v3 = GlobalPosition();

        //    if (swp == null || !swp.Passable)
        //    {
        //        SimRegion PathStore = GetSimRegion();
        //        swp = PathStore.CreateClosestWaypoint(v3);//, GetSizeDistance() + 1, 7, 1.0f);
        //        if (!swp.Passable)
        //        {
        //            double dist = Vector3d.Distance(v3, swp.GlobalPosition());
        //            swp.EnsureAtLeastOnePath();
        //            WorldSystem.WriteLine("CreateClosestWaypoint: " + v3 + " <- " + dist + " -> " + swp + " " + this);
        //        }
        //        if (lastPos != v3)
        //        {
        //            lastPos = v3;
        //            List<ISimObject> objs = GetNearByObjects(3f, false);
        //            foreach (ISimObject O in objs)
        //            {
        //                O.UpdateBlocked(PathStore);
        //            }
        //        }
        //        if (!swp.Passable)
        //        {
        //            double dist = Vector3d.Distance(v3, swp.GlobalPosition());
        //            WorldSystem.WriteLine("BAD: " + v3 + " <- " + dist + " -> " + swp + " " + this);
        //            swp = PathStore.ClosestNode(v3.X, v3.Y, v3.Y, out dist, false);//, GetSizeDistance() + 1, 7, 1.0f);
        //        }
        //    }
        //    return swp;
        //    //            return PathStore.CreateClosestWaypointBox(v3, 4f);
        //}


        public double ZDist(SimPosition self)
        {
            return ZDistance(GlobalPosition, self.GlobalPosition);
        }

        public double Distance(SimPosition prim)
        {
            if (prim == null || !prim.IsRegionAttached) return 1300;
            if (!IsRegionAttached) return 1300;
            Vector3d primGlobalPosition = prim.GlobalPosition;
            Vector3d GlobalPosition;
            if (TryGetGlobalPosition(out GlobalPosition))
            {
                double d = DistanceNoZ(GlobalPosition, primGlobalPosition);
                Vector3d use = prim.UsePosition.GlobalPosition;
                double d1 = DistanceNoZ(use, GlobalPosition);
                return Math.Min(d, d1);
            }
            return 1200;
        }

        public static double DistanceNoZ(Vector3d a, Vector3d b)
        {
            return SimPathStore.DistanceNoZ(a, b);
        }

        public static double ZDistance(Vector3d a, Vector3d b)
        {
            return Math.Abs(a.Z - b.Z);
        }

        // the prim in Secondlife
        public Primitive _Prim0;

        public Primitive Prim0
        {
            get { return _Prim0; }
        }

        public Primitive Prim
        {
            get
            {
                lock (HasPrimLock)
                {
                    if (_Prim0 == null)
                    {
                        if (RegionHandle != 0)
                        {
                            Simulator S = WorldSystem.GetSimulator(RegionHandle);
                            if (S != null)
                            {
                                var found = WorldSystem.GetLibOMVHostedPrim(ID, S, false);
                                if (found == null) return null;
                                SetFirstPrim(found);
                                return found;
                            }
                        }
                        return null;
                    }
                    if (_Prim0.RegionHandle != RegionHandle)
                    {
                        if (RegionHandle != 0)
                            ResetRegion(RegionHandle);
                    }
                    if (RegionHandle == 0)
                    {
                        RegionHandle = _Prim0.RegionHandle;
                    }
                    return _Prim0;
                }
            }
            // set { _Prim0 = value; }
        }

        //{
        //    get { return base.Prim; }
        //    set { Prim = value; }
        //}
        public WorldObjects WorldSystem;

        private bool needUpdate = true;

        public bool NeedsUpdate
        {
            get { return needUpdate; }
        }

        public bool EnsureProperties(TimeSpan block)
        {
            if (needUpdate)
            {
                if (_propertiesCache != null)
                {
                    UpdateProperties(_propertiesCache);
                    return true;
                }
                else
                {
                    if (RegionHandle != 0 && _Prim0 != null)
                    {
                        Simulator simulator = GetSimulator();
                        if (!WorldObjects.NeverSelect(LocalID, simulator)) return true;
                        return WaitForUpdate(() => WorldObjects.ReallyEnsureSelected(GetSimulator(), LocalID), block);
                    }
                    return false;
                }
            }
            else
            {
                return ForceUpdateIfOld(block);
            }
        }

        private bool WaitForUpdate(Action action, TimeSpan block)
        {
            if (block == default(TimeSpan) || block == TimeSpan.MinValue)
            {
                action();
                return true;
            }
            var mre = new ManualResetEvent(false);
            EventHandler<ObjectPropertiesEventArgs> foo = (s, e) =>
                                                              {
                                                                  if (e.Properties.ObjectID == ID)
                                                                  {
                                                                      try
                                                                      {
                                                                          mre.Set();
                                                                      }
                                                                      catch (Exception)
                                                                      {
                                                                      }
                                                                  }
                                                              };
            Client.Objects.ObjectProperties += foo;
            try
            {
                action();
                return mre.WaitOne(block);
            }
            finally
            {
                Client.Objects.ObjectProperties -= foo;
            }
        }

        [ConfigSetting(Description = "how long properties last before being considered needing re-select")] public static TimeSpan PropertiesStaleTime = TimeSpan.FromSeconds(30);
        private DateTime LastUpdateTime = DateTime.MinValue;

        public bool ForceUpdateIfOld(TimeSpan block)
        {
            if (DateTime.Now.Subtract(LastUpdateTime) < PropertiesStaleTime) return true;
            return ForceUpdate(block);
        }

        public bool ForceUpdate(TimeSpan block)
        {

            if (RegionHandle != 0 && _Prim0 != null)
            {
                needUpdate = true;
                _propertiesCache = null;
                return WaitForUpdate(() => WorldObjects.ReallyEnsureSelected(GetSimulator(), LocalID), block);
            }
            return false;
        }

        protected bool WasKilled;

        public virtual bool IsKilled
        {
            // get { return WasKilled; }
            set
            {
                if (WasKilled != value) //already
                {
                    IsDebugging = true;
                    WasKilled = value;
                    var AttachedChildren0 = Children;
                    lock (AttachedChildren0)
                        foreach (SimObject C in AttachedChildren0)
                        {
                            C.IsKilled = value;
                        }
                    if (WasKilled) PathFinding.RemoveCollisions();
                }
            }
            get { return WasKilled; }
        }

        public SimObjectPathFindingImpl PathFinding { get; set; }


        protected ListAsSet<SimObject> _children = new ListAsSet<SimObject>();

        public ListAsSet<SimObject> Children
        {
            get { return _children; }
        }

        public bool HasChildren
        {
            get { return _children.Count > 0; }
        }

        public SimObjectImpl(UUID id, WorldObjects objectSystem, Simulator sim)
            //: base(prim.ID.ToString())
            // : base(prim, SimRegion.SceneProviderFromSimulator(sim))
        {
            //ActionEventQueue = new Queue<SimObjectEvent>(MaxEventSize);
            ID = id;
            PathFinding = new SimObjectPathFindingImpl {thiz = this};
            if (sim != null) RegionHandle = sim.Handle;
            WorldSystem = objectSystem;
            var ot = SimTypeSystem.CreateInstanceType(id.ToString());
            ot.ID = id;
            Affordances = new SimObjectAffordanceImpl
                              {
                                  thiz = this,
                                  ObjectType = ot
                              };
            ot.IsObjectTop = true;

            //_CurrentRegion = SimRegion.GetRegion(sim);
            // PathStore = GetSimRegion();
            //WorldSystem.EnsureSelected(prim.ParentID,sim);
            // Parent; // at least request it
        }

        public virtual void SetFirstPrim(Primitive prim)
        {
            lock (HasPrimLock)
                if (prim != null)
                {
                    if (ID == UUID.Zero) ID = prim.ID;
                    _Prim0 = prim;
                    if (prim.RegionHandle != 0)
                    {
                        RegionHandle = prim.RegionHandle;
                    }
                    lock (_primRefs)
                    {
                        if (!_primRefs.Contains(prim))
                        {
                            _primRefs.Add(prim);
                            int tries = 3;
                            WorldSystem.DelayedEval(() => (prim.Type != PrimType.Unknown), () =>
                                                                                               {
                                                                                                   AddInfoMap(prim,
                                                                                                              "Primitive");
                                                                                               }, tries);
                        }
                    }
                    if (prim.Properties != null)
                    {
                        // Properties = prim.Properties;
                        Properties = prim.Properties;
                    }

                    ToSubCollection(prim).AddTo(this);

                    if (WorldPathSystem.MaintainSimCollisions(prim.RegionHandle) && prim.Sculpt != null &&
                        WorldPathSystem.SculptCollisions)
                    {
                        WorldSystem.StartTextureDownload(prim.Sculpt.SculptTexture);
                    }
                    // TaskInvGrabber.Enqueue(StartGetTaskInventory);
                }
        }

        private ListAsSet<SimObject> ToSubCollection(Primitive prim)
        {
            if ((prim is Avatar))
            {
                return WorldObjects.SimAvatars;
            }            
            if (this is SimAvatar) return WorldObjects.SimAvatars;
            if (IsAttachmentRoot || IsChildOfAttachment)
            {
                return WorldObjects.SimAttachmentObjects;
            }
            if (prim.ParentID != 0)
            {
                return WorldObjects.SimChildObjects;
            }
            return WorldObjects.SimRootObjects;
        }

        protected SimObject __Parent;
        protected SimObject _Parent
        {
            get { return __Parent; }
            set { __Parent = value; }
        } // null means unknown if we IsRoot then Parent == this;
        protected bool IsParentKnown { get; set; } // null means unknown if we IsRoot then Parent == this;

        public virtual SimObject Parent
        {
            get
            {
                if (IsParentKnown) return _Parent;
                var Prim = this.Prim;
                if (_Parent == null)
                {
                    if (Prim == null) return null;
                    uint parentID = Prim.ParentID;
                    if (parentID == 0)
                    {
                        IsParentKnown = true;
                    }
                    else
                    {
                        Simulator simu = GetSimulator();
                        Primitive prim = WorldSystem.GetPrimitive(parentID, simu);
                        if (prim == null)
                        {
                            // missing prim?!
                            IsDebugging = true;
                            // try to request for next time
                            EnsureParentRequested(simu);
                            return _Parent;
                        }
                        IsParentKnown = true;
                        Parent = WorldSystem.GetSimObject(prim, simu);
                    }
                }
                if (Prim != null && Prim.ParentID == 0)
                {
                    IsParentKnown = true;
                }
                return _Parent;
            }
            set
            {
                IsParentKnown = true;
                if (value == _Parent) return;
                SetInfoMap(this, "Parent", GetType().GetProperty("Parent"), value);
                if (value == null)
                {
                    _isChild = false;
                    _Parent.Children.Remove(this);
                }
                else if (value != this)
                {
                    _isChild = true;
                    if (value.Children.AddTo(this))
                    {
                        needUpdate = true;
                        SimObjectImpl simObject = (SimObjectImpl) value;
                        simObject.needUpdate = true;
                    }
                    _Parent = value;
                } else
                {
                    throw new InvalidOperationException("Cant be its onwn parent " + this);
                }
                if (_Parent != null && _Parent.Prim != null) RequestedParent = true;
            }
        }

        public bool AddChild(SimObject simO)
        {
            SimObjectImpl simObject = (SimObjectImpl) simO;
            needUpdate = true;
            simObject._Parent = this;
            simObject.IsParentKnown = true;
            simObject.needUpdate = true;
            bool b = Children.AddTo(simObject);
            return b;
        }

        public virtual bool IsRoot
        {
            get
            {
                if (WasKilled) return false;
                var Prim = this._Prim0;
                if (Prim == null || Prim.ParentID == 0) return true;
                IsParentAccruate(Prim);
                // _Parent = Parent;
                return false;
            }
        }

        public virtual string DebugInfo()
        {
            string str = ToString();
            var Prim = this.Prim;
            if (Prim == null || !HasPrim) return str;
            if (Prim.ParentID != 0)
                return Prim.ParentID + " " + str;
            return str;
        }


        public List<string> GetMenu(SimAvatar avatar)
        {
            //props.Permissions = new Permissions(objectData.BaseMask, objectData.EveryoneMask, objectData.GroupMask,
            //  objectData.NextOwnerMask, objectData.OwnerMask);
            List<string> list = new List<string>();
            if (_propertiesCache != null)
            {
                //  if (theProperties.TextName != "")
                list.Add("grab");
                //   if (theProperties.SitName != "")
                list.Add("sit");
                PermissionMask mask = _propertiesCache.Permissions.EveryoneMask;
                if (Prim.OwnerID == avatar.theAvatar.ID)
                {
                    mask = _propertiesCache.Permissions.OwnerMask;
                }
                PermissionMask result = mask | _propertiesCache.Permissions.BaseMask;
                if ((result & PermissionMask.Copy) != 0)
                    list.Add("copy");
                if ((result & PermissionMask.Modify) != 0)
                    list.Add("modify");
                if ((result & PermissionMask.Move) != 0)
                    list.Add("move");
                if ((result & PermissionMask.Transfer) != 0)
                    list.Add("transfer");
                if ((result & PermissionMask.Damage) != 0)
                    list.Add("damage");
            }
            return list;
        }

        // This field is supposed to be the most recent property udate 
        private Primitive.ObjectProperties MostRecentPropertyUpdate;
        private readonly object MostRecentPropertyUpdateLock = new object();

        private void UpdateProperties(Primitive.ObjectProperties objectProperties)
        {
            if (objectProperties == null)
            {
                Debug("NULL PROPS!!?!");
                return;
            }
            if (!_confirmedObject)
            {
                _confirmedObject = true;
                Debug("Now confirmed!!");
            }
            lock (toStringLock)
            {
                toStringNeedsUpdate = true;
                _TOSRTING = null;
            }
            lock (MostRecentPropertyUpdateLock)
            {
                if (objectProperties.Name == null)
                {
                    if (MostRecentPropertyUpdate == null)
                    {
                        MostRecentPropertyUpdate = new Primitive.ObjectProperties();
                    }
                    MostRecentPropertyUpdate.SetFamilyProperties(objectProperties);
                }
                else
                {
                    MostRecentPropertyUpdate = objectProperties;
                }
            }
            WorldObjects.UpdateObjectData.Enqueue(UpdateProperties0);
        }

        private void UpdateProperties0()
        {
            Primitive.ObjectProperties objectProperties = null;
            lock (MostRecentPropertyUpdateLock)
            {
                if (MostRecentPropertyUpdate == null) return; // something allready did the work            
                objectProperties = MostRecentPropertyUpdate;
                MostRecentPropertyUpdate = null;
            }
            try
            {
                Primitive Prim = this.Prim;
                IsParentAccruate(Prim);
                lock (toStringLock)
                {
                    toStringNeedsUpdate = true;
                    _TOSRTING = null;
                }
                if (objectProperties != null)
                {
                    _propertiesCache = objectProperties;
                    if (Prim != null && Prim.Properties == null) Prim.Properties = objectProperties;
                    needUpdate = false;
                    LastUpdateTime = DateTime.Now;
                    Affordances.UpdateFromProperties(objectProperties);
                    AddInfoMap(objectProperties, "ObjectProperties");
                }
            }
            catch (Exception e)
            {
                Debug("" + e);
            }
        }

        protected bool IsParentAccruate(Primitive child)
        {
            return true;
            //return _Parent != null;
            if (child == null) return false;
            if (child.ParentID == 0)
            {
                _Parent = this;
                return true;
            }
            if (_Parent != null)
            {
                if (_Parent.Prim == null || _Parent.Prim.LocalID == child.ParentID) return true;
                _Parent = null;
                Debug("Nulling parent");
                return false;
            }
            return true;
            //throw new NotImplementedException();
        }

        public virtual void UpdateObject(ObjectMovementUpdate objectUpdate, ObjectMovementUpdate objectUpdateDiff)
        {
            if (needUpdate)
            {
                if (_propertiesCache != null) UpdateProperties(_propertiesCache);
            }
            PathFinding.UpdateCollisions();
            lock (toStringLock)
            {
                toStringNeedsUpdate = true;
                _TOSRTING = null;
            }
        }


        private string _TOSRTING;
        private readonly object toStringLock = new object();

        public override string ToString()
        {
            lock (toStringLock) return ToStringReal();
        }

        public string ToStringReal()
        {
            //  string _TOSRTING = this._TOSRTING;

            if (needUpdate || _TOSRTING == null || toStringNeedsUpdate)
            {
                Primitive Prim = null;
                lock (HasPrimLock)
                {
                    Prim = this.Prim;
                    if (!HasPrim || Prim == null) return "UNATTACHED_PRIM " + ID;
                }
                toStringNeedsUpdate = false;
                this._TOSRTING = "";
                if (_propertiesCache != null)
                {
                    if (!String.IsNullOrEmpty(_propertiesCache.Name))
                        _TOSRTING += String.Format("{0} ", _propertiesCache.Name);
                    if (!String.IsNullOrEmpty(_propertiesCache.Description))
                        _TOSRTING += String.Format(" | {0} ", _propertiesCache.Description);
                }
                else
                {
                    needUpdate = true;
                    if (RegionHandle == 0) RegionHandle = Prim.RegionHandle;
                    // DLRConsole.WriteLine("Reselecting prim " + Prim);
                    Simulator sim = GetSimulator();
                    if (sim != null)
                        WorldObjects.EnsureSelected(Prim, sim);
                }
                ID = Prim.ID;
                Primitive.ConstructionData PrimData = Prim.PrimData;
                PrimType Type = Prim.Type;

                if (PrimData.PCode == PCode.Prim)
                {
                    try
                    {
                        _TOSRTING += "" + Type;
                    }
                    catch (Exception e)
                    {
                    }
                }
                else
                {
                    try
                    {
                        _TOSRTING += "" + PrimData.PCode;
                    }
                    catch (Exception e)
                    {
                    }
                }
                _TOSRTING += String.Format(" {0} ", ID);

                if (!String.IsNullOrEmpty(Prim.Text))
                    _TOSRTING += String.Format(" | {0} ", Prim.Text);
                _TOSRTING += "(localID " + Prim.LocalID + ")";
                uint ParentId = Prim.ParentID;
                if (ParentId != 0)
                {
                    _TOSRTING += "(parent ";

                    Primitive pp = null;
                    if (_Parent != null)
                    {
                        //if (_Parent.!!HasPrim)
                        pp = _Parent.Prim;
                    }
                    else
                    {
                        if (RegionHandle != 0)
                        {
                            Simulator simu = GetSimulator();
                            pp = WorldSystem.GetPrimitive(ParentId, simu);
                        }
                    }
                    if (pp != null)
                    {
                        _TOSRTING += WorldSystem.GetPrimTypeName(pp) + " " + pp.ID.ToString().Substring(0, 8);
                    }
                    else
                    {
                        _TOSRTING += ParentId;
                    }
                    _TOSRTING += ")";
                }
                if (Children.Count > 0)
                {
                    _TOSRTING += "(childs " + Children.Count + ")";
                }
                else
                {
                    _TOSRTING += "(ch0)";
                }

                const PrimFlags AllPrimFlags = (PrimFlags) 0xffffffff;
                const PrimFlags FlagsToPrintFalse =
                    PrimFlags.ObjectAnyOwner | PrimFlags.InventoryEmpty | PrimFlags.ObjectOwnerModify;
                const PrimFlags FlagsToPrintTrue = (PrimFlags) (AllPrimFlags - FlagsToPrintFalse);

                PrimFlags showTrue = (Prim.Flags & FlagsToPrintTrue);
                if (showTrue != PrimFlags.None) _TOSRTING += "(PrimFlagsTrue " + showTrue + ")";
                PrimFlags showFalse = ((~Prim.Flags) & FlagsToPrintFalse);
                if (showFalse != PrimFlags.None) _TOSRTING += "(PrimFlagsFalse " + showFalse + ")";
                if (PathFinding._Mesh != null)
                    _TOSRTING += " (size " + GetSizeDistance() + ") ";
                _TOSRTING += Affordances.SuperTypeString();
                if (Prim.Sound != UUID.Zero)
                    _TOSRTING += "(Audible)";
                if (IsTouchDefined)
                    _TOSRTING += "(IsTouchDefined)";
                if (IsSitDefined)
                    _TOSRTING += "(IsSitDefined)";
                string simverb = GetSimVerb();
                if (!String.IsNullOrEmpty(simverb))
                    _TOSRTING += string.Format("(SimVerb \"{0}\")", simverb);
                if (!IsPassable)
                    _TOSRTING += "(!IsPassable)";
                if (Prim.PrimData.ProfileHollow > 0f)
                    _TOSRTING += String.Format("(hollow {0:0.00})", Prim.PrimData.ProfileHollow);
                if (WasKilled) _TOSRTING += "(IsKilled)";
                _TOSRTING = _TOSRTING.Replace("  ", " ").Replace(") (", ")(");
            }
            string _TOSRTINGR = _TOSRTING;
            toStringNeedsUpdate = false;
            //_TOSRTING = null;
            return _TOSRTINGR;
        }

        public SimObjectAffordanceImpl Affordances { get; set; }

        public bool RequestedParent = false;

        public bool IsRegionAttached
        {
            get
            {
                if (WasKilled) return false;
                if (!HasPrim) return false;
                Primitive Prim = null;
                lock (HasPrimLock)
                {

                    if (!HasPrim) return false;
                    Prim = _Prim0;
                    if (_Prim0.RegionHandle == 0)
                    {
                        return false;
                    }
                }
                if (IsRoot)
                {
                    return Prim != null && Prim.Position != Vector3.Zero;
                }
                if (_Parent == null)
                {
                    if (Prim.ParentID == 0)
                    {
                        Parent = this;
                        return true;
                    }
                    Simulator simu = GetSimulator();
                    if (simu == null) return false;
                    EnsureParentRequested(simu);
                    Primitive pUse = WorldSystem.GetPrimitive(Prim.ParentID, simu);
                    if (pUse == null)
                    {
                        return false;
                    }
                    if (_Parent == null)
                    {
                        if (pUse.ID != UUID.Zero)
                            Parent = WorldSystem.GetSimObject(pUse);
                        return false;
                    }
                }
                lock (HasPrimLock)
                {
                    try
                    {
                        return _Parent == this || (_Parent != null && _Parent.IsRegionAttached);
                    }
                    catch (StackOverflowException)
                    {
                        return false;
                    }
                }
            }
        }

        public virtual Simulator GetSimulator()
        {
            if (RegionHandle == 0)
            {
                return null;
            }
            return WorldSystem.GetSimulator(RegionHandle);
            //return GetSimRegion().TheSimulator;
        }


        public virtual Vector3 GetSimScale()
        {
            return Prim.Scale; // the scale is all in the prim w/o parents? 
        }


        public virtual Quaternion SimRotation
        {
            get
            {

                Primitive thisPrim = this.Prim;
                if (thisPrim == null)
                {
                    Error("GetSimRotation Prim==null: " + this);
                    return Quaternion.Identity;
                }
                Quaternion transValue = thisPrim.Rotation;
                if (!IsRegionAttached)
                {
                    WorldSystem.ReSelectObject(thisPrim);
                    if (thisPrim.ParentID == 0)
                    {
                        return transValue;
                    }
                    //WorldSystem.RequestMissingObject(Prim.LocalID, WorldSystem.GetSimulator(RegionHandle));
                    //WorldSystem.client.Objects.RequestObject(WorldSystem.GetSimulator(RegionHandle), Prim.LocalID);
                }
                if (thisPrim.ParentID != 0)
                {
                    Primitive outerPrim = GetParentPrim(thisPrim, Debug);
                    if (outerPrim == null)
                    {
                        //TODO no biggy here ?
                        Error("GetSimRotation !IsRegionAttached: " + this);
                        return transValue;
                    }
                    transValue = outerPrim.Rotation*transValue;
                    thisPrim = outerPrim;
                    //  transValue.Normalize();
                }
                return transValue;
            }
        }

        public Vector3 LastKnownSimPos;

        public virtual bool TryGetGlobalPosition(out Vector3d pos)
        {
            return TryGetGlobalPosition(out pos, null);
        }

        public virtual void UpdatePosition(ulong handle, Vector3 pos)
        {
            RegionHandle = handle;
            SimPosition = pos;
        }

        public virtual bool TryGetGlobalPosition(out Vector3d pos, OutputDelegate Debug)
        {
            Vector3 local;
            if (TryGetSimPosition(out local, Debug))
            {
                if (RegionHandle != 0)
                {
                    pos = ToGlobal(RegionHandle, local);
                    return true;
                }
                pos = default(Vector3d);
                return false;
            }
            pos = default(Vector3d);
            return false;
        }

        public virtual bool TryGetSimPosition(out Vector3 pos)
        {
            return TryGetSimPosition(out pos, null);
        }

        public virtual bool TryGetSimPosition(out Vector3 pos, OutputDelegate Debug)
        {
            Primitive thisPrim = null;
            pos = this.LastKnownSimPos;
            lock (HasPrimLock)
            {
                thisPrim = this._Prim0;
            }
            {
                {
                    if (thisPrim == null)
                    {
                        return (default(Vector3) != pos);
                    }
                    if (thisPrim.ParentID == 0)
                    {
                        pos = thisPrim.Position;
                        return true;
                    }
                    if (RequestedParent && _Parent != null)
                    {
                        var _ParentPrim = _Parent.Prim;
                        if (_ParentPrim != null && _ParentPrim != thisPrim)
                        {
                            pos = LastKnownSimPos = GetPosAfterParent(_ParentPrim, thisPrim.Position);
                        }
                        return true;
                    }

                    Vector3 thisPos = thisPrim.Position;
                    while (thisPrim.ParentID != 0)
                    {
                        Primitive outerPrim = GetParentPrim(thisPrim, Debug);

                        if (outerPrim == null)
                        {
                            if (pos != default(Vector3)) return true;
                            if (Debug != null) Debug("Unknown parent");
                            return false;
                        }
                        if (outerPrim == thisPrim || outerPrim == this._Prim0 || RequestedParent)
                        {
                            return false;
                        }

                        if (outerPrim.Position == default(Vector3))
                        {
                            if (Debug != null) Debug("parent pos==Zero");
                            return false;
                        }
                        thisPos = GetPosAfterParent(outerPrim, thisPos);
                        thisPrim = outerPrim;
                    }
                    if (false && BadLocation(thisPos))
                    {
                        if (Debug != null) Debug("-------------------------" + this + " shouldnt be at " + thisPos);
                        //   WorldSystem.DeletePrim(thePrim);
                    }
                    LastKnownSimPos = thisPos;
                    return true;
                }
            }
        }

        public static Vector3 GetPosAfterParent(Primitive parentPrim, Vector3 thisPos)
        {
            thisPos = parentPrim.Position +
                      Vector3.Transform(thisPos, Matrix4.CreateFromQuaternion(parentPrim.Rotation));
            return thisPos;
        }

        public virtual Vector3 SimPosition
        {
            get
            {
                Vector3 pos;
                if (TryGetSimPosition(out pos, Debug))
                {
                    return pos;
                }
                lock (HasPrimLock)
                {
                    if (LastKnownSimPos != default(Vector3)) return LastKnownSimPos;
                    if (RequestedParent && _Parent != null)
                    {
                        var thisPrim = this._Prim0;
                        var _ParentPrim = _Parent.Prim;
                        if (_ParentPrim != null && _ParentPrim != thisPrim)
                        {
                            LastKnownSimPos = GetPosAfterParent(_ParentPrim, thisPrim.Position);
                        }
                        return LastKnownSimPos;
                    }
                }
                if (IsRegionAttached) throw Error("GetSimPosition !IsRegionAttached: " + this);
                return LastKnownSimPos;
            }
            set
            {
                if (false)
                {
                    Vector3 current = SimPosition;
                    if (current != value)
                    {
                        SetObjectPosition(value);
                    }
                }
                LastKnownSimPos = value;
            }
        }

        protected Primitive GetParentPrim(Primitive thisPrim, OutputDelegate Debug)
        {
            lock (HasPrimLock) return GetParentPrim0(thisPrim, Debug);
        }

        protected Primitive GetParentPrim0(Primitive thisPrim, OutputDelegate Debug)
        {
            if (RequestedParent && thisPrim == _Prim0 && _Parent != null)
            {
                return _Parent.Prim;
            }
            if (IsKilled) return null;
            if (thisPrim.ParentID == 0)
            {
                return WorldSystem.GetSimObject(thisPrim).Prim;
            }
            int requests = 10;
            Primitive outerPrim = null;
            while (outerPrim == null && requests-- > 0)
            {
                if (thisPrim == Prim && _Parent != null)
                {
                    return _Parent.Prim;
                }
                uint theLPrimParentID = thisPrim.ParentID;
                if (theLPrimParentID == 0 || requests-- < 1)
                {
                    if (!RequestedParent) if (Debug != null) Debug("Why are not we getting a parent prim?");
                    return null;
                }
                Simulator simu = GetSimulator();
                outerPrim = WorldSystem.GetPrimitive(theLPrimParentID, simu);
                if (outerPrim == null)
                {
                    if (IsKilled) return null;
                    if (!RequestedParent)
                    {
                        EnsureParentRequested(simu);
                        if (Debug != null)
                        {
                            Debug("Probing for parent");
                            Thread.Sleep(500);
                        }
                    }
                }
                requests--;
            }
            return outerPrim;
        }


        protected void EnsureParentRequested(Simulator simu)
        {
            if (!RequestedParent)
            {
                RequestedParent = true;
                if (_Parent != null) return;
                Primitive Prim = this.Prim;
                if (Prim == null)
                {
                    RequestedParent = false;
                    return;
                }
                uint theLPrimParentID = Prim.ParentID;
                if (theLPrimParentID == 0 || _Parent != null) return;
                Primitive outerPrim = WorldSystem.GetPrimitive(theLPrimParentID, simu);
                if (outerPrim != null && outerPrim.ID != UUID.Zero)
                {
                    Parent = WorldSystem.GetSimObject(outerPrim, simu);
                    return;
                }
                WorldObjects.EnsureRequested(theLPrimParentID, simu);
                WorldObjects.EnsureSelected(theLPrimParentID, simu);
                ParentGrabber.AddFirst(() => TaskGetParent(theLPrimParentID, simu));
            }
        }

        private void TaskGetParent(uint theLPrimParentID, Simulator simu)
        {
            if (IsKilled) return;
            if (theLPrimParentID == 0 || _Parent != null) return;

            Primitive outerPrim = WorldSystem.GetPrimitive(theLPrimParentID, simu);
            if (outerPrim != null && outerPrim.ID != UUID.Zero)
            {
                Parent = WorldSystem.GetSimObject(outerPrim);
            }
            else
            {
                if (ParentGrabber.NoQueue) return;
                ParentGrabber.Enqueue(() => TaskGetParent(theLPrimParentID, simu));
                // missing parent still?!
                IsDebugging = true;
            }
        }

        protected TaskQueueHandler TaskInvGrabber
        {
            get { return WorldObjects.TaskInvGrabber; }
        }

        protected TaskQueueHandler ParentGrabber
        {
            get { return WorldObjects.ParentGrabber; }
        }


        public bool BadLocation(Vector3 transValue)
        {
            if (transValue.Z < -2.0f) return true;
            if (transValue.X < 0.0f) return true;
            if (transValue.X > 256.0f) return true;
            if (transValue.Y < 0.0f) return true;
            if (transValue.Y > 256.0f) return true;
            return false;
        }

        private float cachedSize = 0f;

        /// <summary>
        ///  Gets the distance a ISimAvatar may be from ISimObject to use
        /// </summary>
        /// <returns>1-255</returns>
        public virtual float GetSizeDistance()
        {
            if (IsAvatar) return 2f;
            if (cachedSize > 0) return cachedSize;
            double size = Math.Sqrt(BottemArea())/2;

            //            if (IsPhantom) return size;

            double fx; // = thePrim.Scale.X;
            //if (fx > size) size = fx;
            double fy; // = thePrim.Scale.Y;
            //if (fy > size) size = fy;

            foreach (SimObject obj in Children)
            {
                Primitive cp = obj.Prim;
                if (cp == null) continue;
                fx = cp.Scale.X;
                if (fx > size) size = fx;
                fy = cp.Scale.Y;
                if (fy > size) size = fy;
                double childSize = obj.GetSizeDistance();
                if (childSize > size) size = childSize;
            }
            return cachedSize = (float) size;
        }

        public virtual List<SimObject> GetNearByObjects(double maxDistance, bool rootOnly)
        {
            if (!IsRegionAttached)
            {
                List<SimObject> objs = new List<SimObject>();
                var Parent = this.Parent;
                if (Parent != null && Parent != this)
                {
                    objs.Add(Parent);
                }
                return objs;
            }
            List<SimObject> objs2 = WorldSystem.GetNearByObjects(GlobalPosition, this, (float) maxDistance, rootOnly);
            SortByDistance(objs2);
            return objs2;
        }

        public virtual Vector3d GlobalPosition
        {
            get { return ToGlobal(RegionHandle, SimPosition); }
        }

        protected Vector3d ToGlobal(ulong regionHandle, Vector3 objectLoc)
        {
            if (regionHandle == 0) Error("regionHandle = NULL");
            uint regionX = 0, regionY = 0;
            Utils.LongToUInts(regionHandle, out regionX, out regionY);
            return new Vector3d(regionX + objectLoc.X, regionY + objectLoc.Y, objectLoc.Z);
        }

        //static ListAsSet<ISimObject> CopyObjects(List<ISimObject> objects)
        //{
        //    ListAsSet<ISimObject> KnowsAboutList = new ListAsSet<ISimObject>();
        //    lock (objects) foreach (ISimObject obj in objects)
        //        {
        //            KnowsAboutList.Add(obj);
        //        }
        //    return KnowsAboutList;
        //}


        public virtual bool Matches(string name)
        {
            EnsureProperties(default(TimeSpan));
            string toString1 = ToString();
            return SimTypeSystem.MatchString(toString1, name);
        }

        public bool Named(string name)
        {
            if (_propertiesCache != null)
            {
                String s = _propertiesCache.Name;
                return s == name;
            }
            return GetName() == name;
        }

        public bool NamedCI(string name)
        {
            if (_propertiesCache != null)
            {
                String s = _propertiesCache.Name;
                if (s != null) return s.ToLower() == name;
            }
            return GetName().ToLower() == name;
        }

        public bool MaxDist(float dist, SimPosition from)
        {
            return Distance(from) < dist;
        }

        public bool MinDist(float dist, SimPosition from)
        {
            return Distance(from) > dist;
        }

        public virtual bool HasFlag(object name)
        {
            if (name == null) return PrimFlags.None == Prim.Flags;
            if (name is PrimFlags) return (Prim.Flags & (PrimFlags) name) != 0;
            return (" " + Prim.Flags.ToString().ToLower() + " ").Contains(" " + name.ToString().ToLower() + " ");
        }

        public virtual void Debug(string p, params object[] args)
        {
            string str = DLRConsole.SafeFormat(p, args) + " -'" + GetName() + "'-";
            WorldSystem.WriteLine(str);
        }

        public Exception Error(string p, params object[] args)
        {
            string str = DLRConsole.SafeFormat(p, args);
            Debug(str);
            return new ArgumentException(str);
        }

        public void SortByDistance(List<SimObject> sortme)
        {
            lock (sortme) sortme.Sort(CompareDistance);
        }

        public int CompareDistance(SimObject p1, SimObject p2)
        {
            if (p1 == p2) return 0;
            return (int) (Distance(p1) - Distance(p2));
        }

        private static int CompareSize(SimObject p1, SimObject p2)
        {
            return (int) (p1.GetSizeDistance()*p1.GetCubicMeters() - p2.GetSizeDistance()*p2.GetCubicMeters());
        }

        public int CompareDistance(Vector3d v1, Vector3d v2)
        {
            Vector3d rp = GlobalPosition;
            return (int) (Vector3d.Distance(rp, v1) - Vector3d.Distance(rp, v2));
        }

        public string DistanceVectorString(SimPosition obj)
        {
            if (!obj.IsRegionAttached)
            {
                Vector3 loc;
                loc = obj.SimPosition;
                SimPathStore R = obj.PathStore;
                return String.Format("unknown relative {0}/{1:0.00}/{2:0.00}/{3:0.00}",
                                     R != null ? R.RegionName : "NULL", loc.X, loc.Y, loc.Z);
            }
            if (!IsRegionAttached) return obj.DistanceVectorString(this);
            return DistanceVectorString(obj.GlobalPosition);
        }

        public string DistanceVectorString(Vector3d loc3d)
        {
            Vector3 loc = SimPathStore.GlobalToLocal(loc3d);
            SimPathStore R = SimPathStore.GetPathStore(loc3d);
            return String.Format("{0:0.00}m ", Vector3d.Distance(GlobalPosition, loc3d))
                   + String.Format("{0}/{1:0.00}/{2:0.00}/{3:0.00}", R.RegionName, loc.X, loc.Y, loc.Z);
        }

        public string DistanceVectorString(Vector3 loc)
        {
            SimRegion R = GetSimRegion();
            return String.Format("{0:0.00}m ", Vector3.Distance(SimPosition, loc))
                   + String.Format("{0}/{1:0.00}/{2:0.00}/{3:0.00}", R.RegionName, loc.X, loc.Y, loc.Z);
        }

        public virtual string GetName()
        {
            EnsureProperties(default(TimeSpan));
            if (_propertiesCache != null)
            {
                String s = _propertiesCache.Name;
                //if (s.Length > 8) return s;
                s += " | " + _propertiesCache.Description;
                if (s.Length > 3) return s;
            }
            lock (HasPrimLock)
                if (!HasPrim) return ToString() + " " + RegionHandle;
            return ToString();
        }


        public static int CompareLowestZ(SimObject p1, SimObject p2)
        {
            Box3Fill b1 = p1.OuterBox;
            Box3Fill b2 = p2.OuterBox;
            if (b1 == b2) return 0;
            // One is fully above the other
            if (b1.MaxZ < b2.MinZ) return -1;
            if (b2.MaxZ < b1.MinZ) return 1;
            // One is partially above the other
            if (b1.MaxZ < b2.MaxZ) return -1;
            if (b2.MaxZ < b1.MaxZ) return 1;
            // they are the same hieght (basically) so compare bottems
            return (int) (b1.MinZ - b2.MinZ);
        }

        public float BottemArea()
        {
            if (PathFinding._Mesh != null)
            {
                if (OuterBox.MaxX != float.MinValue)
                {
                    float bottemX = OuterBox.MaxX - OuterBox.MinX;
                    float bottemY = OuterBox.MaxY - OuterBox.MinY;
                    return bottemX*bottemY;
                }

            }
            if (!HasPrim) return 1;
            return Prim.Scale.X*Prim.Scale.Y;
        }

        //public SimRegion _CurrentRegion;

        public virtual SimRegion GetSimRegion()
        {
            lock (HasPrimLock)
            {
                Primitive Prim = this.Prim;
                while (RegionHandle == 0 && !ReferenceEquals(Prim, null))
                {
                    RegionHandle = Prim.RegionHandle;
                }
                if (RegionHandle == 0)
                {
                    return null;
                }
                return WorldSystem.GetRegion(RegionHandle);
            }
        }

        public Vector3d GetGlobalLeftPos(int angle, double Dist)
        {
            return SimRegion.GetGlobalLeftPos(this, angle, Dist);
        }

        #region SimPosition Members

        //public SimWaypoint GetWaypoint()
        //{
        //    return GetSimRegion().CreateClosestRegionWaypoint(GetSimPosition(),2);
        //}

        #endregion

        //public string ToMeshString()
        //{
        //    if (_Mesh != null)
        //    {
        //       return _Mesh.ToString();
        //    }
        //    return ToString();
        //}


        public bool IsInside(Vector3 L)
        {
            if (!WorldPathSystem.MaintainMeshes) return false;
            return PathFinding.Mesh.IsInside(L.X, L.Y, L.Z);
        }


        public virtual void AddCanBeTargetOf(int ArgN, CogbotEvent evt)
        {
            if (ArgN == 1)
            {
                SimObjectType simTypeSystemCreateObjectUse = SimTypeSystem.CreateObjectType(evt.Verb);
                SimTypeUsage usage = simTypeSystemCreateObjectUse.CreateObjectUsage(evt.Verb);
                if (evt.IsEventType(SimEventType.SIT))
                {
                    usage.UseSit = true;
                }
                if (evt.IsEventType(SimEventType.TOUCH))
                {
                    usage.UseGrab = true;
                }
                if (evt.IsEventType(SimEventType.ANIM))
                {
                    usage.UseAnim = evt.Verb;
                }
                if (evt.IsEventType(SimEventType.EFFECT))
                {
                    //todo need to parse the EffectType
                    usage.UseGrab = true;
                    //   usage.UseAnim = evt.Verb;
                }
                Affordances.ObjectType.AddSuperType(simTypeSystemCreateObjectUse);
            }
        }

        public static int MaxEventSize = 10; // Keeps only last 9 events
        public Queue<CogbotEvent> ActionEventQueue { get; set; }
        public CogbotEvent lastEvent = null;

        public readonly Dictionary<string, CogbotEvent> LastEventByName = new Dictionary<string, CogbotEvent>();


        public bool ShouldEventSource
        {
            get { return WorldSystem.UseEventSource(this); }
        }

        /// <summary>
        /// Returns false if the event has gone unsent
        /// </summary>
        /// <param name="SE"></param>
        /// <returns></returns>
        public virtual bool LogEvent(CogbotEvent SE)
        {
            // string eventName = SE.Verb;
            object[] args1_N = SE.GetArgs();
            bool saveevent = true;
            object[] args0_N = PushFrontOfArray(ref args1_N, this);
            if (ActionEventQueue == null) ActionEventQueue = new Queue<CogbotEvent>(MaxEventSize);
            lock (ActionEventQueue)
            {
                int ActionEventQueueCount = ActionEventQueue.Count;
                if (ActionEventQueueCount > 0)
                {
                    if (lastEvent != null)
                    {
                        if (lastEvent.SameAs(SE))
                        {
                            saveevent = false;
                            return false;
                        }
                        //else if (false)
                        //{
                        //    SimObjectEvent newEvt = lastEvent.CombinesWith(SE);
                        //    if (newEvt != null)
                        //    {
                        //        lastEvent.EventStatus = newEvt.EventStatus;
                        //        lastEvent.Parameters = newEvt.Parameters;
                        //        saveevent = false;
                        //        SE = lastEvent;
                        //    }
                        //}
                    }
                    if (saveevent && ActionEventQueueCount >= MaxEventSize) ActionEventQueue.Dequeue();
                }
                lastEvent = SE;
                if (saveevent)
                {
                    ActionEventQueue.Enqueue(SE);
                }
                if (ShouldEventSource)
                {
                    WorldSystem.SendPipelineEvent(SE);
                    saveevent = true;
                }
                else
                {
                    saveevent = false;
                }
                LastEventByName[SE.EventName] = SE;
            }
            for (int argN = 1; argN < args0_N.Length; argN++)
            {
                object o = args0_N[argN];
                if (o is SimObject)
                {
                    SimObject newSit = (SimObject) o;
                    newSit.AddCanBeTargetOf(argN, SE);
                }
            }
            return saveevent;
        }

        public static object[] RestOfArray(object[] args, int p)
        {
            if (args == null) return null;
            int len = args.Length;
            Type t = args.GetType().GetElementType();
            int newLen = len - p;
            if (newLen <= 0)
            {
                return (object[]) Array.CreateInstance(t, 0);
            }
            object[] o = (object[]) Array.CreateInstance(t, newLen);
            Array.Copy(args, p, o, 0, newLen);
            return o;
        }

        public static object[] PushFrontOfArray(ref object[] args, object p)
        {
            if (args == null) return null;
            int len = args.Length;
            Type t = args.GetType().GetElementType();
            int newLen = len + 1;
            object[] o = (object[]) Array.CreateInstance(t, newLen);
            Array.Copy(args, 0, o, 1, len);
            o[0] = p;
            return o;
        }


        public string GetSimVerb()
        {
            Primitive Prim = this.Prim;
            string sn;
            OpenMetaverse.Primitive.ObjectProperties PrimProperties = _propertiesCache;
            if (PrimProperties != null)
            {
                sn = PrimProperties.TouchName;
                if (!String.IsNullOrEmpty(sn)) return sn;
                sn = PrimProperties.SitName;
                if (!String.IsNullOrEmpty(sn)) return sn;
            }
            sn = Affordances.ObjectType.GetTouchName();
            if (!String.IsNullOrEmpty(sn)) return sn;
            sn = Affordances.ObjectType.GetSitName();
            if (!String.IsNullOrEmpty(sn)) return sn;
            return null;
        }


        public string SitName
        {
            get
            {
                string sn = null;
                if (_propertiesCache != null)
                    sn = _propertiesCache.SitName;
                if (!String.IsNullOrEmpty(sn)) return sn;
                sn = Affordances.ObjectType.GetSitName();
                if (!String.IsNullOrEmpty(sn)) return sn;
                return "SitOnObject";
            }
        }

        public string TouchName
        {
            get
            {
                string sn = null;
                if (_propertiesCache != null)
                    sn = _propertiesCache.TouchName;
                if (!String.IsNullOrEmpty(sn)) return sn;
                sn = Affordances.ObjectType.GetTouchName();
                if (!String.IsNullOrEmpty(sn)) return sn;
                return "TouchTheObject";
            }
        }


        private bool _isChild;

        public bool IsAttachment
        {
            get { return IsAttachmentRoot || IsChildOfAttachment; }
        }
        public bool IsChildOfAttachment
        {
            get { return IsChild && Parent.IsAttachment; }
        }        
        // if Parent is an Avatar
        public bool IsAttachmentRoot
        {
            get { return _Parent is SimAvatar; }
        }

        public AttachmentPoint AttachPoint
        {
            get
            {
                Primitive Prim = this.Prim;
                if (Prim == null) return AttachmentPoint.Default;
                return Prim.PrimData.AttachmentPoint;
            }
        }

        public bool IsAttachable
        {
            get { return AttachPoint != AttachmentPoint.Default; }
        }

        public bool IsChild
        {
            get { return _isChild || _Parent is SimAvatar; }
            set { _isChild = value; }
        }

        private readonly ListAsSet<UUID> CurrentSounds = new ListAsSet<UUID>();
        public static float SoundGainThreshold = 0.1f;

        public void OnSound(UUID soundID, float gain)
        {
            if (soundID == UUID.Zero)
            {
                if (gain < SoundGainThreshold)
                {
                    CurrentSounds.Clear();
                    if (IsDebugging) Debug("Clearing all sounds");
                }
                else
                {
                    Debug("Gain change for unknown sound: " + gain);
                }
            }
            else
            {
                if (gain < SoundGainThreshold)
                {
                    CurrentSounds.Remove(soundID);
                }
                else
                {
                    CurrentSounds.AddTo(soundID);
                }
            }
        }

        public static readonly List<InventoryBase> EMPTY_TASK_INV = new List<InventoryBase>();
        public static readonly List<InventoryBase> ERROR_TASK_INV = new List<InventoryBase>();

        /// <summary>
        /// Retrieve a listing of the items contained in a task (Primitive)
        /// </summary>
        /// <param name="objectID">The tasks <seealso cref="UUID"/></param>
        /// <param name="objectLocalID">The tasks simulator local ID</param>
        /// <param name="timeoutMS">milliseconds to wait for reply from simulator</param>
        /// <returns>A list containing the inventory items inside the task or null
        /// if a timeout occurs</returns>
        /// <remarks>This request blocks until the response from the simulator arrives 
        /// or timeoutMS is exceeded</remarks>
        public void StartGetTaskInventory()
        {
            if (_StartedGetTaskInventory) return;
            _StartedGetTaskInventory = true;
            InventoryManager man = Client.Inventory;
            man.TaskInventoryReply += ti_callback;
            man.RequestTaskInventory(LocalID);
            //Client.Objects.RequestObjectMedia(ID, GetSimulator(), med_entry);
        }

        private void med_entry(bool success, string version, MediaEntry[] facemedia)
        {
            //prim is now updated   
        }

        private bool _StartedGetTaskInventory;
        private ulong _xferID;

        private void ti_callback(object sender, TaskInventoryReplyEventArgs e)
        {
            if (e.ItemID == ID)
            {
                InventoryManager man = Client.Inventory;
                String filename = e.AssetFilename;
                man.TaskInventoryReply -= ti_callback;

                if (!String.IsNullOrEmpty(filename))
                {
                    Client.Assets.XferReceived += xferCallback;

                    // Start the actual asset xfer
                    _xferID = Client.Assets.RequestAssetXfer(filename, true, false, UUID.Zero, AssetType.Unknown, true);
                }
                else
                {
                    Logger.DebugLog("Task is empty for " + ID, Client);
                    if (TaskInventoryLikely)
                    {
                        objectinventory = ERROR_TASK_INV;
                    }
                    else
                    {
                        objectinventory = EMPTY_TASK_INV;
                    }
                }
            }
        }

        private void xferCallback(object sender, XferReceivedEventArgs e)
        {
            if (e.Xfer.XferID == _xferID)
            {
                Client.Assets.XferReceived -= xferCallback;
                if (e.Xfer.Error != TransferError.None)
                {
                    objectinventory = ERROR_TASK_INV;
                    return;
                }
                String taskList = Utils.BytesToString(e.Xfer.AssetData);
                objectinventory = InventoryManager.ParseTaskInventory(taskList);
            }
        }

        public string MissingData
        {
            get
            {
                bool selectObject = false;
                bool requestObject = false;
                bool requestMedia = false;

                string missing = "";
                if (objectinventory == null)
                {
                    if (_StartedGetTaskInventory && !InventoryEmpty)
                    {
                        missing += " TaskInv";
                    }
                    else
                    {
                        StartGetTaskInventory();
                        missing += " WTaskInv";
                    }
                }
                if (_Prim0 == null)
                {
                    missing += " Prim";
                }
                else
                {
                    if (_Prim0.Textures == null)
                    {
                        missing += " PrimTextures";
                        requestMedia = true;
                    }
                    if (Object.Equals(_Prim0.ParticleSys, default(Primitive.ParticleSystem)))
                    {
                        missing += " PrimParticleSys";
                        requestMedia = true;
                    }
                    if (_Prim0.Type == PrimType.Sculpt && _Prim0.Sculpt == null)
                    {
                        missing += " PrimSculpt";
                        requestMedia = true;
                    }
                }
                if (_Parent == null)
                {
                    if (_Prim0 != null)
                    {
                        if (_Prim0.ParentID != 0)
                        {
                            Client.Objects.RequestObject(GetSimulator(), _Prim0.ParentID);
                            missing += " Parent";
                        }
                    }
                }
                if (Properties == null)
                {
                    missing += " Props";
                    selectObject = true;
                }
                else
                {
                    if (_Prim0 != null && _Prim0.Properties == null)
                    {
                        selectObject = true;
                        missing += " PrimProp";
                    }
                    if (Properties.TextureIDs == null)
                    {
                        selectObject = true;
                        missing += " PropData";
                    }
                }
                if (requestMedia)
                {
                    requestObject = true;
                }
                if (RegionHandle == 0)
                {
                    requestObject = true;
                    missing += " RHandle";
                }
                if (IsKilled)
                {
                    requestObject = true;
                    missing += " Killed";
                }
                if (selectObject) Client.Objects.SelectObject(GetSimulator(), LocalID, true);
                if (requestObject) Client.Objects.RequestObject(GetSimulator(), LocalID);
                // if (requestMedia) Client.Objects.RequestObjectMedia(ID, GetSimulator(), med_entry);
                return missing.TrimStart();
            }
        }

        public bool IsComplete
        {
            get { return string.IsNullOrEmpty(MissingData); }
        }

        private List<InventoryBase> objectinventory;
        // might take 10 secoinds the first time
        public virtual List<InventoryBase> TaskInventory
        {
            get
            {
                if (objectinventory == null)
                {
                    if (InventoryEmpty)
                    {
                        return EMPTY_TASK_INV;
                    }
                    List<InventoryBase> ibs = Client.Inventory.GetTaskInventory(ID, LocalID, 10000);
                    if (ibs != null) objectinventory = ibs;
                }
                return objectinventory;
            }
        }

        public virtual bool OnEffect(object evSender, string effectType, object t, object p, float duration, UUID id)
        {
            CogbotEvent newSimObjectEvent = ACogbotEvent.CreateEvent(evSender, SimEventType.Once, effectType,
                                                                     SimEventType.EFFECT | SimEventType.REGIONAL,
                                                                     WorldObjects.ToParameter("doneBy", this),
                                                                     WorldObjects.ToParameter("objectActedOn", t),
                                                                     WorldObjects.ToParameter("eventPartiallyOccursAt",
                                                                                              p),
                                                                     WorldObjects.ToParameter("simDuration", duration),
                                                                     WorldObjects.AsEffectID(id));
            bool noteable = LogEvent(newSimObjectEvent);
            if (!noteable)
            {
                if (WorldSystem.UseEventSource(t) || ShouldEventSource)
                {
                    WorldSystem.SendPipelineEvent(newSimObjectEvent);
                }
            }
            //todo
            // LogEvent will send noteables already if (noteable) WorldSystem.SendPipelineEvent(newSimObjectEvent);
            return noteable;
        }


        public class SimObjectAffordanceImpl : SimObjectAffordance
        {
            public SimObjectImpl thiz;
            // Afordance system
            public SimObjectType ObjectType { get; set; }
            private bool IsUseableCachedKnown, IsUseableCachedTrue;

            public void AddSuperTypes(IList<SimObjectType> listAsSet)
            {
                lock (thiz.toStringLock)
                {
                    thiz.toStringNeedsUpdate = true;
                }
                //SimObjectType _UNKNOWN = SimObjectType._UNKNOWN;
                foreach (SimObjectType type in listAsSet)
                {
                    ObjectType.AddSuperType(type);
                }
            }

            public bool IsUseable
            {
                get
                {
                    if (!IsUseableCachedKnown)
                    {
                        IsUseable = thiz.IsSitDefined || thiz.IsSitDefined || IsTypeOf(SimTypeSystem.USEABLE) != null;
                    }
                    return IsUseableCachedTrue;
                }

                set
                {
                    IsUseableCachedKnown = true;
                    IsUseableCachedTrue = value;
                }
            }


            public bool IsTyped
            {
                get
                {
                    if (thiz.WasKilled) return false;
                    return ObjectType.IsComplete;
                }
            }

            /// <summary>
            /// the bonus or handicap the object has compared to the defination 
            /// (more expensive chair might have more effect)
            /// </summary>
            public float scaleOnNeeds = 1.11F;

            [ConfigSetting(Description = "how long affordance system will wait for object properties")] public static
                TimeSpan AffordanceWaitProps = TimeSpan.FromSeconds(5);

            public SimObjectType IsTypeOf(SimObjectType superType)
            {
                return ObjectType.IsSubType(superType);
            }

            public double RateIt(BotNeeds needs)
            {
                return ObjectType.RateIt(needs, GetBestUse(needs))*scaleOnNeeds;
            }

            public IList<SimTypeUsage> GetTypeUsages()
            {
                return ObjectType.GetTypeUsages();
            }

            public List<SimObjectUsage> GetUsages()
            {
                List<SimObjectUsage> uses = new List<SimObjectUsage>();
                thiz.EnsureProperties(AffordanceWaitProps);
                foreach (SimTypeUsage typeUse in ObjectType.GetTypeUsages())
                {
                    uses.Add(new SimObjectUsage(typeUse, thiz));
                }
                return uses;
            }

            public string SuperTypeString()
            {
                String str = "[";
                lock (ObjectType.SuperType)
                    ObjectType.SuperType.ForEach(delegate(SimObjectType item) { str += item.GetTypeName() + " "; });
                return str.TrimEnd() + "]";
            }

            public void UpdateFromProperties(Primitive.ObjectProperties objectProperties)
            {
                if (!string.IsNullOrEmpty(objectProperties.SitName)) ObjectType.SitName = objectProperties.SitName;
                if (!string.IsNullOrEmpty(objectProperties.TouchName))
                    ObjectType.TouchName = objectProperties.TouchName;
                if (SimObjectImpl.AffordinancesGuessSimObjectTypes)
                    SimTypeSystem.GuessSimObjectTypes(objectProperties, thiz);
            }

            public BotNeeds GetActualUpdate(string pUse)
            {
                thiz.EnsureProperties(AffordanceWaitProps);
                return ObjectType.GetUsageActual(pUse).Magnify(scaleOnNeeds);
            }


            public SimTypeUsage GetBestUse(BotNeeds needs)
            {
                thiz.EnsureProperties(AffordanceWaitProps);

                IList<SimTypeUsage> all = ObjectType.GetTypeUsages();
                if (all.Count == 0) return null;
                SimTypeUsage typeUsage = all[0];
                double typeUsageRating = 0.0f;
                foreach (SimTypeUsage use in all)
                {
                    double f = ObjectType.RateIt(needs, use);
                    if (f > typeUsageRating)
                    {
                        typeUsageRating = f;
                        typeUsage = use;
                    }
                }
                return typeUsage;
            }

            //public Vector3 GetUsePosition()
            //{
            //    return GetSimRegion().GetUsePositionOf(GetSimPosition(),GetSizeDistance());
            //}

            public BotNeeds GetProposedUpdate(string pUse)
            {
                thiz.EnsureProperties(AffordanceWaitProps);
                return ObjectType.GetUsagePromise(pUse).Magnify(scaleOnNeeds);
            }

        }

        private bool IsSolidCachedKnown, IsSolidCachedTrue;

        public virtual bool IsSolid
        {
            get
            {
                if (PathFinding.MadePhantom) return true; // since we "changed" it
                if (!IsSolidCachedKnown)
                {
                    IsSolidCachedKnown = true;
                    if (IsPhantom) return false;
                    IsSolidCachedTrue = !(IsPhantom || Affordances.IsTypeOf(SimTypeSystem.PASSABLE) != null);
                }
                return IsSolidCachedTrue;
            }
            set
            {
                IsSolidCachedKnown = true;
                IsSolidCachedTrue = value;
            }
        }

        public class SimObjectPathFindingImpl : SimObjectPathFinding
        {
            public SimObjectImpl thiz;
            internal SimMesh _Mesh;
            private bool wasMeshUpdated;


            public bool SkipMesh
            {
                get
                {
                    if (thiz._Prim0 == null) return true;
                    return WorldPathSystem.SkipPassableMeshes && (thiz.IsPassable || thiz.IsPhantom);
                }
            }

            public SimMesh Mesh
            {
                get
                {
                    if (_Mesh == null)
                    {
                        if (SkipMesh)
                        {
                            return null;
                        }
                        _Mesh = new SimMesh(thiz, thiz.Prim, thiz.PathStore);
                    }
                    return _Mesh;
                }
                // set { _Mesh = value; }
            }

            public bool MadeNonPhysical = false;
            public bool MadeNonTemp = false;
            public bool MadePhantom = false;
            public bool IsMeshed { get; set; }

            public void RemoveCollisions()
            {
                IsMeshed = false;
                // say we are busy
                if (IsMeshing) return;
                IsMeshing = true;
                if (_Mesh != null)
                {
                    _Mesh.RemoveCollisions();
                    _Mesh = null;
                }
                foreach (SimObject o in thiz.Children)
                {
                    o.PathFinding.RemoveCollisions();
                }
                IsMeshing = false;
            }


            /// <summary>
            /// Update our collisions and all of childrens
            /// </summary>
            public void UpdateCollisions()
            {
                if (thiz.IsRegionAttached)
                {
                    if (IsWorthMeshing)
                    {
                        RemoveCollisions();
                        AddCollisions();
                    }
                    else
                    {
                        RemoveCollisions();
                    }
                }

            }

            private bool? requestedMeshed;

            public bool IsWorthMeshing
            {
                get
                {
                    if (SkipMesh) return false;
                    if (requestedMeshed.HasValue) return requestedMeshed.Value;
                    if (WorldPathSystem.IsWorthMeshing(thiz))
                    {
                        requestedMeshed = true;
                        return true;
                    }
                    return false;
                }
                set { requestedMeshed = value; }
            }


            private bool IsMeshing;

            public virtual bool AddCollisions()
            {
                try
                {
                    return QueueMeshing();
                }
                catch (Exception e)
                {
                    thiz.Debug("While updating " + e);
                    return false;
                }

            }

            private bool IsMeshingQueued = false;

            public virtual bool QueueMeshing()
            {
                if (!thiz.IsRegionAttached)
                {
                    return false;
                }
                if (!thiz.IsRoot)
                {
                    // dont mesh armor
                    if (thiz.Parent is SimAvatar)
                    {
                        return false;
                    }
                }
                try
                {
                    if (SkipMesh)
                    {
                        return false;
                    }

                    if (!IsMeshingQueued)
                    {
                        IsMeshingQueued = true;
                        Cogbot.WorldPathSystem.MeshingQueue.AddFirst("mesh " + ToString(), () => { AddCollisionsNow(); });
                    }
                    return wasMeshUpdated;

                }
                finally
                {
                    wasMeshUpdated = false;
                }
            }

            public bool AddCollisionsNow()
            {
                try
                {
                    return AddCollisionsNowNoCatch();
                }
                catch (Exception e)
                {
                    thiz.Debug("While updating " + e);
                    IsMeshed = false;
                    return false;
                }
                finally
                {
                    IsMeshingQueued = false;
                }
            }

            public bool AddCollisionsNowNoCatch()
            {
                if (!thiz.IsRegionAttached)
                {
                    return false;
                }
                if (!thiz.IsRoot)
                {
                    // dont mesh armor
                    if (thiz.Parent is SimAvatar)
                    {
                        return false;
                    }
                }

                if (SkipMesh) return false;

                if (!WorldPathSystem.MaintainMeshes) return false;

                // avoids loop (since our parent will call us again)
                if (IsMeshing || IsMeshed) return false;
                IsMeshing = true;

                bool updated = thiz.GetSimRegion().AddCollisions(Mesh);
                var _Parent = thiz._Parent;
                // update parent + siblings
                if (!thiz.IsRoot)
                {
                    if (_Parent != null)
                    {
                        if (_Parent.PathFinding.AddCollisionsNow()) updated = true;
                    }
                }
                // update children
                foreach (SimObject o in thiz.Children)
                {
                    if (o.PathFinding.AddCollisionsNow())
                    {
                        updated = true;
                    }
                }
                wasMeshUpdated = updated;
                IsMeshed = true;
                return wasMeshUpdated;
            }


            public virtual bool RestoreEnterable(SimMover actor)
            {
                bool changed = false;
                Primitive Prim = thiz.Prim;
                if (Prim == null) return false;
                PrimFlags tempFlags = Prim.Flags;
                if (MadePhantom)
                {
                    ((SimObjectPathMover) actor).Touch(thiz);
                    changed = true;
                    thiz.IsPhantom = false;
                }
                if (!thiz.IsRoot)
                {
                    SimObject P = thiz.Parent;
                    if (P.Prim != thiz.Prim)
                        return P.PathFinding.RestoreEnterable(actor);
                }
                return changed;
            }

            public virtual bool MakeEnterable(SimMover actor)
            {
                if (thiz.Affordances.IsTypeOf(SimTypeSystem.DOOR) != null)
                {
                    if (!thiz.IsPhantom)
                    {
                        ((SimObjectPathMover) actor).Touch(thiz);
                        return true;
                    }
                    return false;
                }

                if (!thiz.IsRoot)
                {
                    SimObject P = thiz.Parent;
                    if (P.Prim != thiz.Prim)
                        return P.PathFinding.MakeEnterable(actor);
                }

                bool changed = false;
                if (!thiz.IsPhantom)
                {
                    thiz.IsPhantom = true;
                    ((SimObjectPathMover) actor).Touch(thiz);
                    changed = true;
                    // reset automatically after 30 seconds
                    new Thread(new ThreadStart(delegate()
                                                   {
                                                       Thread.Sleep(30000);
                                                       thiz.IsPhantom = false;
                                                   })).Start();
                }
                return changed;
            }

            public bool CanShoot(SimPosition position)
            {
                if (!thiz.IsRegionAttached || !position.IsRegionAttached) return false;
                SimPathStore PS1 = thiz.PathStore;
                SimPathStore PS2 = position.PathStore;
                if (PS1 != PS2) return false;
                Vector3 end = position.SimPosition;
                Vector3 first = PS1.FirstOcclusion(thiz.SimPosition, end);
                return (first == end);
            }
        }

        public virtual bool IsLocal
        {
            get
            {
                return HasPrim;
            }
        }

        public virtual bool HasPrim
        {
            get { return !ReferenceEquals(_Prim0, null); }
        }

        public readonly object HasPrimLock = new object();

        internal Color DebugColor()
        {
            if (Affordances.IsUseable) return Color.Green;
            return Color.Empty;
        }

        #region SimObject Members

        private Dictionary<string, object> _infoMapDictBackup;
        private readonly object FILock = new object();
        protected bool toStringNeedsUpdate = true;
        public bool IsDebugging { get; set; }


        public object this[string s]
        {
            get
            {
                lock (FILock)
                {
                    if (_infoMap != null && _infoMap.ContainsKey(s))
                    {
                        var val = _infoMap[s].Value;
                        if (val is NullType) return null;
                        return val;
                    }
                    if (_infoMapDictBackup == null || !_infoMapDictBackup.ContainsKey(s))
                    {
                        return null;
                    }
                    return _infoMapDictBackup[s];
                }
            }
            set
            {
                lock (FILock)
                {
                    if (_infoMap == null) _infoMap = new Dictionary<object, NamedParam>();
                    else if (_infoMap.ContainsKey(s))
                    {
                        _infoMap[s].SetValue(value);
                    }
                    if (_infoMapDictBackup == null) _infoMapDictBackup = new Dictionary<string, object>();
                    _infoMapDictBackup[s] = value;
                }
            }
        }

        private Dictionary<Type, object> _AsObjectType;
        public static bool AffordinancesGuessSimObjectTypes = false;

        public T AsObject<T>()
        {
            lock (FILock)
            {
                if (_AsObjectType == null)
                {
                    return default(T);
                }
                Type tt = typeof (T);
                object obj;
                if (!_AsObjectType.TryGetValue(tt, out obj))
                {
                    obj = _AsObjectType[tt] = default(T);
                }
                return (T) obj;
            }
        }

        #endregion

        public Parcel GetParcel()
        {
            var R = GetSimRegion();
            if (R == null) return null;
            var pos = SimPosition;
            return R.GetParcel(pos.X, pos.Y);
        }

        public bool CanFly
        {
            get
            {
                var p = GetParcel();
                if (p != null)
                {
                    if ((p.Flags & ParcelFlags.AllowFly) != 0) return true;
                }
                var s = GetSimulator();
                if (s != null)
                {
                    RegionFlags rf = s.Flags;
                    if ((rf & RegionFlags.NoFly) != 0) return false;
                }
                return true;
            }
        }

        public bool CanPush
        {
            get
            {
                var p = GetParcel();
                if (p != null)
                {
                    if ((p.Flags & ParcelFlags.RestrictPushObject) != 0) return false;
                }
                var s = GetSimulator();
                if (s != null)
                {
                    RegionFlags rf = s.Flags;
                    if ((rf & RegionFlags.SkipPhysics) != 0) return false;
                    if ((rf & RegionFlags.RestrictPushObject) != 0) return false;
                }
                return true;
            }
        }

        public bool CanUseVehical
        {
            get
            {
                var p = GetParcel();
                if (p != null)
                {
                    ParcelFlags pf = p.Flags;
                    if ((pf & ParcelFlags.AllowAPrimitiveEntry) == 0) return false;
                    if ((pf & ParcelFlags.AllowOtherScripts) == 0) return false;
                }
                var s = GetSimulator();
                if (s != null)
                {
                    RegionFlags rf = s.Flags;
                    if ((rf & RegionFlags.SkipScripts) != 0) return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.
        ///                 </param>
        public bool Equals(SimObjectImpl other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.ID, ID);
        }

        /// <summary>
        /// Serves as a hash function for a particular type. 
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override int GetHashCode()
        {
            return (ID != null ? ID.GetHashCode() : 0);
        }

        public static bool operator ==(SimObjectImpl left, SimObjectImpl right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(SimObjectImpl left, SimObjectImpl right)
        {
            return !Equals(left, right);
        }

        #region Implementation of MixinSubObjects

        private static Type[] MixedTypes = new Type[]
                                               {
                                                   typeof(SimObjectImpl), 
                                                   typeof(Primitive),
                                                   typeof(Primitive.PhysicsProperties),
                                                   typeof(Primitive.ParticleSystem),
                                                   typeof(Primitive.ObjectProperties),
                                                   typeof(SimObjectAffordanceImpl),
                                                   typeof(SimObjectPathFindingImpl),
                                                   typeof(SimMesh)
                                               };
        public Type[] GetMixedTypes()
        {
            return MixedTypes;
        }
        public Func<object>[] GetInstances()
        {
            return new Func<object>[]
                       {
                           () => this,
                           () => Prim,
                           () => Prim.PhysicsProps,
                           () => Prim.ParticleSys,
                           () => Properties, 
                           () => Affordances, 
                           () => PathFinding, 
                           () => PathFinding.Mesh,
                       };
        }
        public object GetInstance(Type subtype)
        {
            for (int i = 0; i < MixedTypes.Length; i++)
            {
                var s = MixedTypes[i];
                if (subtype.IsAssignableFrom(s))
                {
                    var insts = GetInstances();
                    return insts[i]();
                }
            }
            return null;
        }

        public T GetInstance<T>()
        {
            return (T) GetInstance(typeof (T));
        }

        #endregion
    }

    public interface SimObjectAffordance : NotContextualSingleton
    {
        // Afordance system
        bool IsTyped { get; }
        SimObjectType IsTypeOf(SimObjectType superType);
        IList<SimTypeUsage> GetTypeUsages();
        List<SimObjectUsage> GetUsages();
        BotNeeds GetProposedUpdate(string pUse);
        void AddSuperTypes(IList<SimObjectType> listAsSet);
        BotNeeds GetActualUpdate(string pUse);
        SimTypeUsage GetBestUse(BotNeeds needs);
        bool IsUseable { get; }
        double RateIt(BotNeeds needs);
        SimObjectType ObjectType { get; }
        string SuperTypeString();

        void UpdateFromProperties(Primitive.ObjectProperties objectProperties);
    }
    public interface SimObjectPathMover
    {
        // for pathfinbder mover
        void Touch(SimObject simObjectImpl);
        bool SalientGoto(SimPosition pos);
        bool FollowPathTo(SimPosition globalEnd, double distance);
        void SendUpdate(int ms);
        void SetMoveTarget(SimPosition target, double maxDist);
        bool WaitUntilPosSimple(Vector3d finalTarget, double maxDistance, float maxSeconds, bool adjustCourse);
        bool TeleportTo(SimRegion R, Vector3 local);
        bool TurnToward(SimPosition targetPosition);
        bool TurnToward(Vector3 target);        
    }


    public interface SimObjectPathFinding
    {
        bool IsMeshed { get; set; }
        bool IsWorthMeshing { get; set; }
        // for pathfinder
        bool MakeEnterable(SimMover actor);
        bool RestoreEnterable(SimMover actor);
        //inherited from Object string ToString();
        [ConvertTo]
        SimMesh Mesh { get; }
        bool AddCollisions();
        void UpdateCollisions();
        void RemoveCollisions();
        bool AddCollisionsNow();
        bool CanShoot(SimPosition position);
    }


    public interface SimObject : SimPosition, BotMentalAspect, SimDistCalc, MixinSubObjects
    {
        [ConvertTo]
        SimObjectImpl.SimObjectAffordanceImpl Affordances { get; }
        List<string> GetMenu(SimAvatar avatar);

        [ConvertTo]
        SimObjectImpl.SimObjectPathFindingImpl PathFinding { get; }

        object this[String s] { get; set; }
        bool AddChild(SimObject simObject);
        [FilterSpec]
        SimObject Parent { get; set; }
        bool BadLocation(Vector3 transValue);
        [FilterSpec]
        ListAsSet<SimObject> Children { get; }
        int CompareDistance(SimObject p1, SimObject p2);
        int CompareDistance(Vector3d v1, Vector3d v2);
        string DebugInfo();
        //double Distance(SimPosition prim);
        string DistanceVectorString(Vector3 loc);
        string DistanceVectorString(Vector3d loc3d);
        //inherited from SimPosition: string DistanceVectorString(SimPosition obj);
        Exception Error(string p, params object[] args);
        string GetName();
        Vector3 GetSimScale();
        Simulator GetSimulator();
        bool Flying { get; set; }
        bool InventoryEmpty { get; }
        bool IsInside(Vector3 L);
        bool IsKilled { set; get;  }
        bool IsControllable { get; }
        //inherited from SimPosition: bool IsPassable { get; set; }
        bool IsPhantom { get; set; }
        bool IsPhysical { get; set; }
        bool IsAttachment { get; }
        bool IsAttachable { get; }
        [FilterSpec]
        AttachmentPoint AttachPoint { get; }
        bool IsChild { get; set; }
        bool IsRoot { get; }
        bool IsSculpted { get; }
        bool IsSitDefined { get; }
        bool IsTouchDefined { get; }
        Primitive Prim { get; }
        [FilterSpec]
        bool Matches(string name);
        [FilterSpec]
        bool Named(string name);
        [FilterSpec(LastArgIsSelf = true)]
        bool MaxDist(float dist, SimPosition self);
        [FilterSpec(LastArgIsSelf = true)]
        bool MinDist(float dist, SimPosition self);
        [FilterSpec(LastArgIsSelf = true)]
        double ZDist(SimPosition self);

        [FilterSpec(LastArgIsSelf = true)]
        double Distance(SimPosition other);

        Vector3d GetGlobalLeftPos(int angle, double Dist);        
        Box3Fill OuterBox { get; }
        float BottemArea();


       
        void ResetPrim(Primitive prim, BotClient bc, Simulator sim);
        void ResetRegion(ulong regionHandle);
        bool SetObjectPosition(Vector3 localPos);
        bool SetObjectPosition(Vector3d globalPos);
        bool SetObjectRotation(Quaternion localPos);

        [FilterSpec]
        string SitName { get; }
        [FilterSpec]
        ulong RegionHandle { get; }
        void SortByDistance(List<SimObject> sortme);

        void UpdateObject(ObjectMovementUpdate objectUpdate, ObjectMovementUpdate objectUpdateDiff);

        //void UpdateProperties(Primitive.ObjectProperties props);

        // void AddPossibleAction(string textualActionName, params object[] args);

        void AddCanBeTargetOf(int argN, CogbotEvent evt);

        bool LogEvent(CogbotEvent evt);

        void OnSound(UUID soundID, float gain);

        bool OnEffect(object evSender, string effectType, object t, object p, float duration, UUID id);

        SimObject GetGroupLeader();

        float GetCubicMeters();

        List<SimObject> GetNearByObjects(double maxDistance, bool rootOnly);


        void SetFirstPrim(Primitive primitive);
        [ConvertTo]
        Primitive.ObjectProperties Properties { get; set; }
        bool IsLocal { get; }
        bool HasPrim { get; }
        [FilterSpec]
        uint LocalID { get; }
        [FilterSpec]
        uint ParentID { get; }
        bool IsDebugging { get; set; }
        bool ConfirmedObject { get; set; }
        bool ShouldEventSource { get; }
        bool KilledPrim(Primitive primitive, Simulator simulator);

        //ICollection<NamedParam> GetInfoMap();

        //void SetInfoMap(string key,Type type, Object value);
        SimHeading GetHeading();
        bool TryGetGlobalPosition(out Vector3d pos);
        void UpdatePosition(ulong handle, Vector3 pos);
        Queue<CogbotEvent> ActionEventQueue { get; set; }
        List<InventoryBase> TaskInventory { get;  }
        string MissingData { get; }
        bool IsComplete { get; }
        bool TaskInventoryLikely { get; }
        Primitive Prim0 { get; }
        bool IsTemporary { get; set; }
        bool IsAvatar { get; }

        void StartGetTaskInventory();

        bool EnsureProperties(TimeSpan block);
    }
}