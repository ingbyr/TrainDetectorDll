using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace TrainDetectorDll
{
    public class TrainDetector
    {
        // darknet.exe command args
        public string trainer;
        public string netCfg;
        public string netWeights;
        private string extraArgs = "-dont_show";

        // .data file
        public struct DataFile
        {
            public int classes;
            public string train;
            public string valid;
            public string names;
            public string backup;
        }

        // dir path
        public string trainDataPath;
        public string dataFilePath;

        // process
        Process proc;

        public void startTrain()
        {

            Console.WriteLine(trainer +" "+ buildArgs());
            proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = trainer,
                    Arguments = buildArgs(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            proc.Start();

            // need a thread to parse output
            while(!proc.StandardOutput.EndOfStream)
            {
                string line = proc.StandardOutput.ReadLine();
                Console.WriteLine(line);
            }
        }

        private string buildArgs()
        {
            return "detector train" + " " + dataFilePath + " " + netCfg + " " + netWeights + " " + extraArgs;
        }

        public void prepareData(DataFile dataFile)
        {
            // get pic path list
            string[] files = Directory.GetFileSystemEntries(trainDataPath);
            List<string> pictures = new List<String>();
            foreach (var file in files)
            {
                string extention = Path.GetExtension(file).ToLower();
                if(extention == ".jpg")
                {
                    pictures.Add(file);
                }
            }

            // save to DataFile.train text file
            using (StreamWriter writer = new StreamWriter(dataFile.train))
            {
                foreach(var pic in pictures)
                {
                    writer.WriteLine(pic);
                }
            }

            // save DataFile
            using(StreamWriter writer = new StreamWriter(dataFilePath))
            {
                writer.WriteLine("classes = " + dataFile.classes);
                writer.WriteLine("train = " + dataFile.train);
                writer.WriteLine("valid = " + dataFile.valid);
                writer.WriteLine("names = " + dataFile.names);
                writer.WriteLine("backup = " + dataFile.backup);
            }
        }

        public void stopProcess()
        {
            if(!proc.HasExited)
            {
                proc.Kill();
            }
        }
    }
}
