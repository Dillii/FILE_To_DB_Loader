using System;
using System.Collections.Generic;
using System.Text;

namespace FILE_To_DB_Loader.Interfaces
{
    /// <summary>
    ///  Interface for file data loader
    ///  
    /// In my case it will be XML but you can write your own
    /// </summary>
    public interface IFileDataReader
    {
        /// <summary>
        /// Xml data reader in list of template model type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pathToFile"></param>
        /// <returns></returns>
        IList<T> ReadDataFromFile<T>(string pathToFile) where T : new();
    }
}
