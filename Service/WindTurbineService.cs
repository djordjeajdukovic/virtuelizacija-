using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.ServiceModel;
using Common;

namespace Service
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Single)]
    public class WindTurbineService : IWindTurbineService
    {
        private readonly double windSpikeThresholdMs;
        private readonly double lowWindCutInMs;
        private readonly double nacelleDeviationDeg;
        private readonly double powerDropPct;

        private SessionMeta currentSession;
        private SessionFileWriter writer;
        private WindTurbineSample previousSample;
        private int batchNumber;
        private int totalAccepted;
        private int totalRejected;

        public event EventHandler<TransferEventArgs> OnTransferStarted;
        public event EventHandler<SampleReceivedEventArgs> OnSampleReceived;
        public event EventHandler<TransferEventArgs> OnTransferCompleted;
        public event EventHandler<WarningEventArgs> OnWarningRaised;
        public event EventHandler<BatchReceivedEventArgs> OnBatchReceived;

        public WindTurbineService()
        {
            windSpikeThresholdMs = ReadDoubleSetting("WindSpikeThresholdMs", 5.0);
            lowWindCutInMs = ReadDoubleSetting("LowWindCutInMs", 3.0);
            nacelleDeviationDeg = ReadDoubleSetting("NacelleDeviationDeg", 20.0);
            powerDropPct = ReadDoubleSetting("PowerDropPct", 30.0);
        }

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
            previousSample = null;
            batchNumber = 0;
            totalAccepted = 0;
            totalRejected = 0;

            RaiseTransferStarted();
        }

        public void PushSample(WindTurbineSample sample)
        {
            EnsureSession();

            string formatError = SampleValidator.GetFormatError(sample);
            if (formatError != null)
            {
                WriteReject(sample, formatError);
                totalRejected++;
                throw new FaultException<DataFormatFault>(new DataFormatFault(formatError), "DataFormatFault");
            }

            string validationError = SampleValidator.GetValidationError(sample);
            if (validationError != null)
            {
                WriteReject(sample, validationError);
                totalRejected++;
                throw new FaultException<ValidationFault>(new ValidationFault(validationError), "ValidationFault");
            }

            AcceptSample(sample);
        }

        public BatchResult PushBatch(WindTurbineSample[] samples)
        {
            EnsureSession();

            if (samples == null || samples.Length == 0)
            {
                throw new FaultException<DataFormatFault>(new DataFormatFault("Blok nema uzoraka."), "DataFormatFault");
            }

            batchNumber++;
            int accepted = 0;
            int rejected = 0;

            foreach (WindTurbineSample sample in samples)
            {
                string error = SampleValidator.GetFormatError(sample);

                if (error == null)
                {
                    error = SampleValidator.GetValidationError(sample);
                }

                if (error != null)
                {
                    WriteReject(sample, error);
                    rejected++;
                    totalRejected++;
                    continue;
                }

                AcceptSample(sample);
                accepted++;
            }

            BatchResult result = new BatchResult
            {
                BatchNumber = batchNumber,
                ReceivedCount = samples.Length,
                AcceptedCount = accepted,
                RejectedCount = rejected,
                Status = "BATCH_RECEIVED",
                Message = "Blok primljen"
            };

            RaiseBatchReceived(result);
            return result;
        }

        public void EndSession()
        {
            EnsureSession();
            RaiseTransferCompleted();
            CloseWriter();
            currentSession = null;
            previousSample = null;
        }

        private void AcceptSample(WindTurbineSample sample)
        {
            writer.WriteSample(sample);
            totalAccepted++;
            RaiseSampleReceived(sample);
            AnalyzeSample(sample);
        }

        private void AnalyzeSample(WindTurbineSample sample)
        {
            if (HasNaN(sample))
            {
                return;
            }

            if (sample.WindSpeed < lowWindCutInMs && sample.PowerKW > 0)
            {
                RaiseWarning("CutInAnomalyWarning",
                    "WindSpeed " + Format(sample.WindSpeed) + " m/s je ispod " + Format(lowWindCutInMs) +
                    " m/s, a Power je " + Format(sample.PowerKW) + " kW.", sample.RowIndex);
            }

            if (previousSample != null && !HasNaN(previousSample))
            {
                double windDelta = sample.WindSpeed - previousSample.WindSpeed;

                if (Math.Abs(windDelta) > windSpikeThresholdMs)
                {
                    string direction = windDelta > 0 ? "porast" : "pad";
                    RaiseWarning("WindSpikeWarning",
                        direction + " brzine vetra za " + Format(Math.Abs(windDelta)) + " m/s.", sample.RowIndex);
                }

                double nacelleDelta = Math.Abs(sample.NacellePosition - previousSample.NacellePosition);

                if (nacelleDelta > nacelleDeviationDeg)
                {
                    RaiseWarning("NacelleJumpWarning",
                        "Promena polozaja nacelle je " + Format(nacelleDelta) + " stepeni.", sample.RowIndex);
                }

                if (previousSample.PowerKW > 0)
                {
                    double dropPct = (previousSample.PowerKW - sample.PowerKW) / previousSample.PowerKW * 100.0;

                    if (dropPct > powerDropPct)
                    {
                        RaiseWarning("PowerDropWarning",
                            "Snaga je pala za " + Format(dropPct) + "%.", sample.RowIndex);
                    }
                }
            }

            previousSample = sample;
        }

        private void EnsureSession()
        {
            if (writer == null || currentSession == null)
            {
                throw new FaultException<ValidationFault>(new ValidationFault("Sesija nije pokrenuta."), "ValidationFault");
            }
        }

        private void WriteReject(WindTurbineSample sample, string reason)
        {
            int rowIndex = sample == null ? 0 : sample.RowIndex;
            string originalLine = sample == null ? string.Empty : sample.OriginalLine;
            writer.WriteReject(rowIndex, reason, originalLine);
        }

        private void RaiseTransferStarted()
        {
            EventHandler<TransferEventArgs> handler = OnTransferStarted;

            if (handler != null)
            {
                handler(this, new TransferEventArgs
                {
                    TurbineId = currentSession.TurbineId,
                    SourceFileName = currentSession.SourceFileName
                });
            }
        }

        private void RaiseSampleReceived(WindTurbineSample sample)
        {
            EventHandler<SampleReceivedEventArgs> handler = OnSampleReceived;

            if (handler != null)
            {
                handler(this, new SampleReceivedEventArgs { Sample = sample });
            }
        }

        private void RaiseBatchReceived(BatchResult result)
        {
            EventHandler<BatchReceivedEventArgs> handler = OnBatchReceived;

            if (handler != null)
            {
                handler(this, new BatchReceivedEventArgs
                {
                    BatchNumber = result.BatchNumber,
                    ReceivedCount = result.ReceivedCount,
                    AcceptedCount = result.AcceptedCount,
                    RejectedCount = result.RejectedCount
                });
            }
        }

        private void RaiseWarning(string warningType, string message, int rowIndex)
        {
            EventHandler<WarningEventArgs> handler = OnWarningRaised;

            if (handler != null)
            {
                handler(this, new WarningEventArgs
                {
                    WarningType = warningType,
                    Message = message,
                    RowIndex = rowIndex
                });
            }
        }

        private void RaiseTransferCompleted()
        {
            EventHandler<TransferEventArgs> handler = OnTransferCompleted;

            if (handler != null)
            {
                handler(this, new TransferEventArgs
                {
                    TurbineId = currentSession.TurbineId,
                    SourceFileName = currentSession.SourceFileName,
                    AcceptedCount = totalAccepted,
                    RejectedCount = totalRejected
                });
            }
        }

        private void CloseWriter()
        {
            if (writer != null)
            {
                writer.Dispose();
                writer = null;
            }
        }

        private static bool HasNaN(WindTurbineSample sample)
        {
            return double.IsNaN(sample.WindSpeed) ||
                   double.IsNaN(sample.NacellePosition) ||
                   double.IsNaN(sample.PowerKW);
        }

        private static string Format(double value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static double ReadDoubleSetting(string key, double defaultValue)
        {
            string value = ConfigurationManager.AppSettings[key];
            double parsed;

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            return defaultValue;
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
