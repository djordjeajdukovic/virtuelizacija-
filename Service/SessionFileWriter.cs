using System;
using System.Globalization;
using System.IO;
using Common;

namespace Service
{
    public class SessionFileWriter : IDisposable
    {
        private readonly string dataRoot;
        private readonly string turbineId;
        private bool disposed;
        private FileStream sampleStream;
        private FileStream rejectStream;
        private StreamWriter sampleWriter;
        private StreamWriter rejectWriter;

        public SessionFileWriter(string dataRoot, string turbineId)
        {
            this.dataRoot = dataRoot;
            this.turbineId = turbineId;
        }

        ~SessionFileWriter()
        {
            Dispose(false);
        }

        public void Open()
        {
            string folder = Path.Combine(dataRoot, SafeName(turbineId), DateTime.Now.ToString("yyyy-MM-dd"));
            Directory.CreateDirectory(folder);

            string samplePath = Path.Combine(folder, "session.csv");
            string rejectPath = Path.Combine(folder, "rejects.csv");

            bool newSampleFile = !File.Exists(samplePath) || new FileInfo(samplePath).Length == 0;
            bool newRejectFile = !File.Exists(rejectPath) || new FileInfo(rejectPath).Length == 0;

            sampleStream = new FileStream(samplePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            rejectStream = new FileStream(rejectPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            sampleWriter = new StreamWriter(sampleStream);
            rejectWriter = new StreamWriter(rejectStream);

            if (newSampleFile)
            {
                sampleWriter.WriteLine("Timestamp,WindSpeed,WindDirection,NacellePosition,PowerKW,PotentialPowerDefaultKW,PowerFactor,ReactivePowerKvar,GridFrequencyHz,GeneratorRpm,RowIndex,TurbineId");
                sampleWriter.Flush();
            }

            if (newRejectFile)
            {
                rejectWriter.WriteLine("Time,RowIndex,Reason,OriginalLine");
                rejectWriter.Flush();
            }
        }

        public void WriteSample(WindTurbineSample sample)
        {
            sampleWriter.WriteLine(string.Join(",",
                sample.Timestamp.ToString("o", CultureInfo.InvariantCulture),
                ToText(sample.WindSpeed),
                ToText(sample.WindDirection),
                ToText(sample.NacellePosition),
                ToText(sample.PowerKW),
                ToText(sample.PotentialPowerDefaultKW),
                ToText(sample.PowerFactor),
                ToText(sample.ReactivePowerKvar),
                ToText(sample.GridFrequencyHz),
                ToText(sample.GeneratorRpm),
                sample.RowIndex.ToString(CultureInfo.InvariantCulture),
                Escape(sample.TurbineId)));
            sampleWriter.Flush();
        }

        public void WriteReject(int rowIndex, string reason, string originalLine)
        {
            rejectWriter.WriteLine(string.Join(",",
                DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                rowIndex.ToString(CultureInfo.InvariantCulture),
                Escape(reason),
                Escape(originalLine)));
            rejectWriter.Flush();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                if (sampleWriter != null) sampleWriter.Dispose();
                if (rejectWriter != null) rejectWriter.Dispose();
                if (sampleStream != null) sampleStream.Dispose();
                if (rejectStream != null) rejectStream.Dispose();
            }

            disposed = true;
        }

        private static string ToText(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Escape(string text)
        {
            if (text == null)
            {
                return string.Empty;
            }

            if (text.Contains(",") || text.Contains("\"") || text.Contains("\n") || text.Contains("\r"))
            {
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            }

            return text;
        }

        private static string SafeName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name;
        }
    }
}
