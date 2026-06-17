using System;

namespace Client
{
    public class CsvRowException : Exception
    {
        public CsvRowException(string message) : base(message)
        {
        }
    }
}
