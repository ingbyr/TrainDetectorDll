using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace TrainDetectorDll
{
    public class TrainDetector
    {
        // darknet.exe command args
        public string Trainer;
        public string NetCfg;
        public string NetWeights;
        private string extraArgs = "-dont_show";

        // dir path
        public string TrainDataPath;
        public string DataFilePath;

        // train config
        public int Iteration;

        // .data file
        public struct DataFile
        {
            public int Classes;
            public string Train;
            public string Valid;
            public string Names;
            public string Backup;
        }

        // result data by every step
        public struct StepResult
        {
            public string Iterations;
            public string AvgLoss;
            public string Rate;
            public string Time;
            public string images;

            public override string ToString()
            {
                return "iterations: " + Iterations + ", avg los: " + AvgLoss + ", rate: " + Rate + ", time: " + Time + ", images: " + images;
            }
        }

        // msg queue
        public FixedSizedQueue<StepResult> MsgQueue;

        public bool IsTraining = false;

        // process
        private Process proc;

        public void startTrain()
        {
            IsTraining = true;
            Console.WriteLine("exec: ");
            Console.WriteLine(Trainer + " " + buildArgs());
            proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Trainer,
                    Arguments = buildArgs(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            proc.Start();

            // create new thread to parse output
            var thread = new Thread(parseTrainOutput);
            thread.Start();
        }

        private void parseTrainOutput()
        {
            int currentIteration = 0;
            while (currentIteration < Iteration && !proc.StandardOutput.EndOfStream)
            {
                //Console.WriteLine("current iteration: " + currentIteration);
                var line = proc.StandardOutput.ReadLine();
                if (line.EndsWith("images"))
                {
                    var results = line.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                    //Console.WriteLine("iterations: " + results[0].Split(':')[0]);
                    //Console.WriteLine("avg loss: " + results[1].Split(' ')[0]);
                    //Console.WriteLine("rate: " + results[2].Split(' ')[0]);
                    //Console.WriteLine("Time: " + results[3].Split(' ')[0]); // seconds?
                    //Console.WriteLine("images: " + results[4].Split(' ')[0]);
                    //Console.WriteLine();
                    StepResult stepResult;
                    stepResult.Iterations = results[0].Split(':')[0].TrimStart();
                    stepResult.AvgLoss = results[1].Split(' ')[0];
                    stepResult.Rate = results[2].Split(' ')[0];
                    stepResult.Time = results[3].Split(' ')[0]; // seconds?
                    stepResult.images = results[4].Split(' ')[0];
                    MsgQueue.Enqueue(stepResult);
                    currentIteration++;
                }
            }

            // stop the process
            stopProcess();
        }

    
        private string buildArgs()
        {
            return "detector train" + " " + DataFilePath + " " + NetCfg + " " + NetWeights + " " + extraArgs;
        }

        // must be invoked first
        public void prepareData(DataFile dataFile)
        {
            // get pic path list
            string[] files = Directory.GetFileSystemEntries(TrainDataPath);
            List<string> pictures = new List<String>();
            foreach (var file in files)
            {
                string extention = Path.GetExtension(file).ToLower();
                if (extention == ".jpg")
                {
                    pictures.Add(file);
                }
            }

            // save to DataFile.train text file
            using (StreamWriter writer = new StreamWriter(dataFile.Train))
            {
                foreach (var pic in pictures)
                {
                    writer.WriteLine(pic);
                }
            }

            // save DataFile
            using (StreamWriter writer = new StreamWriter(DataFilePath))
            {
                writer.WriteLine("classes = " + dataFile.Classes);
                writer.WriteLine("train = " + dataFile.Train);
                writer.WriteLine("valid = " + dataFile.Valid);
                writer.WriteLine("names = " + dataFile.Names);
                writer.WriteLine("backup = " + dataFile.Backup);
            }
        }

        public void stopProcess()
        {
            IsTraining = false;
            if (proc != null && !proc.HasExited)
            {
                proc.Kill();
            }
        }
    }

    public class FixedSizedQueue<T> : ConcurrentQueue<T>
    {
        private readonly object syncObject = new object();
        public int Size { get; private set; }

        public FixedSizedQueue(int size)
        {
            Size = size;
        }

        public new void Enqueue(T obj)
        {
            base.Enqueue(obj);
            lock (syncObject)
            {
                while (base.Count > Size)
                {
                    T outObj;
                    base.TryDequeue(out outObj);
                }
            }
        }
    }
}
