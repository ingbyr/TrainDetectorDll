using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace TrainDetectorDll
{
    public class TrainDetector
    {
        // debug
        public bool debug = false;
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
        public ConcurrentQueue<StepResult> MsgQueue = new ConcurrentQueue<StepResult>();

        public bool IsTraining = false;

        // process
        private Process proc;

        // currentIteration
        private int currentIteration = 0;

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
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            proc.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
            proc.ErrorDataReceived += new DataReceivedEventHandler(OutputHandler);
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();

            //TrainDetector.StepResult result;
            //while (MsgQueue.TryDequeue(out result))
            //{
            //    Console.WriteLine("[info] " + result.ToString());
            //}
        }

        void OutputHandler(object sendingProcess, DataReceivedEventArgs line)
        {
            if (currentIteration < Iteration)
            {
                if (debug)
                {
                    Console.WriteLine(line.Data);
                }

                if (line.Data.TrimEnd().EndsWith("images"))
                {
                    var results = line.Data.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
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
            else
            {
                // stop the process
                stopProcess();
            }
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

            // save pics list to DataFile.Train text file
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

            // create DataFile.Backup
            if (!Directory.Exists(dataFile.Backup))
            {
                Directory.CreateDirectory(dataFile.Backup);
            }

            // check related files
            checkFiles(Trainer, NetCfg, NetWeights, TrainDataPath, DataFilePath, dataFile.Train, dataFile.Valid, dataFile.Names, dataFile.Backup);
        }

        private void checkFiles(params string[] files)
        {
            foreach (var file in files)
            {
                if (File.Exists(file) || Directory.Exists(file)) { }
                else throw new FileNotFoundException("not found " + file);
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

}
