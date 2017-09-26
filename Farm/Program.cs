using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Farm
{
    class Program
    {
        static Timer ping;
        static void Main(string[] args)
        {
            // Check every so often for new files
            ping = new Timer(1000);
            ping.Elapsed += Ping_Elapsed;
            ping.Enabled = true;
            Console.WriteLine("Press a key to exit.");
            Console.ReadKey();
        }

        private static void Ping_Elapsed(object sender, ElapsedEventArgs e)
        {
            int firstFrame = 0;
            int lastFrame = 0;

            ping.Enabled = false;
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
                        Console.WriteLine("All frames drawn.");
                }
            }
            ping.Enabled = true;
        }
    }
}
