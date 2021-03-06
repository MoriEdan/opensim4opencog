﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cogbot.World;
using MushDLR223.Utilities;
using OpenMetaverse;
using OpenMetaverse.Packets;
using System.Drawing;
using System.Collections;
using MushDLR223.ScriptEngines;

namespace Cogbot
{
    partial class WorldObjects
    {

        [ConfigSetting(Description = "if true, zero out the info for restoring an object back to inventory. this info is often useless in world")]
        public static bool ZeroOutUselessUUIDs = false;

        private static readonly Dictionary<SimObject, ObjectMovementUpdate> LastObjectUpdate = new Dictionary<SimObject, ObjectMovementUpdate>();
        private static readonly Dictionary<UUID, ObjectMovementUpdate> LastObjectUpdateDiff = new Dictionary<UUID, ObjectMovementUpdate>();
        private static readonly Dictionary<SimObject, Vector3> primVect = new Dictionary<SimObject, Vector3>();        


        public void Objects_OnPrimitiveProperties(object sender, ObjectPropertiesUpdatedEventArgs e)
        {
            var prim = e.Prim;
            var props = e.Properties;
            var simulator = e.Simulator;
            if (ScriptHolder == null && prim.ParentID != 0 && prim.ParentID == client.Self.LocalID)
            {
                if ("ScriptHolder" == props.Name)
                {
                    Debug("!!!!!XOXOX!!! Found our ScriptHolder " + prim);
                    ScriptHolder = prim;
                    ScriptHolderAttached = true;
                    ScriptHolderAttachWaiting.Set();
                }
            }
            CheckConnected(simulator);
            EnsureSimulator(simulator);
            NeverSelect(prim.LocalID, simulator);

            if (!MaintainPropertiesFromQueue)
                Objects_OnObjectProperties11(simulator, prim, props);
            else                
                PropertyQueue.Enqueue(() => Objects_OnObjectProperties11(simulator, prim, props));
        }

        public override void Objects_OnObjectProperties(object sender, ObjectPropertiesEventArgs e)
        {
            var simulator = e.Simulator;
            var props = e.Properties;          
            //throw new InvalidOperationException("Objects_OnObjectProperties");
            CheckConnected(simulator);
            //NeverSelect(props.LocalID, simulator);                
            PropertyQueue.Enqueue(delegate() { Objects_OnObjectProperties11(simulator, null, props); });
        }

        public void Objects_OnObjectProperties11(Simulator simulator, Primitive prim, Primitive.ObjectProperties props)
        {
            //Primitive prim = GetPrimitive(props.ObjectID, simulator);
            if (prim == null)
            {
                prim = GetPrimitive(props.ObjectID, simulator);
            }
            DeclareProperties(prim, props, simulator);
            if (prim != null)
            {
                prim.RegionHandle = simulator.Handle;
                SimObject updateMe = GetSimObject(prim, simulator);
                if (updateMe == null)
                {
                    return;
                }
                if (prim.ParentID == 0 && !SimRegion.OutOfRegion(prim.Position))
                {
                    updateMe.ResetPrim(prim, client, simulator);
                }
                if (MaintainObjectProperties)
                {
                    //updateMe.Properties = null;
                    updateMe.Properties = (props);
                }
                //Debug("UpdateProperties: {0}", updateMe.DebugInfo());
                describePrimToAI(prim, simulator);
            }
        }

        #region Nested type: DoWhat

        private delegate object DoWhat(SimObject objectUpdated, string p, Object before, Object after, Object diff);

        #endregion

        private void PrimtiveBlockUpdate(Simulator simulator, ObjectMovementUpdate objectupdate0, 
            Primitive prim,ObjectUpdatePacket.ObjectDataBlock block, Primitive.ConstructionData data)
        {
            bool isNewPrim = prim.Scale == Vector3.Zero && prim.Position == Vector3.Zero && prim.NameValues == null;
            if (prim.ID == UUID.Zero)
            {
                if (!simulator.Client.Settings.OBJECT_TRACKING)
                {
                    throw new ArgumentException("Need to enable object tracking!!");
                }
                SimObject O = GetSimObjectFromUUID(block.FullID);
                if (O == null)
                {
                    Debug("PrimData ZERO for " + prim);
                    if (block.ID > 0)
                    {
                        uint localID = block.ID;
                        prim.LocalID = localID;
                        prim.ID = block.FullID;
                       // var p = simulator.Client.Objects.GetPrimitive(simulator, block.ID, block.FullID, false);
                       // simulator.GetPrimitive(
                    }
                }
                else
                {
                    Debug("SimData ZERO for " + prim);
                    uint localID = block.ID;
                    prim.LocalID = localID;
                    prim.ID = block.FullID;
                    O.IsDebugging = true;
                }
            }
           // else
            {
                if (prim.RegionHandle == simulator.Handle && prim.ID != UUID.Zero)
                {
                    if (!prim.PrimData.Equals(data)
                        /* || prim.Scale != block.Scale
                            || prim.Position != objectupdate.Position
                            || prim.Rotation != objectupdate.Rotation
                            || prim.ParentID != block.ParentID*/
                        )
                    {
                        SimObject O = GetSimObjectFromUUID(prim.ID);
                        if (O != null)
                        {
                            if (!isNewPrim)
                            {
                                Debug("PrimData changed for " + prim);
                                O.PathFinding.RemoveCollisions();
                            }
                            // the old OnNewPrim code will force the reindexing
                        }
                        else
                        {
                            //Debug("PrimData new for " + prim);
                            O = GetSimObject(prim, simulator);
                            return;
                        }

                    }
                    if (!isNewPrim)
                    {
                        DeclareProperties(prim, prim.Properties, simulator);
                        SendNewRegionEvent(SimEventType.DATA_UPDATE, "on-data-updated", prim);
                    }
                    //Objects_OnPrimitiveUpdate(simulator, prim, objectupdate0, simulator.Handle, 0);
                }
                else
                {
                    //if (prim.RegionHandle == 0)
                    //    prim.RegionHandle = simulator.Handle;
                    if (prim.ID != UUID.Zero)
                    {
                        SimObject O = GetSimObjectFromUUID(prim.ID);
                        if (O != null && prim.Properties != null && prim.RegionHandle == simulator.Handle)
                        {
                            Objects_OnPrimitiveUpdateReal(simulator, prim, objectupdate0, simulator.Handle, 0);
                            //O = GetSimObject(prim, simulator);
                        }
                    }
                }
            }
        }

        private void Objects_OnObjectDataBlockUpdate(object sender, ObjectDataBlockUpdateEventArgs e)
        {
            Simulator simulator = e.Simulator;
            Primitive prim = e.Prim;
            var data = e.ConstructionData;
            ObjectUpdatePacket.ObjectDataBlock block = e.Block;
            var objectupdate0 = e.Update;
            if (!IsMaster(simulator)) return;
            // return;            
            if (!objectupdate0.Avatar)
            {
                PrimtiveBlockUpdate(simulator, objectupdate0, prim, block, data);
            }
            else // this code only is usefull for avatars
            {
                prim.Flags = (PrimFlags)block.UpdateFlags;

                if ((prim.Flags & PrimFlags.ZlibCompressed) != 0)
                {
                    Logger.Log("Got a ZlibCompressed ObjectMovementUpdate, implement me!",
                        Helpers.LogLevel.Warning, client);
                }

                prim.NameValues = e.NameValues;
                prim.LocalID = block.ID;
                prim.ID = block.FullID;
               //NOTE this broke onSitChanged! prim.ParentID = block.ParentID;
                prim.RegionHandle = simulator.Handle;
                prim.Scale = block.Scale;
                prim.ClickAction = (ClickAction)block.ClickAction;
                prim.OwnerID = block.OwnerID;
                prim.MediaURL = Utils.BytesToString(block.MediaURL);
                prim.Text = Utils.BytesToString(block.Text);
                prim.TextColor = new Color4(block.TextColor, 0, false, true);

                // Sound information
                prim.Sound = block.Sound;
                prim.SoundFlags = (SoundFlags)block.Flags;
                prim.SoundGain = block.Gain;
                prim.SoundRadius = block.Radius;

                // Joint information
                prim.Joint = (JointType)block.JointType;
                prim.JointPivot = block.JointPivot;
                prim.JointAxisOrAnchor = block.JointAxisOrAnchor;

                // Object parameters
                prim.PrimData = data;

                // Textures, texture animations, particle system, and extra params
                //prim.Textures = objectupdate.Textures;

                prim.TextureAnim = new Primitive.TextureAnimation(block.TextureAnim, 0);
                prim.ParticleSys = new Primitive.ParticleSystem(block.PSBlock, 0);
                prim.SetExtraParamsFromBytes(block.ExtraParams, 0);

                // PCode-specific data
                switch (data.PCode)
                {
                    case PCode.Grass:
                    case PCode.Tree:
                    case PCode.NewTree:
                        if (block.Data.Length == 1)
                            prim.TreeSpecies = (Tree)block.Data[0];
                        else
                            Logger.Log("Got a foliage update with an invalid TreeSpecies field", Helpers.LogLevel.Warning);
                        prim.ScratchPad = Utils.EmptyBytes;
                        break;
                    default:
                        prim.ScratchPad = new byte[block.Data.Length];
                        if (block.Data.Length > 0)
                            Buffer.BlockCopy(block.Data, 0, prim.ScratchPad, 0, prim.ScratchPad.Length);
                        break;
                }

                // Packed parameters
                //prim.CollisionPlane = objectupdate.CollisionPlane;
                //prim.Position = objectupdate.Position;
                //prim.Velocity = objectupdate.Velocity;
                //prim.Acceleration = objectupdate.Acceleration;
                //prim.Rotation = objectupdate.Rotation;
                //prim.AngularVelocity = objectupdate.AngularVelocity;
                EnsureSelected(prim, simulator);
                Objects_OnPrimitiveUpdateReal(simulator, prim, objectupdate0, simulator.Handle, 0);
            }
        }

        private void Objects_OnPrimitiveUpdateReal(Simulator simulator, Primitive av, ObjectMovementUpdate update, ulong RegionHandle, ushort TimeDilation)
        {
            if (!IsMaster(simulator)) return;
            if (av == null)
            {
                return;
            }
            if (av.ID == UUID.Zero)
            {
                return; // too early
            }
            SimObject AV = null;
            Object Obj;
            //lock (uuidTypeObject)
            if (UUIDTypeObjectTryGetValue(av.ID, out Obj))
                {
                    AV = Obj as SimObject;
                }
                else
                {
                    if (av.ID==client.Self.AgentID)                    
                        AV = GetSimObject(av, simulator);
                   
                }            
            if (AV != null)
            {
                Primitive prev = AV.Prim;
                if (prev == null)
                {
                    AV.SetFirstPrim(av);
                }
                if (av.ParentID == 0 && !SimRegion.OutOfRegion(av.Position))
                {
                    AV.ResetPrim(av, client, simulator);
                }
                if (prev!=null)
                {
                    // parent changed?
                    if (prev.ParentID != av.ParentID)
                    {
                        AV.Parent = null;
                    }
                }

                if (av.ParentID == 0 && !SimRegion.OutOfRegion(update.Position))
                {
                    if (update.Avatar)
                    {
                        SimRegion.GetRegion(simulator).UpdateTraveled(av.ID, av.Position, av.Rotation);
                        //return;
                    }
                }
            if (!MaintainObjectUpdates) return;
                EventQueue.Enqueue(() => Objects_OnObjectUpdated1(simulator, av, updatFromSimObject(AV), RegionHandle, TimeDilation));
            }
        }

          public override void Objects_OnObjectUpdated(object sender, TerseObjectUpdateEventArgs e1)
          {
              Objects_OnPrimitiveUpdateReal(e1.Simulator, e1.Prim, e1.Update, e1.Simulator.Handle, e1.TimeDilation);
              //base.Objects_OnObjectUpdated(sender,e1);
          }

        public void Objects_OnPrimitiveUpdateRealDead(Simulator simulator, ObjectMovementUpdate update, ulong regionHandle,
                                                     ushort timeDilation)
        {
            throw new InvalidOperationException("Objects_OnObjectProperties");
            /// return;
            if (simulator.Handle != regionHandle)
            {
                Debug("Strange update" + simulator);
                //base.Objects_OnObjectUpdated(simulator, update, regionHandle, timeDilation);
            }
            // return;
            CheckConnected(simulator);
            //all things if (update.Avatar)

            Primitive av = GetPrimitive(update.LocalID, simulator);
            Objects_OnObjectUpdated1(simulator, av, update, regionHandle, timeDilation);
        }

        public void Objects_OnObjectUpdated1(Simulator simulator, Primitive objectUpdated, ObjectMovementUpdate update, ulong regionHandle,
                                             ushort timeDilation)
        {
            if (!IsMaster(simulator)) return;
            if (objectUpdated != null)
            {
                //lock (objectUpdated)
                {
                    // other OMV code already updated the Primitive
                    // updateToPrim(prim, update);

                    if (objectUpdated.Properties != null)
                    {
                        //CalcStats(objectUpdated); 
                        DeclareProperties(objectUpdated, objectUpdated.Properties, simulator);
                        describePrimToAI(objectUpdated, simulator);
                    }
                    else
                    {
                        EnsureSelected(objectUpdated, simulator);
                    }

                    // Make a Last Object Update from the Primitive if we knew nothing about it
                    if (MaintainObjectUpdates)
                    {

                        SimObject simObject = GetSimObject(objectUpdated, simulator);
                        if (simObject != null)
                        {
                            lock (LastObjectUpdate)
                                if (!LastObjectUpdate.ContainsKey(simObject))
                                {
                                    LastObjectUpdate[simObject] = updatFromSimObject(simObject);
                                }
                            if (m_TheSimAvatar != null)
                            {
                                //double dist = simObject.Distance(TheSimAvatar);
                                //if (dist > 30 && !update.Avatar) return;
                            }                            
                            // Make a "diff" from previous
                            ObjectMovementUpdate up;
                            lock (LastObjectUpdate) up = LastObjectUpdate[simObject];
                            Object diffO = notifyUpdate(simObject, up, update,
                                                        InformUpdate);
                            if (diffO != null)
                            {
                                ObjectMovementUpdate diff = (ObjectMovementUpdate) diffO;
                                //if (lastObjectUpdateDiff.ContainsKey(objectUpdated.ID))
                                //{
                                //    notifyUpdate(objectUpdated, lastObjectUpdateDiff[objectUpdated.ID], diff, InformUpdateDiff);
                                //}
                                lock (LastObjectUpdateDiff) LastObjectUpdateDiff[objectUpdated.ID] = diff;
                            }

                            else
                            {
                                // someThingElseNeedsUpdate(objectUpdated);
                                //  needsOsdDiff = true;
                            }
                            ObjectMovementUpdate TheDiff = default(ObjectMovementUpdate);
                            lock (LastObjectUpdateDiff)
                            {
                                if (LastObjectUpdateDiff.ContainsKey(objectUpdated.ID))
                                {
                                    TheDiff = LastObjectUpdateDiff[objectUpdated.ID];
                                }
                                else
                                {
                                    LastObjectUpdateDiff[objectUpdated.ID] = TheDiff;
                                }
                            }
                            simObject.UpdateObject(update, TheDiff);
                            lock (LastObjectUpdate) LastObjectUpdate[simObject] = update;
                        }
                    }
                }
            }
            else
            {
                //WriteLine("missing Objects_OnObjectUpdated");
            }
        }

        public object InformUpdate(SimObject objectUpdated, string p, Object before, Object after, Object diff)
        {
            //Debug("{0} {1} DIFF {2} BEFORE {3} AFTER {4}", p, objectUpdated, diff, before, after);
            if (diff is Vector3)
            {
                // if the change is too small skip the event
                if ((1 > ((Vector3)diff).Length()))
                {                    
                   // return after;
                }
            }
            //  String lispName = "on-" + ((objectUpdated is Avatar) ? "avatar-" : "prim-") + p.ToLower() + "-updated";
            String lispName = "on-object-" + p.ToLower();
            SendNewUpdateEvent(lispName, objectUpdated, after);
            return after;
        }

        private Object notifyUpdate(SimObject objectUpdated, ObjectMovementUpdate before, ObjectMovementUpdate after, DoWhat didUpdate)
        {
            ObjectMovementUpdate diff = updateDiff(before, after);
            bool wasChanged = false;
            bool wasPositionUpdateSent = false;
            if (before.Acceleration != after.Acceleration)
            {
                after.Acceleration =
                    (Vector3)
                    didUpdate(objectUpdated, "Acceleration", before.Acceleration, after.Acceleration, diff.Acceleration);
                wasChanged = true;
            }
            if (before.AngularVelocity != after.AngularVelocity)
            {
                after.AngularVelocity =
                    (Vector3)
                    didUpdate(objectUpdated, "AngularVelocity", before.AngularVelocity, after.AngularVelocity,
                              diff.AngularVelocity);
                wasChanged = true;
            }
            if (false) if (before.CollisionPlane != after.CollisionPlane)
            {
                after.CollisionPlane =
                    (Vector4)
                    didUpdate(objectUpdated, "CollisionPlane", before.CollisionPlane, after.CollisionPlane,
                              diff.CollisionPlane);
                wasChanged = true;
            }
            if (before.Position != after.Position)
            {
                after.Position =
                    (Vector3)didUpdate(objectUpdated, "Position", before.Position, after.Position, diff.Position);
                wasChanged = true;
                wasPositionUpdateSent = true;
            }
            Quaternion diff1 = before.Rotation - after.Rotation;
            if (diff1.Length() > 0.1f)
            {
                after.Rotation =
                    (Quaternion)didUpdate(objectUpdated, "Rotation", before.Rotation, after.Rotation, diff.Rotation);
                wasChanged = true;
            }
            if (false) if (before.State != after.State)
            {
                didUpdate(objectUpdated, "State", before.State, after.State, diff.State);
                wasChanged = true;
            }
            if (before.Textures != after.Textures)
            {
                //didUpdate(objectUpdated, "Textures", before.Textures, after.Textures, diff.Textures);
                //wasChanged = true;
            }
            if (before.Velocity != after.Velocity)
            {
                // didUpdate(objectUpdated, "Velocity", before.Velocity, after.Velocity, diff.Velocity);
                if (before.Velocity == Vector3.Zero)
                {
                    SendNewUpdateEvent("on-object-start-velocity", objectUpdated, after.Velocity);
                    if (!wasPositionUpdateSent) SendNewUpdateEvent("on-object-position", objectUpdated, after.Position);
                }
                else if (after.Velocity == Vector3.Zero)
                {
                    if (!wasPositionUpdateSent) SendNewUpdateEvent("on-object-position", objectUpdated, after.Position);
                    SendNewUpdateEvent("on-object-stop-velocity", objectUpdated, -before.Velocity);
                }
                else
                {
                    //SendNewUpdateEvent("on-object-change-velosity", objectUpdated, after.Velocity);
                    if (!wasPositionUpdateSent) SendNewUpdateEvent("on-object-position", objectUpdated, after.Position);
                }
                wasChanged = true;
            }
            if (!wasChanged) return null;
            return diff;
        }

        private void SendNewUpdateEvent(string eventName, SimObject obj, object value)
        {
            return;
            //if (primitive is Avatar)                 
            //client.
            //	Debug(eventName + " " + client.argsListString(args));
            String evtStr = eventName.ToString();
            if (evtStr == "on-object-rotation") return;

            if (evtStr == "on-object-position")
            {
                SimObject prim = obj;
                Vector3 vect = (Vector3)value;

                if (!primVect.ContainsKey(prim))
                {
                    primVect[prim] = vect;
                    SendNewRegionEvent(SimEventType.MOVEMENT, eventName, obj, value);
                }
                else
                {
                    Vector3 v3 = primVect[prim] - vect;
                    if (v3.Length() > 0.5)
                    {
                        SendNewRegionEvent(SimEventType.MOVEMENT, eventName, obj, value);
                        primVect[prim] = vect;
                    }
                }
                return;
            }
            SendNewRegionEvent(SimEventType.MOVEMENT, eventName, obj, value);
        }

        public static ObjectMovementUpdate updatFromSimObject(SimObject from)
        {
            ObjectMovementUpdate update = new ObjectMovementUpdate();
            if (from.Prim != null)
            {
                update.Acceleration = from.Prim.Acceleration;
                update.AngularVelocity = from.Prim.AngularVelocity;
                update.CollisionPlane = from.Prim.CollisionPlane;
                update.Position = from.Prim.Position;
                update.Rotation = from.Prim.Rotation;
                update.State = from.Prim.PrimData.State;
                if (from.Prim.Textures != null) update.Textures = from.Prim.Textures;
                update.Velocity = from.Prim.Velocity;
                update.LocalID = from.Prim.LocalID;
            }
            update.Avatar = (from is SimAvatar);
            return update;
        }

        public static ObjectMovementUpdate updatFromPrim0(Primitive fromPrim)
        {
            ObjectMovementUpdate update;// = new ObjectMovementUpdate();
            update.Acceleration = fromPrim.Acceleration;
            update.AngularVelocity = fromPrim.AngularVelocity;
            update.CollisionPlane = fromPrim.CollisionPlane;
            update.Position = fromPrim.Position;
            update.Rotation = fromPrim.Rotation;
            update.State = fromPrim.PrimData.State;
            update.Textures = fromPrim.Textures;
            update.Velocity = fromPrim.Velocity;
            update.LocalID = fromPrim.LocalID;
            update.Avatar = (fromPrim is Avatar);
            return update;
        }


        public static ObjectMovementUpdate updateDiff(ObjectMovementUpdate fromPrim, ObjectMovementUpdate diff)
        {
            ObjectMovementUpdate update;// = new ObjectMovementUpdate();
            update.LocalID = fromPrim.LocalID;
            update.Avatar = fromPrim.Avatar;
            update.Acceleration = fromPrim.Acceleration - diff.Acceleration;
            update.AngularVelocity = fromPrim.AngularVelocity - diff.AngularVelocity;
            update.CollisionPlane = fromPrim.CollisionPlane - diff.CollisionPlane;
            update.Position = fromPrim.Position - diff.Position;
            update.Rotation = fromPrim.Rotation - diff.Rotation;
            update.State = (byte)(fromPrim.State - diff.State);
            update.Textures = fromPrim.Textures != diff.Textures ? diff.Textures : null;
            update.Velocity = fromPrim.Velocity - diff.Velocity;
            return update;
        }


        public static void updateToPrim(Primitive prim, ObjectMovementUpdate update)
        {
            prim.Acceleration = update.Acceleration;
            prim.AngularVelocity = update.AngularVelocity;
            prim.CollisionPlane = update.CollisionPlane;
            prim.Position = update.Position;
            prim.Rotation = update.Rotation;
            prim.PrimData.State = update.State;
            if (update.Textures != null) prim.Textures = update.Textures;
            prim.Velocity = update.Velocity;
        }


        public string describeAvatar(Avatar avatar)
        {
            //	string verb;
            //if (avatar.SittingOn == 0)
            //    verb = "standing";
            //else
            //    verb = "sitting";
            //WriteLine(avatar.Name + " is " + verb + " in " + avatar.CurrentSim.Name + ".");
            if (avatar == null) return "NULL avatar";
            string s = String.Empty;
            s += (avatar.Name + " is " + TheSimAvatar.DistanceVectorString(GetSimObject(avatar)) + " distant.");
            if (!HasValue(avatar.ProfileProperties)) return s;
            if (avatar.ProfileProperties.BornOn != null)
                s += ("Born on: " + avatar.ProfileProperties.BornOn);
            if (avatar.ProfileProperties.AboutText != null)
                s += ("About their second life: " + avatar.ProfileProperties.AboutText);
            if (avatar.ProfileProperties.FirstLifeText != null)
                s += ("About their first life: " + avatar.ProfileProperties.FirstLifeText);
            if (avatar.ProfileInterests.LanguagesText != null)
                s += ("Languages spoken: " + avatar.ProfileInterests.LanguagesText);
            if (avatar.ProfileInterests.SkillsText != null)
                s += ("Skills: " + avatar.ProfileInterests.SkillsText);
            if (avatar.ProfileInterests.WantToText != null)
                s += ("Wants to: " + avatar.ProfileInterests.WantToText);
            return s;
        }

        public void describeAvatarToAI(Avatar avatar)
        {
            // string verb;
            // if (avatar.SittingOn == 0)
            //     verb = "standing";
            // else
            //     verb = "sitting";
            SimObject A = GetSimObject(avatar);
            //WriteLine(avatar.Name + " is " + verb + " in " + avatar.CurrentSim.Name + ".");
            //WriteLine(avatar.Name + " is " + Vector3.Distance(GetSimPosition(), avatar.Position).ToString() + " distant.");
            client.SendPersonalEvent(SimEventType.MOVEMENT, "on-avatar-dist", A, A.Distance(TheSimAvatar));
            SendNewRegionEvent(SimEventType.MOVEMENT, "on-avatar-pos", A, A.GlobalPosition);
            SendNewRegionEvent(SimEventType.EFFECT, "on-avatar-description", avatar, avatar.GroupName);
            //  botenqueueLispTask("(on-avatar-posture (@\"" + avatar.Name + "\") (@\"" + verb + "\") )");

            /*
            if (avatar.ProfileProperties.BornOn != null)
                WriteLine("Born on: " + avatar.ProfileProperties.BornOn);
            if (avatar.ProfileProperties.AboutText != null)
                WriteLine("About their second life: " + avatar.ProfileProperties.AboutText);
            if (avatar.ProfileProperties.FirstLifeText != null)
                WriteLine("About their first life: " + avatar.ProfileProperties.FirstLifeText);
            if (avatar.ProfileInterests.LanguagesText != null)
                WriteLine("Languages spoken: " + avatar.ProfileInterests.LanguagesText);
            if (avatar.ProfileInterests.SkillsText != null)
                WriteLine("Skills: " + avatar.ProfileInterests.SkillsText);
            if (avatar.ProfileInterests.WantToText != null)
                WriteLine("Wants to: " + avatar.ProfileInterests.WantToText);
            */
        }

        public void DelayedEval(Func<bool> func, Action action, int tries)
        {
            if (func())
            {
                action();
                return;
            }
            if (--tries < 0)
            {
                return;
            }
            OnConnectedQueue.Enqueue(() => DelayedEval(func, action, tries));
        }

        private void DeclareProperties(Primitive prim0, Primitive.ObjectProperties props, Simulator simulator)
        {
            if (props == null) return;
            if (prim0 != null)
            {
                bool go = PrimFlags.ObjectGroupOwned == (prim0.Flags & PrimFlags.ObjectGroupOwned);
                if (go && UUID.Zero != props.OwnerID)
                {
                    DeclareAvatarProfile(props.OwnerID);
                }
            }
            string debugInfo = "" + prim0;
            DeclareGroup(props.GroupID);

            if (UUID.Zero != props.OwnerID)
            {
                DeclareAvatarProfile(props.OwnerID);
            }
            if (UUID.Zero != props.LastOwnerID)
            {
                DeclareAvatarProfile(props.LastOwnerID);
            }
            if (UUID.Zero != props.FolderID)
            {
                if (ZeroOutUselessUUIDs) props.FolderID = UUID.Zero;
                //DeclareGeneric("Folder", props.FolderID, debugInfo);
            }

            if (UUID.Zero != props.ItemID)
            {
                if (ZeroOutUselessUUIDs) props.ItemID = UUID.Zero;
                //DeclareGeneric("Item", props.ItemID, debugInfo);
            }


            if (UUID.Zero != props.FromTaskID && client.Self.AgentID != props.FromTaskID)
            {
                if (DeclareTask(props.FromTaskID, simulator) == null)
                {
                    if (ZeroOutUselessUUIDs) props.FromTaskID = UUID.Zero;
                }
            }

            var tids = props.TextureIDs;
            if (tids != null)
            {
                foreach (UUID tid in tids)
                {
                    DeclareTexture(tid);
                }
            }
        }

    }
}
