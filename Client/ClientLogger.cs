using System;
using System.Globalization;
using System.IO;

namespace Client
{
    public class ClientLogger : IDisposable
    {
        private readonly FileStream stream;
        private readonly StreamWriter writer;
        private bool disposed;

        public ClientLogger(string path)
        {
            string folder = Path.GetDirectoryName(path);

            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            writer = new StreamWriter(stream);
        }

        public void Log(int rowIndex, string message)
        {
            writer.WriteLine(DateTime.Now.ToString("o", CultureInfo.InvariantCulture) + " | Row " + rowIndex + " | " + message);
            writer.Flush();
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
                if (writer != null) writer.Dispose();
                if (stream != null) stream.Dispose();
            }

            disposed = true;
        }
    }
}
