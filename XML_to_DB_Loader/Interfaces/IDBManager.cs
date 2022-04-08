using System;
using System.Collections.Generic;
using System.Text;

namespace XML_To_DB_Loader.Interfaces
{
    /// <summary>
    /// Data Base manager
    /// </summary>
    public interface IDBManager
    {
        /// <summary>
        /// Connect to Data Base
        /// </summary>
        /// <param name="connectionString">Connection string</param>
        void ConnectToDb(string connectionString);
        /// <summary>
        /// BULK INSERT parsed data to DATA BASE tables
        /// </summary>
        /// <typeparam name="T">Type of data - It will be used <see cref="IModelTypeToTableNameConvertor"/> to convert Type Name into Table Name</typeparam>
        /// <param name="dataList">data list to load in DB</param>
        void CopyDataInDataBase<T>(IList<T> dataList);
        /// <summary>
        /// INSERT/UPDATE or MERGE parsed data to DATA BASE tables
        /// </summary>
        /// <typeparam name="T">Type of data - It will be used <see cref="IModelTypeToTableNameConvertor"/> to convert Type Name into Table Name</typeparam>
        /// <param name="dataList">data list to load in DB</param>
        void MergeDataInDataBase<T>(IList<T> dataList);
    }
}
