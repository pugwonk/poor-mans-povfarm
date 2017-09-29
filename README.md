# poor-mans-povfarm
A rather basic POV-Ray animation-only render farm communicating via the file system.

# Introduction
This is a very simple POV-Ray render farm that only works where all computers have access to a shared filesystem (I'm using DropBox). It only works with animated POVs (you cannot use it to split a single image render into component parts). To use it:

* Create a DropBox folder (or some other shared file area) for your farm
* Put farm.exe in it
* Run farm.exe on all the computers you want to use in the farm
* Copy an INI file (containing animation) with its associated POV into the folder
* The computers in your farm will each pick individual frames to render and render them

Once the rendering is finished, the computers will repeatedly display "All frames drawn". You can if you wish put more than one set of INI/POV files in there - each computer will select a random INI to animate from whenever the machine is ready to render a new frame.

# Tips
* You can, whenever you want, delete any .log files in the folder. The main farm.log one is the shared status updates you'll see in each computer's windows
* I'd monitor the start-up of POV-Ray on each machine when it first starts running in your farm, because sometimes it shows a kind of "About" page that you have to click ok on. It doesn't seem to come up after it's been shown once
* Once POV-Ray is running on a machine, close all the editor windows apart from "Messages". As otherwise it might pop up with a "we've saved your file because you switched to another application" message which causes your render to stop