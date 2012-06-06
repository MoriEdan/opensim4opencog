using OpenMetaverse;

namespace Cogbot.World
{
    public class SimNotecard : SimAsset
    {
        public SimNotecard(UUID uuid, string name):base(uuid,name,OpenMetaverse.AssetType.Notecard)
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