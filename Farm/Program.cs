using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Farm
{
    class Program
    {
        static volatile bool renderNow = true; // render a frame on the next check?
        static Random rnd = new Random();
        static Process render = new Process();

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

        static void log(string msg)
        {
            Console.WriteLine(DateTime.Now.ToShortTimeString() + " " + msg);
        }

        static void Main(string[] args)
        {
            // Check every so often for new files
            log("Press a key to exit (after current frame is rendered).");
            deleteOldLocks();

            render.StartInfo.FileName = @"c:\Program Files\POV-Ray\v3.7\bin\pvengine.exe";
            render.EnableRaisingEvents = true;
            render.Exited += Render_Exited;

            do
            {
                checkFiles();
                System.Threading.Thread.Sleep(2000);
            } while (!Console.KeyAvailable);
            log("Exiting after current frame rendered...");
            // Wait till we're ready to render another frame (at which point we'll quit instead)
            do
            {
                System.Threading.Thread.Sleep(500);
            } while (!renderNow);
        }

        private static void Render_Exited(object sender, EventArgs e)
        {
            log("Render finished");
            renderNow = true;
        }

        private static void checkFiles()
        {
            var dir = new DirectoryInfo(".");
            var inis = dir.EnumerateFiles("*.ini");
            foreach (var iniFile in inis)
            {
                checkIni(iniFile);
            }
        }

        private static void checkIni(FileInfo iniFile)
        {
            int firstFrame = 0;
            int lastFrame = 0;

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
                }
                else
                {
                    checkFrames(iniFile, firstFrame, lastFrame);
                }
            }
        }

        private static void checkFrames(FileInfo iniFile, int firstFrame, int lastFrame)
        {
            int[] randomFrames = randomiseFrames(firstFrame, lastFrame);
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
                    log("Have done " + doneFrames.ToString() + "/" + totFrames.ToString() + " in " + ToReadableString(tookSoFar));
                    log("Time per frame: " + ToReadableString(timePerFrame) + ". ETA is " + eta.ToString());
                }
                // Is there a render thread going on?
                if (!renderNow)
                    log("Busy rendering so not trying again");
                else
                {
                    log("Starting render");
                    if (lockWorks(doingFrame))
                    {
                        // Go ahead and render
                        renderNow = false;
                        render.StartInfo.Arguments = '"' + iniFile.FullName + "\" /exit -sf" + doingFrame.ToString() + " -ef" + doingFrame.ToString();
                        render.Start();
                        //cmd.WaitForExit();
                        System.Threading.Thread.Sleep(5000); // for dropbox sync
                    }
                    else
                    {
                        log("Failed to get a lock on frame");
                    }
                    File.Delete(doingFrame.ToString() + "_" + System.Environment.MachineName + ".lock");
                }
            }
        }

        private static int[] randomiseFrames(int firstFrame, int lastFrame)
        {
            //log("Picking a random frame...");
            int[] frames = new int[lastFrame - firstFrame + 1];
            for (int i = firstFrame; i <= lastFrame; i++)
            {
                frames[i - firstFrame] = i;
            }
            return frames.OrderBy(x => rnd.Next()).ToArray();
        }

        private static void deleteOldLocks()
        {
            var dir = new DirectoryInfo(".");
            var locks = dir.EnumerateFiles("*_" + System.Environment.MachineName + ".lock");
            if (locks.Count() > 0)
            {
                log("Deleting some old lock files");
                foreach (var lockf in locks) {
                    lockf.Delete();
                }
            }
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
