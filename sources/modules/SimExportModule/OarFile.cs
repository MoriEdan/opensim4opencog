using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.IO;
using System.Xml;
using Cogbot;
using Cogbot.Actions;
using Cogbot.World;
using MushDLR223.Utilities;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.StructuredData;

using MushDLR223.ScriptEngines;

namespace SimExportModule
{
    public partial class OarFile
    {

        public static void PrepareDir(string directoryname)
        {
            try
            {
                if (!Directory.Exists(directoryname)) Directory.CreateDirectory(directoryname);
                if (!Directory.Exists(directoryname + "/assets")) Directory.CreateDirectory(directoryname + "/assets");
                if (!Directory.Exists(directoryname + "/objects")) Directory.CreateDirectory(directoryname + "/objects");
                if (!Directory.Exists(directoryname + "/terrains")) Directory.CreateDirectory(directoryname + "/terrains");
            }
            catch (Exception ex) { Logger.Log(ex.Message, Helpers.LogLevel.Error); return; }
        }

        public static void PackageArchive(string directoryName, string filename, bool includeTerrain, bool includeLandData)
        {
            const string ARCHIVE_XML = "<?xml version=\"1.0\" encoding=\"utf-16\"?>\n<archive major_version=\"0\" minor_version=\"1\" />";

            TarArchiveWriter archive = new TarArchiveWriter();

            // Create the archive.xml file
            archive.AddFile("archive.xml", ARCHIVE_XML);

            // Add the assets
            string[] files = Directory.GetFiles(directoryName + "/assets");
            foreach (string file in files)
                archive.AddFile("assets/" + Path.GetFileName(file), File.ReadAllBytes(file));

            // Add the objects
            files = Directory.GetFiles(directoryName + "/objects");
            foreach (string file in files)
                archive.AddFile("objects/" + Path.GetFileName(file), File.ReadAllBytes(file));

            // Add the terrain(s)
            files = Directory.GetFiles(directoryName + "/terrains");
            if (includeTerrain) foreach (string file in files)
                    archive.AddFile("terrains/" + Path.GetFileName(file), File.ReadAllBytes(file));

            // Add the terrain(s)
            files = Directory.GetFiles(directoryName + "/landdata");
            if (includeLandData) foreach (string file in files)
                    archive.AddFile("landdata/" + Path.GetFileName(file), File.ReadAllBytes(file));

            File.Delete(filename);
            archive.WriteTar(new GZipStream(new FileStream(filename, FileMode.Create), CompressionMode.Compress));
        }

        static void WriteBytes(XmlTextWriter writer, string name, byte[] data)
        {
            writer.WriteStartElement(name);
            byte[] d;
            if (data != null)
                d = data;
            else
                d = Utils.EmptyBytes;
            writer.WriteBase64(d, 0, d.Length);
            writer.WriteEndElement(); // name

        }

        static void WriteFlags0(XmlTextWriter writer, string name, string flagsStr, ImportSettings options)
        {
            // Older versions of serialization can't cope with commas, so we eliminate the commas
            writer.WriteElementString(name, flagsStr.Replace(",", " ").Replace("  ", " "));
        }
        static void WriteFlags(XmlTextWriter writer, string name, Enum flagsStr, ImportSettings options)
        {
            // Older versions of serialization can't cope with commas, so we eliminate the commas
            WriteEnum(writer, name, flagsStr);
        }


        static void WriteUUID(XmlTextWriter writer, string name, UUID id, ImportSettings options)
        {
            writer.WriteStartElement(name);
            if (options.ContainsKey("old-guids"))
                writer.WriteElementString("Guid", id.ToString());
            else
                writer.WriteElementString("UUID", id.ToString());
            writer.WriteEndElement();
        }


        static void WriteUUID(XmlTextWriter writer, string name, UUID id)
        {
            writer.WriteStartElement(name);
            writer.WriteElementString("UUID", id.ToString());
            writer.WriteEndElement();
        }

        static void WriteVector(XmlTextWriter writer, string name, Vector3 vec)
        {
            writer.WriteStartElement(name);
            writer.WriteElementString("X", vec.X.ToString());
            writer.WriteElementString("Y", vec.Y.ToString());
            writer.WriteElementString("Z", vec.Z.ToString());
            writer.WriteEndElement();
        }

        static void WriteQuaternion(XmlTextWriter writer, string name, Quaternion quat)
        {
            writer.WriteStartElement(name);
            writer.WriteElementString("X", quat.X.ToString());
            writer.WriteElementString("Y", quat.Y.ToString());
            writer.WriteElementString("Z", quat.Z.ToString());
            writer.WriteElementString("W", quat.W.ToString());
            writer.WriteEndElement();
        }

        private static void WriteEnum(XmlWriter writer, string name, Enum mask)
        {
            Type t = Enum.GetUnderlyingType(mask.GetType());
            writer.WriteElementString(name, OSD.InvokeCast(mask, t).ToString());
        }
        private static void WriteValue(XmlTextWriter writer, string name, IComparable<string> value)
        {
            writer.WriteElementString(name, value.ToString());
        }
        private static void WriteUInt(XmlTextWriter writer, string name, ulong value)
        {
            writer.WriteElementString(name, value.ToString());
        }
        private static void WriteFloat(XmlTextWriter writer, string name, IComparable<float> value)
        {
            writer.WriteElementString(name, value.ToString());
        }
        private static void WriteInt(XmlTextWriter writer, string name, long value)
        {
            writer.WriteElementString(name, value.ToString());
        }
        private static void WriteDate(XmlTextWriter writer, string name, DateTime time)
        {
            WriteValue(writer, name, ((int)Utils.DateTimeToUnixTime(time)).ToString());
        }
        private static Enum Reperm(PermissionMask mask, ImportSettings settings)
        {
            if (settings.Contains("sameperms")) return mask;
            if (settings.Contains("+xfer+copy")) return mask | PermissionMask.Copy | PermissionMask.Transfer;
            return PermissionMask.All;
        }
        private static void WriteUserUUID(XmlTextWriter writer, string name, UUID uuid, ImportSettings settings)
        {
            WriteUUID(writer, name, uuid, settings);
        }
    }
}
