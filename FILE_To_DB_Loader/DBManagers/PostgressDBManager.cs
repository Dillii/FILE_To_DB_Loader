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
        /// Constructor
        /// </summary>
        /// <param name="connectionString">Full connection string</param>
        /// <param name="modelTypeToTableNameConvertor">convertor to compare model names to DB table names</param>
        /// <param name="dateFormat">date format - need to be passed as a valid date/timespan with/without time zone in DB</param>
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
        /// <param name="timeOut">Timeout</param>
        /// <param name="commandTimeOut">Command timeout</param>
        /// <param name="modelTypeToTableNameConvertor">convertor to compare model names to DB table names</param>
        /// <param name="dateFormat">date format - need to be passed as a valid date/timespan with/without time zone in DB</param>
        public PostgressDBManager(string host, string dBname, string user, string password, string port, string sslMode, int timeOut, int commandTimeOut, IModelTypeToTableNameConvertor modelTypeToTableNameConvertor, string dateFormat = "yyyy-MM-dd HH:mm:ss.fff")
            : this(string.Format("Server={0};Username={1};Database={2};Port={3};Password={4};SSLMode={5};Timeout={timeOut};CommandTimeout={commandTimeOut}", host, user, dBname, dBname, port, password, sslMode, timeOut, commandTimeOut), modelTypeToTableNameConvertor, dateFormat)
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
                Type dataType = typeof(T);
                try
                {
                    var properties = dataType.GetProperties();
                    if (properties.Length <= 0) return;
                    foreach (var data in dataList)
                    {
                        using var command = connection.CreateCommand();
                        command.CommandText = $"INSERT INTO \"{_modelTypeToTableNameConvertor.ConvertModelNameToTableName(dataType.Name)}\" " +
                            $"({ GenerateColumnsStringByModelType(properties)})" +
                            $" VALUES ({GenerateValuesForInsertCommand(data)})" +
                            $" ON CONFLICT (\"{properties[0].Name.ToUpper()}\") DO UPDATE SET {GenerateSetValuesForUpdateCommand(data, properties[0])};";
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
        /// <summary>
        /// Reads all properties in type T and makes update part of insert/update command 
        /// </summary>
        /// <typeparam name="T">model Type that should compare to table name in properties(column) names without case</typeparam>
        /// <param name="dataItem"></param>
        /// <param name="keyProperty">matching key - ignored in update command</param>
        /// <exception cref="ArgumentException"></exception>
        /// <returns></returns>
        protected string GenerateSetValuesForUpdateCommand<T>(T dataItem, PropertyInfo keyProperty)
        {
            var values = "";
            foreach (var property in typeof(T).GetProperties())
            {
                if (property.Name != keyProperty.Name)
                    values += "\"" + property.Name.ToUpper() + $"\" = {ConvertPropertyByValueType(property, dataItem)}" + ", ";
            }
            if (values.Length < 2) throw new ArgumentException("Cant find any properties in type " + typeof(T).Name);
            values = values.Substring(0, values.Length - 2);
            return values;
        }
        /// <summary>
        /// Reads all properties in type T and makes insert part of insert/update command 
        /// </summary>
        /// <typeparam name="T">model Type that should compare to table name in properties(column) names without case</typeparam>
        /// <param name="dataItem">matching key - ignored in update command</param>
        /// <returns></returns>
        protected string GenerateValuesForInsertCommand<T>(T dataItem)
        {
            var values = "";
            foreach (var property in typeof(T).GetProperties())
            {
                values += ConvertPropertyByValueType(property, dataItem) + ", ";
            }
            if (values.Length < 2) throw new ArgumentException("Cant find any properties in type " + typeof(T).Name);
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
            }
            result = result.Substring(0, result.Length - 2);

            return result;
        }
        /// <summary>
        /// Converts property type to DB type 
        /// 
        /// If you need more types just inherit and override
        /// </summary>
        /// <param name="type">Model type</param>
        /// <returns></returns>
        protected virtual NpgsqlTypes.NpgsqlDbType GetNpgsqlDbType(TypeInfo type)
        {
            if (type == null) throw new Exception("Cannot convert null to PostgreSQL DB type");
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
