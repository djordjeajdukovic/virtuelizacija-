using System;
using System.Globalization;
using System.ServiceModel;

namespace Service
{
    internal class Program
    {
        static void Main(string[] args)
        {
            WindTurbineService service = new WindTurbineService();

            service.OnTransferStarted += delegate(object sender, TransferEventArgs e)
            {
                Console.WriteLine("[EVENT] Prenos je poceo.");
                Console.WriteLine("[EVENT] TurbineId: " + e.TurbineId);
                Console.WriteLine("[EVENT] Fajl: " + e.SourceFileName);
            };

            service.OnSampleReceived += delegate(object sender, SampleReceivedEventArgs e)
            {
                Console.WriteLine("[SAMPLE] Red " + e.Sample.RowIndex +
                    " | Wind " + e.Sample.WindSpeed.ToString("0.00", CultureInfo.InvariantCulture) +
                    " m/s | Power " + e.Sample.PowerKW.ToString("0.00", CultureInfo.InvariantCulture) + " kW");
            };

            service.OnBatchReceived += delegate(object sender, BatchReceivedEventArgs e)
            {
                Console.WriteLine("[BATCH " + e.BatchNumber + "] Blok primljen" +
                    " | primljeno: " + e.ReceivedCount +
                    " | prihvaceno: " + e.AcceptedCount +
                    " | odbijeno: " + e.RejectedCount);
            };

            service.OnWarningRaised += delegate(object sender, WarningEventArgs e)
            {
                Console.WriteLine("[WARNING] " + e.WarningType + " | Red " + e.RowIndex + " | " + e.Message);
            };

            service.OnTransferCompleted += delegate(object sender, TransferEventArgs e)
            {
                Console.WriteLine("[EVENT] Prenos zavrsen" +
                    " | prihvaceno: " + e.AcceptedCount +
                    " | odbijeno: " + e.RejectedCount);
            };

            using (ServiceHost host = new ServiceHost(service))
            {
                try
                {
                    host.Open();
                    Console.WriteLine("Vetrogenerator WCF servis je pokrenut.");
                    Console.WriteLine("Adresa: net.tcp://localhost:4002/WindTurbineService");
                    Console.WriteLine("Pritisni ENTER za zaustavljanje servisa.");
                    Console.ReadLine();
                    host.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    host.Abort();
                    Console.ReadLine();
                }
            }
        }
    }
}
