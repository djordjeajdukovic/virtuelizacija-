using System;
using System.Configuration;
using System.IO;
using System.ServiceModel;
using Common;

namespace Service
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class WindTurbineService : IWindTurbineService
    {
        private SessionMeta currentSession;
        private SessionFileWriter writer;

        public void StartSession(SessionMeta meta)
        {
            if (meta == null)
            {
                throw new FaultException<DataFormatFault>(new DataFormatFault("Meta podaci nisu poslati."), "DataFormatFault");
            }

            if (string.IsNullOrWhiteSpace(meta.TurbineId))
            {
                throw new FaultException<ValidationFault>(new ValidationFault("TurbineId je obavezan."), "ValidationFault");
            }

            CloseWriter();

            currentSession = meta;
            writer = new SessionFileWriter(GetDataRoot(), meta.TurbineId);
            writer.Open();

            Console.WriteLine("[START] Sesija pocela.");
            Console.WriteLine("[START] TurbineId: " + meta.TurbineId);
            Console.WriteLine("[START] Fajl: " + meta.SourceFileName);
        }

        public void PushSample(WindTurbineSample sample)
        {
            if (writer == null || currentSession == null)
            {
                throw new FaultException<ValidationFault>(new ValidationFault("Sesija nije pokrenuta."), "ValidationFault");
            }

            string formatError = SampleValidator.GetFormatError(sample);

            if (formatError != null)
            {
                writer.WriteReject(sample == null ? 0 : sample.RowIndex, formatError);
                throw new FaultException<DataFormatFault>(new DataFormatFault(formatError), "DataFormatFault");
            }

            string validationError = SampleValidator.GetValidationError(sample);

            if (validationError != null)
            {
                writer.WriteReject(sample.RowIndex, validationError);
                throw new FaultException<ValidationFault>(new ValidationFault(validationError), "ValidationFault");
            }

            writer.WriteSample(sample);

            Console.WriteLine("[DATA] Primljen red " + sample.RowIndex + " | Power " + sample.PowerKW.ToString(System.Globalization.CultureInfo.InvariantCulture) + " kW");

        }

        public void EndSession()
        {
            CloseWriter();
            currentSession = null;
            Console.WriteLine("[END] Sesija zavrsena.");
        }

        private void CloseWriter()
        {
            if (writer != null)
            {
                writer.Dispose();
                writer = null;
            }
        }

        private static string GetDataRoot()
        {
            string path = ConfigurationManager.AppSettings["DataPath"];

            if (string.IsNullOrWhiteSpace(path))
            {
                path = "Data";
            }

            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        }
    }
}