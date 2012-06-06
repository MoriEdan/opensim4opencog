using System;
using System.Collections.Generic;
using MushDLR223.Utilities;
using Drawing2D = System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Drawing;
using OpenMetaverse;
using OpenMetaverse.Http;
using OpenMetaverse.Imaging;

using MushDLR223.ScriptEngines;

namespace Cogbot.Actions.SimExport
{
    public class UploadImageCommand : Command, BotPersonalCommand
    {
        AutoResetEvent UploadCompleteEvent = new AutoResetEvent(false);
        UUID TextureID = UUID.Zero;
        DateTime start;

        public UploadImageCommand(BotClient testClient)
        {
            Name = "uploadimage";
            Description = "Upload an image to your inventory. Usage: uploadimage [inventoryname] [timeout] [filename]";
            Category = CommandCategory.Inventory;
        }

        public override CmdResult ExecuteRequest(CmdRequest args)
        {
            string inventoryName;
            uint timeout;
            string fileName;

            if (args.Length != 3)
                return ShowUsage();// " uploadimage [inventoryname] [timeout] [filename]";

            TextureID = UUID.Zero;
            inventoryName = args[0];
            fileName = args[2];
            if (!UInt32.TryParse(args[1], out timeout))
                return ShowUsage();// " uploadimage [inventoryname] [timeout] [filename]";

            WriteLine("Loading image " + fileName);
            byte[] jpeg2k = LoadImage(fileName);
            if (jpeg2k == null)
                return Failure("failed to compress image to JPEG2000");
            WriteLine("Finished compressing image to JPEG2000, uploading...");
            start = DateTime.Now;
            DoUpload(jpeg2k, inventoryName);

            if (UploadCompleteEvent.WaitOne((int)timeout, false))
            {
                return
                    Success(string.Format("Texture upload {0}: {1}", (TextureID != UUID.Zero) ? "succeeded" : "failed",
                                          TextureID));
            }
            else
            {
                return Failure( "Texture upload timed out");
            }
        }

        private void DoUpload(byte[] UploadData, string FileName)
        {
            if (UploadData != null)
            {
                string name = Path.GetFileNameWithoutExtension(FileName);

                Client.Inventory.RequestCreateItemFromAsset(UploadData, name, "Uploaded with BotClient",
                    AssetType.Texture, InventoryType.Texture, Client.Inventory.FindFolderForType(AssetType.Texture),
                    delegate(bool success, string status, UUID itemID, UUID assetID)
                    {
                        WriteLine(String.Format(
                            "RequestCreateItemFromAsset() returned: Success={0}, Status={1}, ItemID={2}, AssetID={3}",
                            success, status, itemID, assetID));

                        TextureID = assetID;
                        WriteLine(String.Format("Upload took {0}", DateTime.Now.Subtract(start)));
                        UploadCompleteEvent.Set();
                    }
                );
            }
        }

        private byte[] LoadImage(string fileName)
        {
            byte[] UploadData;
            string lowfilename = fileName.ToLower();
            Bitmap bitmap = null;

            try
            {
                if (lowfilename.EndsWith(".jp2") || lowfilename.EndsWith(".j2c"))
                {
                    Image image;
                    ManagedImage managedImage;

                    // Upload JPEG2000 images untouched
                    UploadData =  File.ReadAllBytes(fileName);

                    OpenJPEG.DecodeToImage(UploadData, out managedImage, out image);
                    bitmap = (Bitmap)image;
                }
                else
                {
                    if (lowfilename.EndsWith(".tga"))
                        bitmap = LoadTGAClass.LoadTGA(fileName);
                    else
                        bitmap = (Bitmap) Image.FromFile(fileName);

                    int oldwidth = bitmap.Width;
                    int oldheight = bitmap.Height;

                    if (!IsPowerOfTwo((uint)oldwidth) || !IsPowerOfTwo((uint)oldheight))
                    {
                        Bitmap resized = new Bitmap(256, 256, bitmap.PixelFormat);
                        Graphics graphics = Graphics.FromImage(resized);

                        graphics.SmoothingMode =  Drawing2D.SmoothingMode.HighQuality;
                        graphics.InterpolationMode =
                            Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.DrawImage(bitmap, 0, 0, 256, 256);

                        bitmap.Dispose();
                        bitmap = resized;

                        oldwidth = 256;
                        oldheight = 256;
                    }

                    // Handle resizing to prevent excessively large images
                    if (oldwidth > 1024 || oldheight > 1024)
                    {
                        int newwidth = (oldwidth > 1024) ? 1024 : oldwidth;
                        int newheight = (oldheight > 1024) ? 1024 : oldheight;

                        Bitmap resized = new Bitmap(newwidth, newheight, bitmap.PixelFormat);
                        Graphics graphics = Graphics.FromImage(resized);

                        graphics.SmoothingMode = Drawing2D.SmoothingMode.HighQuality;
                        graphics.InterpolationMode =
                          Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.DrawImage(bitmap, 0, 0, newwidth, newheight);

                        bitmap.Dispose();
                        bitmap = resized;
                    }

                    UploadData = OpenJPEG.EncodeFromImage(bitmap, false);
                }
            }
            catch (Exception ex)
            {
                WriteLine(ex.ToString() + " SL Image Upload ");
                return null;
            }
            return UploadData;
        }

        private static bool IsPowerOfTwo(uint n)
        {
            return (n & (n - 1)) == 0 && n != 0;
        }
    }
}