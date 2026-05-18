using System;
using System.ServiceModel;

namespace Service
{
    internal class Program
    {
        static void Main(string[] args)
        {
            using (ServiceHost host = new ServiceHost(typeof(WindTurbineService)))
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
