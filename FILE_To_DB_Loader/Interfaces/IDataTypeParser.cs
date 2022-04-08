using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FILE_To_DB_Loader.Interfaces
{
    /// <summary>
    /// Implements methods to parse file name into model types
    /// 
    /// In order not to engage in manual file to model Type conversion
    /// you need to find relationship between file name and Type name of your models
    /// </summary>
    public interface IDataTypeParser
    {
        /// <summary>
        /// File to model type Parser
        /// </summary>
        /// <param name="file">file to parse</param>
        /// <returns>Model Type</returns>
        Type ParseFileNameToModelType(FileInfo file);
    }
}
