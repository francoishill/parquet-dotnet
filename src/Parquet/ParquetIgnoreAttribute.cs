﻿using System;

namespace Parquet {
    /// <summary>
    /// Annotates a class property as a marker to ignore while serializing to parquet file
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ParquetIgnoreAttribute : Attribute {
    }
}
