using System;
using Common;

namespace Service
{
    public static class SampleValidator
    {
        public static string GetFormatError(WindTurbineSample sample)
        {
            if (sample == null)
            {
                return "Uzorak nije poslat.";
            }

            if (sample.Timestamp == DateTime.MinValue)
            {
                return "Timestamp nije ispravan.";
            }

            return null;
        }

        public static string GetValidationError(WindTurbineSample sample)
        {
            if (string.IsNullOrWhiteSpace(sample.TurbineId))
            {
                return "TurbineId je obavezan.";
            }

            if (sample.RowIndex < 11)
            {
                return "RowIndex mora biti 11 ili veći.";
            }

            string missing = FindMissingValue(sample);
            if (missing != null)
            {
                return "Nedostaje vrednost: " + missing;
            }

            if (sample.WindSpeed <= 0)
            {
                return "WindSpeed mora biti veći od 0.";
            }

            if (sample.GridFrequencyHz <= 0)
            {
                return "GridFrequencyHz mora biti veći od 0.";
            }

            return null;
        }

        private static string FindMissingValue(WindTurbineSample sample)
        {
            if (double.IsNaN(sample.WindSpeed)) return "WindSpeed";
            if (double.IsNaN(sample.WindDirection)) return "WindDirection";
            if (double.IsNaN(sample.NacellePosition)) return "NacellePosition";
            if (double.IsNaN(sample.PowerKW)) return "PowerKW";
            if (double.IsNaN(sample.PotentialPowerDefaultKW)) return "PotentialPowerDefaultKW";
            if (double.IsNaN(sample.PowerFactor)) return "PowerFactor";
            if (double.IsNaN(sample.ReactivePowerKvar)) return "ReactivePowerKvar";
            if (double.IsNaN(sample.GridFrequencyHz)) return "GridFrequencyHz";
            if (double.IsNaN(sample.GeneratorRpm)) return "GeneratorRpm";
            return null;
        }
    }
}
