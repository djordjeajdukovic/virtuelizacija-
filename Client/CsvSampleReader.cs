using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Common;

namespace Client
{
    public class CsvSampleReader : IDisposable
    {
        private readonly string path;
        private readonly string turbineId;
        private readonly StreamReader reader;
        private Dictionary<string, int> columns;
        private bool disposed;

        public CsvSampleReader(string path, string turbineId)
        {
            this.path = path;
            this.turbineId = turbineId;
            reader = new StreamReader(path);
        }

        public IEnumerable<ReadSampleResult> ReadSamples()
        {
            ReadHeader();
            int rowIndex = 10;
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                rowIndex++;

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                ReadSampleResult result = new ReadSampleResult();
                result.RowIndex = rowIndex;

                try
                {
                    result.Sample = ParseSample(line, rowIndex);
                    result.IsValid = true;
                }
                catch (CsvRowException ex)
                {
                    result.IsValid = false;
                    result.ErrorMessage = ex.Message;
                }

                yield return result;
            }
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
                if (reader != null) reader.Dispose();
            }

            disposed = true;
        }

        private void ReadHeader()
        {
            for (int i = 1; i <= 9; i++)
            {
                if (reader.ReadLine() == null)
                {
                    throw new CsvRowException("CSV fajl nema prvih 9 redova.");
                }
            }

            string headerLine = reader.ReadLine();

            if (headerLine == null)
            {
                throw new CsvRowException("CSV fajl nema header u 10. redu.");
            }

            string[] header = SplitCsvLine(headerLine).ToArray();
            columns = new Dictionary<string, int>();

            for (int i = 0; i < header.Length; i++)
            {
                string key = Normalize(header[i]);

                if (!columns.ContainsKey(key))
                {
                    columns.Add(key, i);
                }
            }

            CheckColumn("Date and time");
            CheckColumn("Wind speed (m/s)");
            CheckColumn("Wind direction (°)");
            CheckColumn("Nacelle position (°)");
            CheckColumn("Power (kW)");
            CheckColumn("Potential power default PC (kW)");
            CheckColumn("Power factor (cosphi)");
            CheckColumn("Reactive power (kvar)");
            CheckColumn("Grid frequency (Hz)");
            CheckColumn("Generator RPM (RPM)");
        }

        private WindTurbineSample ParseSample(string line, int rowIndex)
        {
            string[] values = SplitCsvLine(line).ToArray();

            return new WindTurbineSample
            {
                Timestamp = ParseDate(GetValue(values, "Date and time"), rowIndex),
                WindSpeed = ParseDouble(GetValue(values, "Wind speed (m/s)"), "Wind speed", rowIndex),
                WindDirection = ParseDouble(GetValue(values, "Wind direction (°)"), "Wind direction", rowIndex),
                NacellePosition = ParseDouble(GetValue(values, "Nacelle position (°)"), "Nacelle position", rowIndex),
                PowerKW = ParseDouble(GetValue(values, "Power (kW)"), "Power", rowIndex),
                PotentialPowerDefaultKW = ParseDouble(GetValue(values, "Potential power default PC (kW)"), "Potential power default PC", rowIndex),
                PowerFactor = ParseDouble(GetValue(values, "Power factor (cosphi)"), "Power factor", rowIndex),
                ReactivePowerKvar = ParseDouble(GetValue(values, "Reactive power (kvar)"), "Reactive power", rowIndex),
                GridFrequencyHz = ParseDouble(GetValue(values, "Grid frequency (Hz)"), "Grid frequency", rowIndex),
                GeneratorRpm = ParseDouble(GetValue(values, "Generator RPM (RPM)"), "Generator RPM", rowIndex),
                RowIndex = rowIndex,
                TurbineId = turbineId
            };
        }

        private DateTime ParseDate(string value, int rowIndex)
        {
            DateTime result;

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            {
                return result;
            }

            throw new CsvRowException("Red " + rowIndex + ": Timestamp nije ispravan.");
        }

        private double ParseDouble(string value, string name, int rowIndex)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Equals("NaN", StringComparison.OrdinalIgnoreCase))
            {
                throw new CsvRowException("Red " + rowIndex + ": " + name + " nedostaje ili je NaN.");
            }

            double result;

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }

            throw new CsvRowException("Red " + rowIndex + ": " + name + " nije broj.");
        }

        private string GetValue(string[] values, string columnName)
        {
            int index = FindColumn(columnName);

            if (index >= values.Length)
            {
                return string.Empty;
            }

            return values[index].Trim();
        }

        private void CheckColumn(string columnName)
        {
            FindColumn(columnName);
        }

        private int FindColumn(string columnName)
        {
            string key = Normalize(columnName);
            int index;

            if (columns.TryGetValue(key, out index))
            {
                return index;
            }

            foreach (KeyValuePair<string, int> item in columns)
            {
                if (item.Key.Contains(key) || key.Contains(item.Key))
                {
                    return item.Value;
                }
            }

            throw new CsvRowException("Nije pronađena kolona: " + columnName);
        }

        private static string Normalize(string text)
        {
            StringBuilder builder = new StringBuilder();

            foreach (char c in text.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        private static IEnumerable<string> SplitCsvLine(string line)
        {
            List<string> values = new List<string>();
            StringBuilder current = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (c == ',' && !quoted)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            values.Add(current.ToString());
            return values;
        }
    }
}
