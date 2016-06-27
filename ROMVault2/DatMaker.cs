/******************************************************
 *     ROMVault2 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2014                                 *
 ******************************************************/

using System;
using System.IO;
using ROMVault2.RvDB;

namespace ROMVault2
{
    public static class DatMaker
    {
        private static StreamWriter _sw;
        private static string _datName;
        private static string _datDir;

        public static void MakeDatFromDir(RvDir startingDir, bool CHDsAreDisk = true)
        {
            _datName = startingDir.Name;
            _datDir = startingDir.Name;
            Console.WriteLine("Creating Dat: " + startingDir.Name + ".dat");
            _sw = new StreamWriter(startingDir.Name + ".dat");

            WriteDatFile(startingDir, CHDsAreDisk);

            _sw.Close();

            Console.WriteLine("Dat creation complete");
        }

    private static void WriteDatFile(RvDir dir, bool CHDsAreDisk)
        {
            WriteLine("<?xml version=\"1.0\"?>");
            WriteLine("");
            WriteLine("<datafile>");
            WriteHeader(CHDsAreDisk ? "CHDs as disk - if you see lots of status=nodump, try the other way" : 
                "CHD as rom - if you see lots of empty double-quotes, try the other way");

            /* write Games/Dirs */
            if (CHDsAreDisk) ProcessDir(dir); else PlainProcessDir(dir);

            WriteLine("</datafile>");
        }

        private static void WriteHeader(string comment)
        {
            WriteLine("\t<header>");
            WriteLine("\t\t<name>" + clean(_datName) + "</name>");
            WriteLine("\t\t<rootdir>" + clean(_datDir) + "</rootdir>");
            WriteLine("\t\t<comment>" + clean(comment) + "</comment>");
            WriteLine("\t</header>");
        }

        private static void WriteLine(string s)
        {
            _sw.WriteLine(s);
        }

        private static string clean(string s)
        {
            s = s.Replace("&", "&amp;");
            s = s.Replace("\"", "&quot;");
            s = s.Replace("'", "&apos;");
            s = s.Replace("<", "&lt;");
            s = s.Replace(">", "&gt;");
            return s;
        }

        private static bool hasChdGrandChildren(RvDir aDir)
        {
            if (aDir == null) return false;
            for (int i = 0; i < aDir.ChildCount; i++)
            {
                RvDir item = aDir.Child(i) as RvDir;
                if (item == null) continue;
                else for (int j = 0; j < item.ChildCount; j++)
                        if (item.Child(j).Name.ToLower().EndsWith(".chd")) return true;
            }
            return false;
        }

        // CHDs as rom
        private static void PlainProcessDir(RvDir dir, int depth = 1)
        {
            string indent = new string('\t', depth);
            for (int i = 0; i < dir.ChildCount; i++)
            {
                RvDir item = dir.Child(i) as RvDir;
                if (item != null && (item.FileType == FileType.Zip || item.FileType == FileType.Dir))
                {
                    WriteLine(indent + "<game name=\"" + clean(item.Name) + "\">");
                    WriteLine(indent + "\t<description>" + clean(item.Game == null ? item.Name : item.Game.GetData(RvGame.GameData.Description)) + "</description>");

                    for (int j = 0; j < item.ChildCount; j++)
                    {
                        RvFile file = item.Child(j) as RvFile;
                        if (file != null)
                            WriteLine(indent + "\t<rom name=\"" + clean(file.Name) + "\" size=\"" + file.Size + "\" crc=\"" + Utils.ArrByte.ToString(file.CRC) + "\" md5=\"" + Utils.ArrByte.ToString(file.MD5) + "\" sha1=\"" + Utils.ArrByte.ToString(file.SHA1) + "\"/>");
                        RvDir aDir = item.Child(j) as RvDir;
                        if (aDir != null)
                        {
                            string dName = aDir.Name;
                            for (int k = 0; k < aDir.ChildCount; k++)
                            {
                                RvFile subFile = aDir.Child(k) as RvFile;
                                WriteLine(indent + "\t<rom name=\"" + dName + "\\" + clean(subFile.Name) + "\" size=\"" + subFile.Size + "\" crc=\"" + Utils.ArrByte.ToString(subFile.CRC) + "\" md5=\"" + Utils.ArrByte.ToString(subFile.MD5) + "\" sha1=\"" + Utils.ArrByte.ToString(subFile.SHA1) + "\"/>");
                            }
                        }
                    }
                    WriteLine(indent + "</game>");
                }
                // only recurse when grandchildren are not CHDs
                if (item != null && item.FileType == FileType.Dir && !hasChdGrandChildren(item))
                {
                    WriteLine(indent + "<dir name=\"" + clean(item.Name) + "\">");
                    PlainProcessDir(item, depth + 1);
                    WriteLine(indent + "</dir>");
                }
            }
        }

        // returns number of CHD files in a RvDir
        // will be confused if there are any RvDirs as well as CHD files in 'dir'
        // does not check sub-dirs of 'dir'
        private static int numDisks(RvDir dir)
        {
            int retVal = 0;
            if (dir != null)
            {
                for (int i = 0; i < dir.ChildCount; i++)
                {
                    RvFile chd = dir.Child(i) as RvFile;
                    if (chd != null && chd.FileType == FileType.File && chd.Name.EndsWith(".chd")) retVal++;
                }
            }
            return retVal;
        }

        // writes a list of CHDs as a game when there are no ROMs in the game
        private static void justCHDs(string indent, System.Collections.Generic.List<string> lst)
        {
            WriteLine(indent + "<game name=\"" + clean(lst[0]) + "\">");
            WriteLine(indent + "\t<description>" + clean(lst[1]) + "</description>");
            for (int j = 2; j < lst.Count; j++) WriteLine(lst[j]);
            WriteLine(indent + "</game>");
        }

        // CHDs as disk
        private static void ProcessDir(RvDir dir, int depth = 1)
        {
            string indent = new string('\t', depth);  // recursive indent
            System.Collections.Generic.List<string> disks = new System.Collections.Generic.List<string>() { string.Empty };

            for (int i = 0; i < dir.ChildCount; i++)
            {
                RvDir item = dir.Child(i) as RvDir;
                if (item != null && item.FileType == FileType.Dir)
                {
                    if (disks.Count > 2 && item.Name != disks[0]) // flush the last one if there were only CHDs in it
                    {
                        justCHDs(indent, disks);
                        disks.Clear();
                    }
                // tabulate next disk list, if any
                disks = new System.Collections.Generic.List<string>()
                    { item.Name, item.Game == null ? item.Name : item.Game.GetData(RvGame.GameData.Description) };
                for (int j = 0; j < item.ChildCount; j++)
                    {
                        RvFile chd = item.Child(j) as RvFile;
                        if (chd != null && chd.FileType == FileType.File && chd.Name.EndsWith(".chd"))
                        {
                            if (!string.IsNullOrEmpty(Utils.ArrByte.ToString(chd.SHA1CHD)))
                                disks.Add((indent + "\t<disk name=\"" + clean(chd.Name).Replace(".chd", "") + "\" sha1=\"" + Utils.ArrByte.ToString(chd.SHA1CHD) + "\"/>"));
                            else
                                disks.Add((indent + "\t<disk name=\"" + clean(chd.Name).Replace(".chd", "") + "\" status=\"nodump\"/>"));
                        }
                    }
                }
                if (item != null && item.FileType == FileType.Zip)
                {
                    WriteLine(indent + "<game name=\"" + clean(item.Name) + "\">");
                    string desc = item.Game == null ? item.Name : item.Game.GetData(RvGame.GameData.Description);
                    WriteLine(indent + "\t<description>" + clean(desc) + "</description>");

                    for (int j = 0; j < item.ChildCount; j++)
                    {
                        RvFile file = item.Child(j) as RvFile;
                        if (file != null)
                        {
                            WriteLine(indent + "\t<rom name=\"" + clean(file.Name) + "\" size=\"" + file.Size + "\" crc=\"" + Utils.ArrByte.ToString(file.CRC) + "\" md5=\"" + Utils.ArrByte.ToString(file.MD5) + "\" sha1=\"" + Utils.ArrByte.ToString(file.SHA1) + "\"/>");
                        }
                    }

                    if (disks.Count > 2) // take care of previous list of CHDs now
                    {
                        for (int j = 2; j < disks.Count; j++) WriteLine(disks[j]);
                        disks.Clear();
                    }

                    WriteLine(indent + "</game>");
                }
                
                if (item != null && item.FileType == FileType.Dir)
                {
                    if (numDisks(item) == 0) // only recurse when children are not CHDs
                    {
                        WriteLine(indent + "<dir name=\"" + clean(item.Name) + "\">");
                        ProcessDir(item, depth + 1);
                        WriteLine(indent + "</dir>");
                    }
                }
            }
            // check for one last CHDs-only game
            if (disks.Count > 2) justCHDs(indent, disks);
        }
    }
}
