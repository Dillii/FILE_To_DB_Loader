using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Xml;
using XML_To_DB_Loader.Interfaces;

namespace XML_To_DB_Loader.DataReaders
{
    /// <summary>
    /// XML file reader
    /// </summary>
    public class XmlFileDataReader : IFileDataReader
    {
        /// <summary>
        /// Reads XML file into <see cref="IList{T}"/> where model (T) is data representation of xml file
        /// 
        /// Data should store in attributes and names of models properties should compare to attribute names (no matter letters case) ad type 
        /// </summary>
        /// <typeparam name="T">Model that copares to data in XML file</typeparam>
        /// <param name="pathToFile">Path to XML file</param>
        /// <returns></returns>
        public IList<T> ReadDataFromFile<T>(string pathToFile) where T : new()
        {
            using (var reader = XmlReader.Create(pathToFile))
            {
                reader.MoveToContent();

                var propertyList = typeof(T).GetProperties();

                var objList = new List<T>();
                while (reader.Read())
                {
                    if (reader.AttributeCount == propertyList.Length)
                    {
                        try
                        {
                            var obj = new T();
                            foreach (var prop in propertyList)
                            {
                                var propType = prop.PropertyType;
                                var converter = TypeDescriptor.GetConverter(propType);
                                var convertedObject = converter.ConvertFromString(reader.GetAttribute(prop.Name.ToUpper()));
                                prop.SetValue(obj, convertedObject);
                            }
                            objList.Add(obj);
                        }
                        catch(Exception e)
                        {
                            Console.WriteLine("model - " + typeof(T).Name + "must have parameterless constructor");
                            throw;
                        }
                    }
                }
                return objList;
            }
        }
    }
}
