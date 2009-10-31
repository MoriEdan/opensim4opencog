using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace cogbot.Actions
{
    public class ImportCommand : Command,RegionMasterCommand
    {
        private enum ImporterState
        {
            RezzingParent,
            RezzingChildren,
            Linking,
            Idle
        }

        private class Linkset
        {
            public Primitive RootPrim;
            public List<Primitive> Children = new List<Primitive>();

            public Linkset()
            {
                RootPrim = new Primitive();
            }

            public Linkset(Primitive rootPrim)
            {
                RootPrim = rootPrim;
            }
        }

        Primitive currentPrim;
        Vector3 currentPosition;
        AutoResetEvent primDone = new AutoResetEvent(false);
        List<Primitive> primsCreated;
        List<uint> linkQueue;
        uint rootLocalID;
        ImporterState state = ImporterState.Idle;
        EventHandler<PrimEventArgs> callback;

        public ImportCommand(BotClient testClient)
        {
            Name = "import";
            Description = "Import prims from an exported xml file. Usage: import inputfile.xml [usegroup]";
            Category = CommandCategory.Objects;
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            if (args.Length < 1)
                return ShowUsage();// " import inputfile.xml [usegroup]";

            if (callback == null)
            {
                callback = new EventHandler<PrimEventArgs>(Objects_OnNewPrim);
                Client.Objects.ObjectUpdate += callback;
            }
            try
            {
                string filename = args[0];
                UUID GroupID = (args.Length > 1) ? TheBotClient.GroupID : UUID.Zero;
                string xml;
                List<Primitive> prims;

                try { xml = File.ReadAllText(filename); }
                catch (Exception e) { return Failure(e.Message); }

                try { prims = ClientHelpers.OSDToPrimList(OSDParser.DeserializeLLSDXml(xml)); }
                catch (Exception e) { return Failure("failed to deserialize " + filename + ": " + e.Message); }

                // Build an organized structure from the imported prims
                Dictionary<uint, Linkset> linksets = new Dictionary<uint, Linkset>();
                for (int i = 0; i < prims.Count; i++)
                {
                    Primitive prim = prims[i];

                    if (prim.ParentID == 0)
                    {
                        if (linksets.ContainsKey(prim.LocalID))
                            linksets[prim.LocalID].RootPrim = prim;
                        else
                            linksets[prim.LocalID] = new Linkset(prim);
                    }
                    else
                    {
                        if (!linksets.ContainsKey(prim.ParentID))
                            linksets[prim.ParentID] = new Linkset();

                        linksets[prim.ParentID].Children.Add(prim);
                    }
                }

                primsCreated = new List<Primitive>();
                WriteLine("Importing " + linksets.Count + " structures.");

                foreach (Linkset linkset in linksets.Values)
                {
                    if (linkset.RootPrim.LocalID != 0)
                    {
                        Simulator CurSim = WorldSystem.GetSimulator(linkset.RootPrim);
                        state = ImporterState.RezzingParent;
                        currentPrim = linkset.RootPrim;
                        // HACK: Import the structure just above our head
                        // We need a more elaborate solution for importing with relative or absolute offsets
                        linkset.RootPrim.Position = GetSimPosition();
                        linkset.RootPrim.Position.Z += 3.0f;
                        currentPosition = linkset.RootPrim.Position;

                        // Rez the root prim with no rotation
                        Quaternion rootRotation = linkset.RootPrim.Rotation;
                        linkset.RootPrim.Rotation = Quaternion.Identity;

                        Client.Objects.AddPrim(CurSim, linkset.RootPrim.PrimData, GroupID,
                            linkset.RootPrim.Position, linkset.RootPrim.Scale, linkset.RootPrim.Rotation);

                        if (!primDone.WaitOne(10000, false))
                            return Failure( "Rez failed, timed out while creating the root prim.");

                        Client.Objects.SetPosition(CurSim, primsCreated[primsCreated.Count - 1].LocalID, linkset.RootPrim.Position);

                        state = ImporterState.RezzingChildren;

                        // Rez the child prims
                        foreach (Primitive prim in linkset.Children)
                        {
                            currentPrim = prim;
                            currentPosition = prim.Position + linkset.RootPrim.Position;

                            Client.Objects.AddPrim(CurSim, prim.PrimData, GroupID, currentPosition,
                                prim.Scale, prim.Rotation);

                            if (!primDone.WaitOne(10000, false))
                                return Failure( "Rez failed, timed out while creating child prim.");
                            Client.Objects.SetPosition(CurSim, primsCreated[primsCreated.Count - 1].LocalID, currentPosition);

                        }

                        // Create a list of the local IDs of the newly created prims
                        List<uint> primIDs = new List<uint>(primsCreated.Count);
                        primIDs.Add(rootLocalID); // Root prim is first in list.

                        if (linkset.Children.Count != 0)
                        {
                            // Add the rest of the prims to the list of local IDs
                            foreach (Primitive prim in primsCreated)
                            {
                                if (prim.LocalID != rootLocalID)
                                    primIDs.Add(prim.LocalID);
                            }
                            linkQueue = new List<uint>(primIDs.Count);
                            linkQueue.AddRange(primIDs);

                            // Link and set the permissions + rotation
                            state = ImporterState.Linking;
                            Client.Objects.LinkPrims(CurSim, linkQueue);

                            if (primDone.WaitOne(1000 * linkset.Children.Count, false))
                                Client.Objects.SetRotation(CurSim, rootLocalID, rootRotation);
                            else
                                WriteLine("Warning: Failed to link {0} prims", linkQueue.Count);

                        }
                        else
                        {
                            Client.Objects.SetRotation(CurSim, rootLocalID, rootRotation);
                        }

                        // Set permissions on newly created prims
                        Client.Objects.SetPermissions(CurSim, primIDs,
                            PermissionWho.Everyone | PermissionWho.Group | PermissionWho.NextOwner,
                            PermissionMask.All, true);

                        state = ImporterState.Idle;
                    }
                    else
                    {
                        // Skip linksets with a missing root prim
                        WriteLine("WARNING: Skipping a linkset with a missing root prim");
                    }

                    // Reset everything for the next linkset
                    primsCreated.Clear();
                }
                return Success("Import complete.");
            }
            finally
            {
                Client.Objects.ObjectUpdate -= callback;
                callback = null;
            }
        }

        void Objects_OnNewPrim(object s, PrimEventArgs e)
        {
            Simulator simulator = e.Simulator;
            Primitive prim = e.Prim;
            if ((prim.Flags & PrimFlags.CreateSelected) == 0)
                return; // We received an update for an object we didn't create

            switch (state)
            {
                case ImporterState.RezzingParent:
                    rootLocalID = prim.LocalID;
                    goto case ImporterState.RezzingChildren;
                case ImporterState.RezzingChildren:
                    if (!primsCreated.Contains(prim))
                    {
                        WriteLine("Setting properties for " + prim.LocalID);
                        // TODO: Is there a way to set all of this at once, and update more ObjectProperties stuff?
                        Client.Objects.SetPosition(simulator, prim.LocalID, currentPosition);
                        Client.Objects.SetTextures(simulator, prim.LocalID, currentPrim.Textures);

                        if (currentPrim.Light.Intensity > 0) {
                            Client.Objects.SetLight(simulator, prim.LocalID, currentPrim.Light);
                        }

                        Client.Objects.SetFlexible(simulator, prim.LocalID, currentPrim.Flexible);
 
                        if (currentPrim.Sculpt.SculptTexture != UUID.Zero) {
                            Client.Objects.SetSculpt(simulator, prim.LocalID, currentPrim.Sculpt);
                        }

                        if (!String.IsNullOrEmpty(currentPrim.Properties.Name))
                            Client.Objects.SetName(simulator, prim.LocalID, currentPrim.Properties.Name);
                        if (!String.IsNullOrEmpty(currentPrim.Properties.Description))
                            Client.Objects.SetDescription(simulator, prim.LocalID, currentPrim.Properties.Description);

                        primsCreated.Add(prim);
                        primDone.Set();
                    }
                    break;
                case ImporterState.Linking:
                    lock (linkQueue)
                    {
                        int index = linkQueue.IndexOf(prim.LocalID);
                        if (index != -1)
                        {
                            linkQueue.RemoveAt(index);
                            if (linkQueue.Count == 0)
                                primDone.Set();
                        }
                    }
                    break;
            }
        }
    }
}
