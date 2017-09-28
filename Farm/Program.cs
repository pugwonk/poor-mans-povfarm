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
        static string lastLogFile = "";
        const string logFile = "farm.log";
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

            if (string.IsNullOrEmpty(formatted)) formatted = "0 minutes";

            return formatted;
        }

        static void log(string msg)
        {
            using (StreamWriter sw = File.AppendText(System.Environment.MachineName + ".log"))
            {
                sw.WriteLine(DateTime.Now.ToString() + " " + msg);
            }
        }

        static void sharedLog(string msg)
        {
            using (StreamWriter sw = File.AppendText(logFile))
            {
                sw.WriteLine(DateTime.Now.ToShortTimeString() + " " + msg);
            }
        }

        static void Main(string[] args)
        {
            // Check every so often for new files
            Console.WriteLine("Press a key to exit (after current frame is rendered).");
            sharedLog(System.Environment.MachineName + " entering farm");
            Timer cLog = new System.Timers.Timer(30000);
            cLog.Elapsed += CLog_Elapsed;
            cLog.Enabled = true;
            deleteOldLocks();
            do
            {
                checkFiles();
                System.Threading.Thread.Sleep(1000);
            } while (!Console.KeyAvailable);
            sharedLog(System.Environment.MachineName + " leaving farm");
            cLog.Enabled = false;
        }

        private static void CLog_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (File.Exists(logFile))
            {
                string nextLogFile = File.ReadAllText(logFile);
                if (lastLogFile != "")
                {
                    if (nextLogFile != lastLogFile)
                    {
                        // Only spew out the new bits if this one is bigger than the last one
                        // in case it got deleted
                        if (nextLogFile.Length < lastLogFile.Length)
                        {
                            log("Ignoring smaller-than-previous logfile");
                            lastLogFile = "";
                        }
                        string newBits = nextLogFile.Substring(lastLogFile.Length);
                        Console.ForegroundColor = ConsoleColor.Green;
                        foreach (string line in newBits.Split("\r\n".ToCharArray()))
                        {
                            if (line.Trim() != "")
                                Console.WriteLine(line);
                        }
                        Console.ResetColor();
                    }
                }
                lastLogFile = nextLogFile;
            }
        }

        private static void deleteOldLocks()
        {
            var dir = new DirectoryInfo(".");
            var locks = dir.EnumerateFiles("*_" + System.Environment.MachineName + "_*.lock");
            if (locks.Count() > 0)
            {
                log("Deleting some old lock files");
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
                log("Found " + iniFile.Name);
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
                    //log("First frame is " + firstFrame.ToString());
                    //log("Last frame is " + lastFrame.ToString());
                    if (lastFrame == 0)
                    {
                        log("No animation in INI.");
                        return true;
                    }
                    //log("Picking a random frame...");
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
                                doingFrame = fn;
                                log("Found undrawn frame " + doingFrame.ToString());
                            }
                        }
                    }
                    if (doingFrame == 0)
                    {
                        log("All frames drawn.");
                        return true;
                    }
                    else
                    {
                        // Calculate EA
                        if (doneFrames > 1)
                        {
                            TimeSpan tookSoFar = lastFrameAt - firstFrameAt;
                            int totFrames = lastFrame - firstFrame + 1;
                            // Because this only takes creation time, it doesn't account for the time the
                            // first frame took to draw, hence the doneFrames +1. Although who knows if that's
                            // the right way to do this
                            TimeSpan timePerFrame = TimeSpan.FromTicks((long)(tookSoFar.Ticks / ((double)(doneFrames + 1))));
                            TimeSpan timeLeft = TimeSpan.FromTicks(timePerFrame.Ticks * (totFrames - doneFrames + 1));
                            DateTime eta = DateTime.Now + timeLeft;
                            sharedLog(Path.GetFileNameWithoutExtension(iniFile.FullName) + ": Have done " + doneFrames.ToString() + "/" + totFrames.ToString() + " in " + ToReadableString(tookSoFar));
                            sharedLog(Path.GetFileNameWithoutExtension(iniFile.FullName) + ": Time per frame " + ToReadableString(timePerFrame) + ". ETA is " + eta.ToString());
                        }
                        string lockFile = doingFrame.ToString() + "_" + System.Environment.MachineName + "_" + Path.GetFileNameWithoutExtension(iniFile.FullName) + ".lock";
                        string lockFileWild = doingFrame.ToString() + "_*_" + Path.GetFileNameWithoutExtension(iniFile.FullName) + ".lock";
                        if (lockWorks(doingFrame, lockFile, lockFileWild))
                        {
                            // Go ahead and render
                            DateTime startTime = DateTime.Now;
                            sharedLog(Path.GetFileNameWithoutExtension(iniFile.FullName) + ": " + System.Environment.MachineName + " starting frame " + doingFrame.ToString());
                            ProcessStartInfo cmdsi = new ProcessStartInfo(@"c:\Program Files\POV-Ray\v3.7\bin\pvengine.exe");
                            cmdsi.Arguments = '"' + iniFile.FullName + "\" /exit -sf" + doingFrame.ToString() + " -ef" + doingFrame.ToString();
                            log("Executing pvengine.exe " + cmdsi.Arguments);
                            Process cmd = Process.Start(cmdsi);
                            cmd.WaitForExit();
                            TimeSpan took = DateTime.Now - startTime;
                            sharedLog(Path.GetFileNameWithoutExtension(iniFile.FullName) + ": " + System.Environment.MachineName + " finished frame " + doingFrame.ToString() + " in " + ToReadableString(took));
                            System.Threading.Thread.Sleep(5000); // for dropbox sync
                        }
                        else
                        {
                            log("Failed to get a lock on frame");
                        }
                        File.Delete(lockFile);
                    }
                }
            }
            return false;
        }

        private static bool lockWorks(int doingFrame, string lockFile, string lockFileWild)
        {
            // Is it locked right now?
            var dir = new DirectoryInfo(".");
            var locks = dir.EnumerateFiles(lockFileWild);
            if (locks.Count() != 0)
            {
                log("Frame " + doingFrame.ToString() + " already in progress (try #1)");
                return false;
            }
            else
            {
                // There isn't a lock - try making one and see if it's the only one that got made
                File.Create(lockFile).Close();
                System.Threading.Thread.Sleep(5000);
                locks = dir.EnumerateFiles(lockFileWild);
                if (locks.Count() != 1)
                    log("Frame " + doingFrame.ToString() + " already in progress (try #2)");
                return locks.Count() == 1;
            }
        }
    }
}
