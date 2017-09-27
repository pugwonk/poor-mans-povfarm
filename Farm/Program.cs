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
        // https://stackoverflow.com/questions/842057/how-do-i-convert-a-timespan-to-a-formatted-string
        static string ToReadableAgeString(TimeSpan span)
        {
            return string.Format("{0:0}", span.Days / 365.25);
        }
        static string ToReadableString(TimeSpan span)
        {
            string formatted = string.Format("{0}{1}{2}",
                span.Duration().Days > 0 ? string.Format("{0:0} day{1}, ", span.Days, span.Days == 1 ? String.Empty : "s") : string.Empty,
                span.Duration().Hours > 0 ? string.Format("{0:0} hour{1}, ", span.Hours, span.Hours == 1 ? String.Empty : "s") : string.Empty,
                span.Duration().Minutes > 0 ? string.Format("{0:0} minute{1}, ", span.Minutes, span.Minutes == 1 ? String.Empty : "s") : string.Empty);

            if (formatted.EndsWith(", ")) formatted = formatted.Substring(0, formatted.Length - 2);

            if (string.IsNullOrEmpty(formatted)) formatted = "0 seconds";

            return formatted;
        }

        static void Main(string[] args)
        {
            // Check every so often for new files
            Console.WriteLine("Press a key to exit.");
            deleteOldLocks();
            do
            {
                checkFiles();
                System.Threading.Thread.Sleep(1000);
            } while (!Console.KeyAvailable);
        }

        private static void deleteOldLocks()
        {
            var dir = new DirectoryInfo(".");
            var locks = dir.EnumerateFiles("*_" + System.Environment.MachineName + ".lock");
            if (locks.Count() > 0)
            {
                Console.WriteLine("Deleting some old lock files");
                foreach (var lockf in locks) {
                    lockf.Delete();
                }
            }
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
                Random rnd = new Random();
                int selectIni = rnd.Next(inis.Count());
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
                    //Console.WriteLine("First frame is " + firstFrame.ToString());
                    //Console.WriteLine("Last frame is " + lastFrame.ToString());
                    if (lastFrame == 0)
                    {
                        Console.WriteLine("No animation in INI.");
                        return true;
                    }
                    //Console.WriteLine("Picking a random frame...");
                    int[] frames = new int[lastFrame - firstFrame + 1];
                    for (int i = firstFrame; i <= lastFrame; i++)
                    {
                        frames[i - firstFrame] = i;
                    }
                    int[] randomFrames = frames.OrderBy(x => rnd.Next()).ToArray();
                    // The filenames are padded to fill out to the last frame
                    int padding = lastFrame.ToString().Length;
                    var newName = Path.GetFileNameWithoutExtension(iniFile.FullName);
                    int doingFrame = 0;
                    int doneFrames = 0;
                    DateTime firstFrameAt = DateTime.Now;
                    DateTime lastFrameAt = new DateTime(0);
                    foreach (int fn in randomFrames)
                    {
                        string imgFile = newName + fn.ToString(new string('0', padding)) + ".png";
                        if (File.Exists(imgFile))
                        {
                            // Store times for ETA
                            doneFrames++;
                            DateTime thisOne = File.GetLastWriteTime(imgFile);
                            if (thisOne < firstFrameAt)
                                firstFrameAt = thisOne;
                            if (thisOne > lastFrameAt)
                                lastFrameAt = thisOne;
                        }
                        else
                        {
                            if (doingFrame == 0) // haven't already picked one
                            {
                                Console.WriteLine("Found undrawn frame at " + imgFile);
                                doingFrame = fn;
                            }
                        }
                    }
                    if (doingFrame == 0)
                    {
                        Console.WriteLine("All frames drawn.");
                        return true;
                    }
                    else
                    {
                        // Calculate EA
                        if (doneFrames > 1)
                        {
                            TimeSpan tookSoFar = lastFrameAt - firstFrameAt;
                            int totFrames = lastFrame - firstFrame;
                            // Because this only takes creation time, it doesn't account for the time the
                            // first frame took to draw, hence the doneFrames -1. Although who knows if that's
                            // the right way to do this
                            TimeSpan timeLeft = TimeSpan.FromTicks((long)(tookSoFar.Ticks * ((double)(doneFrames - 1) / totFrames)));
                            DateTime eta = DateTime.Now + timeLeft;
                            Console.WriteLine("Have done " + doneFrames.ToString() + "/" + totFrames.ToString() + " in " + ToReadableString(tookSoFar) + ". ETA is " + eta.ToString());
                        }
                        if (lockWorks(doingFrame))
                        {
                            // Go ahead and render
                            ProcessStartInfo cmdsi = new ProcessStartInfo(@"\Program Files\POV-Ray\v3.7\bin\pvengine.exe");
                            cmdsi.Arguments = '"' + iniFile.FullName + "\" /exit -sf" + doingFrame.ToString() + " -ef" + doingFrame.ToString();
                            Process cmd = Process.Start(cmdsi);
                            cmd.WaitForExit();
                            System.Threading.Thread.Sleep(5000); // for dropbox sync
                        }
                        else
                        {
                            Console.WriteLine("Failed to get a lock on frame");
                        }
                        File.Delete(doingFrame.ToString() + "_" + System.Environment.MachineName + ".lock");
                    }
                }
            }
            return false;
        }

        private static bool lockWorks(int doingFrame)
        {
            // Is it locked right now?
            var dir = new DirectoryInfo(".");
            var locks = dir.EnumerateFiles(doingFrame.ToString() + "_*.lock");
            if (locks.Count() != 0)
                return false;
            else
            {
                // There isn't a lock - try making one and see if it's the only one that got made
                File.Create(doingFrame.ToString() + "_" + System.Environment.MachineName + ".lock").Close();
                System.Threading.Thread.Sleep(5000);
                return locks.Count() == 1;
            }
        }
    }
}
