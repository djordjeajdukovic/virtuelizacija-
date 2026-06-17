using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.ServiceModel;
using Common;

namespace Client
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string inputDirectory = GetPath(ConfigurationManager.AppSettings["InputDirectory"], "Input");
            string logPath = GetPath(ConfigurationManager.AppSettings["ClientLogPath"], @"Logs\client_rejects.txt");
            int maxRowsToSend = ReadIntSetting("MaxRowsToSend", 100);
            int batchSize = ReadIntSetting("BatchSize", 10);

            Directory.CreateDirectory(inputDirectory);

            string filePath = ChooseFile(inputDirectory);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            string turbineId = GetTurbineIdFromFileName(filePath);

            Console.WriteLine("Izabran fajl: " + Path.GetFileName(filePath));
            Console.WriteLine("TurbineId: " + turbineId);
            Console.WriteLine("Max redova za slanje: " + maxRowsToSend);
            Console.WriteLine("Velicina bloka: " + batchSize);
            Console.WriteLine();

            int sentToServer = 0;
            int accepted = 0;
            int clientRejected = 0;
            int serverRejected = 0;
            int batchNumber = 0;

            using (ClientLogger logger = new ClientLogger(logPath))
            using (CsvSampleReader reader = new CsvSampleReader(filePath, turbineId))
            using (WcfServiceProxy proxy = new WcfServiceProxy("WindTurbineService"))
            {
                try
                {
                    Console.WriteLine("[START] Pokretanje prenosa.");

                    proxy.Service.StartSession(new SessionMeta
                    {
                        TurbineId = turbineId,
                        SourceFileName = Path.GetFileName(filePath),
                        StartedAt = DateTime.Now
                    });

                    Console.WriteLine("[START] Sesija je pokrenuta.");
                    Console.WriteLine("[DATA] Slanje blokova je u toku.");

                    List<WindTurbineSample> batch = new List<WindTurbineSample>();
                    int prepared = 0;

                    foreach (ReadSampleResult result in reader.ReadSamples())
                    {
                        if (prepared >= maxRowsToSend)
                        {
                            break;
                        }

                        if (!result.IsValid)
                        {
                            clientRejected++;
                            logger.Log(result.RowIndex, result.ErrorMessage, result.OriginalLine);
                            Console.WriteLine("[CLIENT REJECT] Red " + result.RowIndex + " | " + result.ErrorMessage);
                            continue;
                        }

                        batch.Add(result.Sample);
                        prepared++;

                        if (batch.Count == batchSize)
                        {
                            SendBatch(proxy, batch, ref batchNumber, ref sentToServer, ref accepted, ref serverRejected);
                            batch.Clear();
                        }
                    }

                    if (batch.Count > 0)
                    {
                        SendBatch(proxy, batch, ref batchNumber, ref sentToServer, ref accepted, ref serverRejected);
                    }

                    proxy.Service.EndSession();
                    Console.WriteLine("[END] Prenos je zavrsen.");
                }
                catch (FaultException<DataFormatFault> ex)
                {
                    Console.WriteLine("[FAULT] " + ex.Detail.Message);
                }
                catch (FaultException<ValidationFault> ex)
                {
                    Console.WriteLine("[FAULT] " + ex.Detail.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[ERROR] " + ex.Message);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Poslato serveru: " + sentToServer);
            Console.WriteLine("Prihvaceno na serveru: " + accepted);
            Console.WriteLine("Odbijeno na klijentu: " + clientRejected);
            Console.WriteLine("Odbijeno na serveru: " + serverRejected);
            Console.WriteLine("Client log: " + logPath);
            Console.WriteLine("Pritisni ENTER za kraj.");
            Console.ReadLine();
        }

        private static void SendBatch(WcfServiceProxy proxy, List<WindTurbineSample> batch, ref int batchNumber, ref int sentToServer, ref int accepted, ref int rejected)
        {
            BatchResult result = proxy.Service.PushBatch(batch.ToArray());
            batchNumber = result.BatchNumber;
            sentToServer += result.ReceivedCount;
            accepted += result.AcceptedCount;
            rejected += result.RejectedCount;

            Console.WriteLine("[BATCH " + result.BatchNumber + "] " + result.Message +
                " | primljeno: " + result.ReceivedCount +
                " | prihvaceno: " + result.AcceptedCount +
                " | odbijeno: " + result.RejectedCount);
        }

        private static string ChooseFile(string inputDirectory)
        {
            string[] files = Directory.GetFiles(inputDirectory, "*.csv");

            if (files.Length == 0)
            {
                Console.WriteLine("U Input folderu nema CSV fajlova.");
                Console.Write("Unesi punu putanju do CSV fajla: ");
                string path = Console.ReadLine();
                return File.Exists(path) ? path : null;
            }

            Array.Sort(files);
            Console.WriteLine("Izaberi CSV fajl:");

            for (int i = 0; i < files.Length; i++)
            {
                Console.WriteLine((i + 1) + ". " + Path.GetFileName(files[i]));
            }

            Console.Write("Broj fajla: ");
            int choice;

            if (!int.TryParse(Console.ReadLine(), out choice) || choice < 1 || choice > files.Length)
            {
                return null;
            }

            return files[choice - 1];
        }

        private static string GetPath(string configuredPath, string defaultPath)
        {
            string path = string.IsNullOrWhiteSpace(configuredPath) ? defaultPath : configuredPath;

            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path));
        }

        private static int ReadIntSetting(string key, int defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            int parsed;

            if (int.TryParse(value, out parsed) && parsed > 0)
            {
                return parsed;
            }

            return defaultValue;
        }

        private static string GetTurbineIdFromFileName(string filePath)
        {
            string name = Path.GetFileNameWithoutExtension(filePath);
            string marker = "Kelmarsh_";
            int index = name.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

            if (index < 0)
            {
                return name;
            }

            int start = index + marker.Length;
            int end = start;

            while (end < name.Length && char.IsDigit(name[end]))
            {
                end++;
            }

            if (end == start)
            {
                return name;
            }

            return marker + name.Substring(start, end - start);
        }
    }
}
