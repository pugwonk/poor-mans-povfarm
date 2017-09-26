using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Farm
{
    class Program
    {
        static void Main(string[] args)
        {
            // Check every so often for new files
            Console.WriteLine("Press a key to exit.");
            bool done = false;
            do
            {
                done = checkFiles();
                System.Threading.Thread.Sleep(1000);
            } while ((!Console.KeyAvailable) && (!done));
        }

        private static bool checkFiles()
        {
            int firstFrame = 0;
            int lastFrame = 0;

            var dir = new DirectoryInfo(".");
            var inis = dir.EnumerateFiles("*.ini");
            if (inis.Count() > 0)
            {
                // Pick a random one
                int selectIni = 0;
                FileInfo iniFile = inis.ElementAt(selectIni);
                Console.WriteLine("Found " + iniFile.FullName);
                using (StreamReader getLines = new StreamReader(iniFile.FullName))
                {
                    string line;
                    while ((line = getLines.ReadLine()) != null)
                    {
                        if (line.StartsWith("Final_Frame="))
                            lastFrame = int.Parse(line.Substring("Final_Frame=".Length));
                        if (line.StartsWith("Initial_Frame="))
                            firstFrame = int.Parse(line.Substring("Initial_Frame=".Length));
                    }
                    Console.WriteLine("First frame is " + firstFrame.ToString());
                    Console.WriteLine("Last frame is " + lastFrame.ToString());
                    Console.WriteLine("Picking a random frame...");
                    int[] frames = new int[lastFrame - firstFrame + 1];
                    for (int i = firstFrame; i <= lastFrame; i++)
                    {
                        frames[i - firstFrame] = i;
                    }
                    Random rnd = new Random();
                    int[] randomFrames = frames.OrderBy(x => rnd.Next()).ToArray();
                    // The filenames are padded to fill out to the last frame
                    int padding = lastFrame.ToString().Length;
                    var newName = Path.GetFileNameWithoutExtension(iniFile.FullName);
                    int doingFrame = 0;
                    foreach (int fn in randomFrames)
                    {
                        string imgFile = newName + fn.ToString(new string('0', padding)) + ".png";
                        if (!File.Exists(imgFile))
                        {
                            Console.WriteLine("Found undrawn frame at " + imgFile);
                            doingFrame = fn;
                            break;
                        }
                    }
                    if (doingFrame == 0)
                    {
                        Console.WriteLine("All frames drawn.");
                        return true;
                    }
                    else
                    {
                        if (lockWorks(doingFrame))
                        {
                            // Go ahead and render
                            ProcessStartInfo cmdsi = new ProcessStartInfo(@"\Program Files\POV-Ray\v3.7\bin\pvengine.exe");
                            cmdsi.Arguments = '"' + iniFile.FullName + "\" /exit -sf" + doingFrame.ToString() + " -ef" + doingFrame.ToString();
                            Process cmd = Process.Start(cmdsi);
                            cmd.WaitForExit();
                            System.Threading.Thread.Sleep(5000); // for dropbox sync
                            File.Delete(doingFrame.ToString() + "_" + System.Environment.MachineName + ".lock");
                        }
                        else
                        {
                            Console.WriteLine("Failed to get a lock on frame");
                        }
                    }
                }
            }
            return false;
        }

        private static bool lockWorks(int doingFrame)
        {
            File.Create(doingFrame.ToString() + "_" + System.Environment.MachineName + ".lock").Close();
            System.Threading.Thread.Sleep(5000);
            var dir = new DirectoryInfo(".");
            var locks = dir.EnumerateFiles(doingFrame.ToString() + "_*.lock");
            return locks.Count() == 1;
        }
    }
}
