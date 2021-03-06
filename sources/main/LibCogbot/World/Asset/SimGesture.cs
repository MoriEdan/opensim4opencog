using System;
using System.Collections.Generic;
using System.Text;
using Cogbot;
using OpenMetaverse;
using OpenMetaverse.Assets;

namespace Cogbot.World
{
    internal class SimGesture : SimAsset
    {
        static readonly Dictionary<UUID,SimGesture> AnimationGestures = new Dictionary<UUID, SimGesture>();

        public SimGesture(UUID uuid, string name)
            : base(uuid, name, AssetType.Gesture)
        {
        }

        public AssetGesture GetGesture()
        {
            return (AssetGesture)ServerAsset;
        }

        private void tbtnReupload_Click(object sender, EventArgs e)
        {
            AssetGesture gestureAsset = GetGesture();
            GridClient client = WorldObjects.GridMaster.client;
            InventoryItem gesture = Item;
            UpdateStatus("Creating new item...");

            client.Inventory.RequestCreateItem(gesture.ParentUUID, "Copy of " + gesture.Name, gesture.Description, AssetType.Gesture, UUID.Random(), InventoryType.Gesture, PermissionMask.All,
                delegate(bool success, InventoryItem item)
                {
                    if (success)
                    {
                        UpdateStatus("Uploading data...");

                        client.Inventory.RequestUploadGestureAsset(gestureAsset.AssetData, item.UUID,
                            delegate(bool assetSuccess, string status, UUID itemID, UUID assetID)
                            {
                                if (assetSuccess)
                                {
                                    gesture.AssetUUID = assetID;
                                    UpdateStatus("OK");
                                }
                                else
                                {
                                    UpdateStatus("Asset failed");
                                }
                            }
                        );
                    }
                    else
                    {
                        UpdateStatus("Inv. failed");
                    }
                }
            );
        }

        private void UpdateStatus(string s)
        {
           // throw new NotImplementedException();
        }

        protected override string GuessAssetName()
        {
            if (Item != null)
            {
                return Item.Name;
            }
            String s = "";
            if (_ServerAsset == null) return null;
            Decode(ServerAsset);
            AssetGesture gestureAsset = GetGesture();
            for (int i = 0; i < gestureAsset.Sequence.Count; i++)
            {
                s += (gestureAsset.Sequence[i].ToString().Trim() + Environment.NewLine);
            }
            if (!string.IsNullOrEmpty(s)) return s;
            AssetGesture S = (AssetGesture)ServerAsset;
            AssetData = S.AssetData;
            return UnknownName;
        }

        protected override List<SimAsset> GetParts()
        {

            AssetGesture gestureAsset = GetGesture();
            //            StringBuilder sb = new StringBuilder();
            //sb.Append("2\n");
            if (gestureAsset == null) return null;
            Name  = gestureAsset.Trigger;
            //sb.Append(TriggerKey + "\n");
            //sb.Append(TriggerKeyMask + "\n");
            //sb.Append(Trigger + "\n");
            Name = gestureAsset.ReplaceWith;//sb.Append(ReplaceWith + "\n");

            List<GestureStep> Sequence = gestureAsset.Sequence;
            int count = 0;
            if (Sequence != null)
            {
                count = Sequence.Count;
            }
            List<SimAsset> parts = new List<SimAsset>(count);
            //sb.Append(count + "\n");
            _Length = 0;
            for (int i = 0; i < count; i++)
            {
                GestureStep step = Sequence[i];
                // sb.Append((int)step.GestureStepType + "\n");
                SimAsset asset;

                switch (step.GestureStepType)
                {
                    case GestureStepType.EOF:
                        goto Finish;

                    case GestureStepType.Animation:
                        GestureStepAnimation animstep = (GestureStepAnimation) step;
                        asset = SimAssetStore.FindOrCreateAsset(animstep.ID, AssetType.Animation);
                        asset.Name = animstep.Name;
                        if (animstep.AnimationStart)
                        {
                            parts.Add(asset);
                            //                            sb.Append("0\n");
                            _Length += asset.Length;
                        }
                        else
                        {
                            //             sb.Append("1\n");
                        }
                        break;

                    case GestureStepType.Sound:
                        GestureStepSound soundstep = (GestureStepSound) step;
                        asset = SimAssetStore.FindOrCreateAsset(soundstep.ID, AssetType.Sound);
                        asset.Name = soundstep.Name;
                        parts.Add(asset);
                        _Length += asset.Length;
                        break;

                    case GestureStepType.Chat:
                        GestureStepChat chatstep = (GestureStepChat) step;
                        Name = chatstep.Text;
                        //sb.Append(chatstep.Text + "\n");
                        //sb.Append("0\n");
                        _Length += 10;
                        break;

                    case GestureStepType.Wait:
                        GestureStepWait waitstep = (GestureStepWait) step;
                        //sb.AppendFormat("{0:0.000000}\n", waitstep.WaitTime);
                        int waitflags = 0;

                        if (waitstep.WaitForTime)
                        {
                            waitflags |= 0x01;
                            _Length += waitstep.WaitTime;
                        }
                        if (waitstep.WaitForAnimation)
                        {
                            waitflags |= 0x02;
                            _Length += 10;
                        }
                        //sb.Append(waitflags + "\n");
                        break;
                }
            }
            Finish:

            if (parts.Count==1)
            {
                SimAsset A = parts[0];
                if (A is SimAnimation)
                {
                    AnimationGestures[A.AssetID] = this;
                    if (A.Item == null) A.Item = Item;
                }
            }
            return parts;
        }

        private float _Length = 1000f;

        public override float Length
        {
            get
            {
                GetParts();
                return _Length;
            }
        }

        public override bool IsContinuousEffect
        {
            get { return false; }
        }

        public static InventoryItem SaveGesture(SimAnimation animation)
        {
            return null;
         //   throw new NotImplementedException();
        }
    }
}