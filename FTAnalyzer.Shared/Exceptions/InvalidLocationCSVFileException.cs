﻿using System;
using System.Runtime.Serialization;

namespace FTAnalyzer
{
    [Serializable]
    public class InvalidLocationCSVFileException : Exception
    {
        public InvalidLocationCSVFileException(string message)
            : base(message)
        { }

        public InvalidLocationCSVFileException()
        {
        }

        public InvalidLocationCSVFileException(string message, Exception innerException) : base(message, innerException)
        {
        }
        protected InvalidLocationCSVFileException(SerializationInfo serializationInfo, StreamingContext streamingContext)
        {
        }
    }
}
