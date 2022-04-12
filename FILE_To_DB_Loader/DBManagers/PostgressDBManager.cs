using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Npgsql;
using FILE_To_DB_Loader.Interfaces;

namespace FILE_To_DB_Loader.DBManagers
{
    /// <summary>
    /// Realization of copy data from IList of models to Data Base tables with <see cref="IModelTypeToTableNameConvertor"/>
    /// </summary>
    public class PostgressDBManager : IDBManager
    {
        private string _connectionString;
        private IModelTypeToTableNameConvertor _modelTypeToTableNameConvertor;
        private string _dateFormat;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="modelTypeToTableNameConvertor"></param>
        /// <param name="dateFormat"></param>
        public PostgressDBManager(string connectionString, IModelTypeToTableNameConvertor modelTypeToTableNameConvertor, string dateFormat = "yyyy-MM-dd HH:mm:ss.fff")
        {
            _modelTypeToTableNameConvertor = modelTypeToTableNameConvertor;
            _dateFormat = dateFormat;
            ConnectToDb(connectionString);
        }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="host">Host</param>
        /// <param name="dBname">Data Base name</param>
        /// <param name="user">User</param>
        /// <param name="password">Password</param>
        /// <param name="port">Port</param>
        /// <param name="sslMode">SSLMode</param>
        /// <param name="modelTypeToTableNameConvertor">convertor to compare model names to DB table names</param>
        /// <param name="dateFormat">date format - need to be passed as a valid date/timespan with/without time zone in DB</param>
        public PostgressDBManager(string host, string dBname, string user, string password, string port, string sslMode, IModelTypeToTableNameConvertor modelTypeToTableNameConvertor, string dateFormat = "yyyy-MM-dd HH:mm:ss.fff")
            : this(string.Format("Server={0};Username={1};Database={2};Port={3};Password={4};SSLMode={5}", host, user, dBname, dBname, port, password, sslMode), modelTypeToTableNameConvertor, dateFormat)
        {
        }
        /// <summary>
        /// Save and check connection string
        /// </summary>
        /// <param name="connectionString">Connection string to DB</param>
        public void ConnectToDb(string connectionString)
        {
            _connectionString = connectionString;
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
            }
        }
        /// <summary>
        /// BULK INSERT parsed data to DATA BASE tables
        /// </summary>
        /// <typeparam name="T">Type of data - It will be used <see cref="IModelTypeToTableNameConvertor"/> to convert Type Name into Table Name</typeparam>
        /// <param name="dataList">data list to load in DB</param>
        public void CopyDataInDataBase<T>(IList<T> dataList)
        {
            if (dataList.Count == 0) return;
            using (var connection = GetConnection())
            {
                Type dataType = dataList[0].GetType();
                try
                {
                    var properties = dataType.GetProperties();
                    using (var writer = connection.BeginBinaryImport($"COPY \"{_modelTypeToTableNameConvertor.ConvertModelNameToTableName(dataType.Name)}\" ({GenerateColumnsStringByModelType(properties)}) FROM STDIN (FORMAT BINARY)"))
                    {

                        foreach (var dataItem in dataList)
                        {
                            writer.StartRow();
                            foreach (var property in properties)
                            {
                                var value = property.GetValue(dataItem);
                                if (value == null)
                                    writer.WriteNull();
                                else
                                    writer.Write(value, GetNpgsqlDbType(property.PropertyType as TypeInfo));
                            }
                        }
                        writer.Complete();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error! - {e.Message} \n While BULK INSERT model - {dataType.Name}");
                    throw e;
                }
            }
        }
        /// <summary>
        /// INSERT/UPDATE or MERGE parsed data to DATA BASE tables
        /// </summary>
        /// <typeparam name="T">Type of data - It will be used <see cref="IModelTypeToTableNameConvertor"/> to convert Type Name into Table Name</typeparam>
        /// <param name="dataList">data list to load in DB</param>
        public void MergeDataInDataBase<T>(IList<T> dataList)
        {
            if (dataList.Count == 0) return;
            using (var connection = GetConnection())
            {
                Type dataType = dataList[0].GetType();
                try
                {
                    var properties = dataType.GetProperties();
                    if (properties.Length <= 0) return;
                    foreach (var data in dataList)
                    {
                        using var command = connection.CreateCommand();
                        command.CommandText = $"INSERT INTO \"{_modelTypeToTableNameConvertor.ConvertModelNameToTableName(dataType.Name)}\" " +
                            $"({ GenerateColumnsStringByModelType(properties)})" +
                            $" VALUES ({GenerateValuesForInsertCommand(data, properties)})" +
                            $" ON CONFLICT (\"{properties[0].Name.ToUpper()}\") DO UPDATE SET {GenerateSetValuesForUpdateCommand(data, properties, properties[0])};";
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error! - {e.Message} \n While Merge model - {dataType.Name} - in DB" + e.Message);
                    throw e;
                }
            }
        }

        protected string GenerateSetValuesForUpdateCommand<T>(T dataItem, PropertyInfo[] properties, PropertyInfo keyProperty)
        {
            var values = "";
            foreach (var property in properties)
            {
                if (property.Name != keyProperty.Name)
                    values += "\"" + property.Name.ToUpper() + $"\" = {ConvertPropertyByValueType(property, dataItem)}" + ", ";
            }
            values = values.Substring(0, values.Length - 2);
            return values;
        }
        protected string GenerateValuesForInsertCommand<T>(T dataItem, PropertyInfo[] properties)
        {
            var values = "";
            foreach (var property in properties)
            {
                values += ConvertPropertyByValueType(property, dataItem) + ", ";
            }
            values = values.Substring(0, values.Length - 2);
            return values;
        }
        protected string ConvertPropertyByValueType<T>(PropertyInfo property, T dataItem)
        {
            if (property.GetValue(dataItem) == null) return "NULL";
            return "\'" + (property.PropertyType == typeof(DateTime) ? ((DateTime)property.GetValue(dataItem)).ToString(_dateFormat) : property.GetValue(dataItem).ToString()) + "\'";
        }
        protected string GenerateColumnsStringByModelType(PropertyInfo[] properties)
        {
            string result = "";
            foreach (var property in properties)
            {
                result += "\"" + property.Name.ToUpper() + "\"" + ", ";
                //result += property.Name.ToUpper() + ", ";
            }
            result = result.Substring(0, result.Length - 2);

            return result;
        }
        protected NpgsqlTypes.NpgsqlDbType GetNpgsqlDbType(TypeInfo type)
        {
            if (type == null) throw new Exception("Cannot recognize PostgreSQL DB type");
            string typeName = type.GenericTypeArguments.Length > 0 ? type.GenericTypeArguments[0].Name : type.Name;

            switch (typeName)
            {
                case nameof(Int32):
                    return NpgsqlTypes.NpgsqlDbType.Integer;
                case nameof(Int64):
                    return NpgsqlTypes.NpgsqlDbType.Bigint;
                case nameof(DateTime):
                    return NpgsqlTypes.NpgsqlDbType.Timestamp;
                case nameof(Boolean):
                    return NpgsqlTypes.NpgsqlDbType.Boolean;
                case nameof(Guid):
                    return NpgsqlTypes.NpgsqlDbType.Uuid;
                case nameof(String):
                    return NpgsqlTypes.NpgsqlDbType.Char;
            }



            throw new Exception($"Unknown Type {type.Name}");
        }

        protected NpgsqlConnection GetConnection()
        {
            var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            return connection;
        }
    }
}
