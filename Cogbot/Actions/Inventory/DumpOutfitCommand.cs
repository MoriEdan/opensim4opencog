using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using cogbot.TheOpenSims;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.Imaging;

namespace cogbot.Actions
{
    public class DumpOutfitCommand : Command
    {
        List<UUID> OutfitAssets = new List<UUID>();

        public DumpOutfitCommand(BotClient testClient)
        {
            Name = "dumpoutfit";
            Description = "Dumps all of the textures from an avatars outfit to the hard drive. Usage: dumpoutfit [avatar-uuid]";
            Category = CommandCategory.Inventory;
            Parameters = new[] {  new NamedParam(typeof(SimObject), typeof(UUID)) };
        }

        public override CmdResult Execute(string[] args, UUID fromAgentID, OutputDelegate WriteLine)
        {
            if (args.Length < 1)
                return Failure(Usage); // " dumpoutfit [avatar-uuid]";

            //UUID target;

            //if (!UUIDTryParse(args, 0 , out target))
            //    return Failure(Usage);// " dumpoutfit [avatar-uuid]";

            //lock (Client.Network.Simulators)
            {
                //for (int i = 0; i < Client.Network.Simulators.Count; i++)
                {


                    int argsUsed;
                    List<Primitive> PS = WorldSystem.GetPrimitives(args, out argsUsed);
                    if (IsEmpty(PS)) return Failure("Cannot find objects from " + string.Join(" ", args));
                    foreach (var targetAv in PS)
                    {
                        StringBuilder output = new StringBuilder("Downloading ");

                        lock (OutfitAssets) OutfitAssets.Clear();

                        for (int j = 0; j < targetAv.Textures.FaceTextures.Length; j++)
                        {
                            Primitive.TextureEntryFace face = targetAv.Textures.FaceTextures[j];

                            if (face != null)
                            {
                                ImageType type = ImageType.Normal;

                                switch ((AvatarTextureIndex) j)
                                {
                                    case AvatarTextureIndex.HeadBaked:
                                    case AvatarTextureIndex.EyesBaked:
                                    case AvatarTextureIndex.UpperBaked:
                                    case AvatarTextureIndex.LowerBaked:
                                    case AvatarTextureIndex.SkirtBaked:
                                        type = ImageType.Baked;
                                        break;
                                }

                                OutfitAssets.Add(face.TextureID);
                                Client.Assets.RequestImage(face.TextureID, type, Assets_OnImageReceived);
                                output.Append(((AvatarTextureIndex) j).ToString());
                                output.Append(" ");
                            }
                        }

                        Success(output.ToString());
                    }
                }
            }
            return SuccessOrFailure();
        }

        private void Assets_OnImageReceived(TextureRequestState state, AssetTexture assetTexture)
        {
            lock (OutfitAssets)
            {
                if (OutfitAssets.Contains(assetTexture.AssetID))
                {
                    if (state == TextureRequestState.Finished)
                    {
                        try
                        {
                            File.WriteAllBytes(assetTexture.AssetID + ".jp2", assetTexture.AssetData);
                            Console.WriteLine("Wrote JPEG2000 image " + assetTexture.AssetID + ".jp2");

                            ManagedImage imgData;
                            OpenJPEG.DecodeToImage(assetTexture.AssetData, out imgData);
                            byte[] tgaFile = imgData.ExportTGA();
                            File.WriteAllBytes(assetTexture.AssetID + ".tga", tgaFile);
                            Console.WriteLine("Wrote TGA image " + assetTexture.AssetID + ".tga");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.ToString());
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to download image " + assetTexture.AssetID);
                    }

                    OutfitAssets.Remove(assetTexture.AssetID);
                }
            }
        }
    }
}
