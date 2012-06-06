using System;
using OpenMetaverse;

namespace Cogbot.World
{
    public class SimLandmark : SimAsset
    {
        public SimLandmark(UUID uuid, string name)
            : base(uuid, name, OpenMetaverse.AssetType.CallingCard)
        {

        }
        protected override string GuessAssetName()
        {
            if (_ServerAsset == null) return UnknownName;
            Decode(ServerAsset);
            //  Ass S = (AssetCallingCard)ServerAsset;
            //AssetData = S.AssetData;
            return UnknownName;
        }
    }
}