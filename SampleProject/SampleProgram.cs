using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using TorrentParser;

namespace SampleProject
{
    class SampleProgram
    {
        static void Main(string[] args)
        {
            //get path of directory from which the app is launched
            string workDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\";
            string filePath = workDir + @"test.torrent";

            //initialize parser instance
            BEncodeParser ben = new BEncodeParser();
            //parse torrent file and get TorrentObject
            TorrentObject torrent = ben.BuildTorrent(filePath);

            //get all files and put them to separate list
            List<TorrentObject.FileStruct> listFiles = torrent.Info.Files;

            //print all file names from torrent in sorted (alpha-numeric) order
            Console.WriteLine("===Torrent consists of next files:");
            foreach (object o in torrent.Info.Files.OrderBy(x => x.FullPath))
            {
                //custom print method from 'PrintTree' class
                PrintTree.PrintTreeSimple(o, 1, 0);
            }
            Console.WriteLine();

            //get all files from folder "The Hunting Party".
            //  as files in different folders have different start index (first 2 symbols + one space) in its names,
            //  we get file name starting from 4 symbol:
            //      x.FileName.Substring(3)
            string[] list1 = listFiles.Where(x => x.FullPath.StartsWith("The Hunting Party")).Select(x => x.FileName.Substring(3)).ToArray();
            //get all files from folder "Compilation"
            string[] list2 = listFiles.Where(x => x.FullPath.StartsWith("Compilation")).Select(x => x.FileName.Substring(3)).ToArray();
            //get all files in folder "The Hunting Party" which are not included in folder "Compilation"
            string[] except = list1.Except(list2).ToArray();

            //print all selected files
            Console.WriteLine("===The Hunting Party unique files:");
            foreach (object o in except)
            {
                //custom print method from 'PrintTree' class
                PrintTree.PrintTreeSimple(o, 1, 0);
            }

            Console.WriteLine("\nAny key to exit...");
            Console.ReadKey();
        }

        /* App output:
            ===Torrent consists of next files:
            Compilation\01 All For Nothing (feat. Page Hamil.m4a
            Compilation\02 Guilty All the Same (feat. Rakim).m4a
            Compilation\03 Rebellion (feat. Daron Malakian).m4a
            Compilation\04 Drawbar (feat. Tom Morello).m4a
            Compilation\AlbumArtSmall.jpg
            Compilation\Folder.jpg
            The Hunting Party\01 Keys To the Kingdom.m4a
            The Hunting Party\02 All For Nothing (feat. Page Hamil.m4a
            The Hunting Party\03 Guilty All the Same (feat. Rakim).m4a
            The Hunting Party\04 The Summoning.m4a
            The Hunting Party\05 War.m4a
            The Hunting Party\06 Wastelands.m4a
            The Hunting Party\07 Until It's Gone.m4a
            The Hunting Party\08 Rebellion (feat. Daron Malakian).m4a
            The Hunting Party\09 Mark the Graves.m4a
            The Hunting Party\10 Drawbar (feat. Tom Morello).m4a
            The Hunting Party\11 Final Masquerade.m4a
            The Hunting Party\12 A Line In the Sand.m4a
            The Hunting Party\AlbumArtSmall.jpg
            The Hunting Party\Folder.jpg

            ===The Hunting Party unique files:
            A Line In the Sand.m4a
            Keys To the Kingdom.m4a
            The Summoning.m4a
            War.m4a
            Wastelands.m4a
            Until It's Gone.m4a
            Mark the Graves.m4a
            Final Masquerade.m4a

            Any key to exit...
        */
    }
}
