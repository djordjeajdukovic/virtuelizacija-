using System;
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
            Console.WriteLine();

            int sent = 0;
            int rejected = 0;

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
                    Console.WriteLine("[DATA] Slanje uzoraka je u toku.");

                    foreach (ReadSampleResult result in reader.ReadSamples())
                    {
                        if (sent >= maxRowsToSend)
                        {
                            break;
                        }

                        if (!result.IsValid)
                        {
                            rejected++;
                            logger.Log(result.RowIndex, result.ErrorMessage);
                            Console.WriteLine("[REJECT] Red " + result.RowIndex + " | " + result.ErrorMessage);
                            continue;
                        }

                        try
                        {
                            proxy.Service.PushSample(result.Sample);
                            sent++;

                            Console.WriteLine("[" + sent + "/" + maxRowsToSend + "] PushSample: ACK | Red " + result.Sample.RowIndex);

                        }
                        catch (FaultException<DataFormatFault> ex)
                        {
                            rejected++;
                            logger.Log(result.RowIndex, ex.Detail.Message);
                            Console.WriteLine("[" + sent + "/" + maxRowsToSend + "] PushSample: NACK | " + ex.Detail.Message);
                        }
                        catch (FaultException<ValidationFault> ex)
                        {
                            rejected++;
                            logger.Log(result.RowIndex, ex.Detail.Message);
                            Console.WriteLine("[" + sent + "/" + maxRowsToSend + "] PushSample: NACK | " + ex.Detail.Message);
                        }
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
            Console.WriteLine("Poslato redova: " + sent);
            Console.WriteLine("Odbijeno redova: " + rejected);
            Console.WriteLine("Client log: " + logPath);
            Console.WriteLine("Pritisni ENTER za kraj.");
            Console.ReadLine();
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

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
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

            if (name.Contains("Kelmarsh_1")) return "Kelmarsh_1";
            if (name.Contains("Kelmarsh_2")) return "Kelmarsh_2";
            if (name.Contains("Kelmarsh_3")) return "Kelmarsh_3";
            if (name.Contains("Kelmarsh_4")) return "Kelmarsh_4";
            if (name.Contains("Kelmarsh_5")) return "Kelmarsh_5";
            if (name.Contains("Kelmarsh_6")) return "Kelmarsh_6";

            return name;
        }
    }
}