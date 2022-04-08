using System;
using System.Collections.Generic;
using System.Text;

namespace FILE_To_DB_Loader.Interfaces
{
    /// <summary>
    /// Implements method for Converting parsed model Type to Existing Table names in Data Base
    /// 
    /// Need to auto insert parsed data in that tables
    /// </summary>
    public interface IModelTypeToTableNameConvertor
    {
        /// <summary>
        /// Convert model Type to Table name
        /// </summary>
        /// <param name="modelName">Model Type Name</param>
        /// <returns>Table Name</returns>
        string ConvertModelNameToTableName(string modelName);
    }
}
