using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FILE_To_DB_Loader.DataReaders;
using FILE_To_DB_Loader.Interfaces;

namespace FILE_To_DB_Loader
{
    public class DirectoryReader : ICancelble
    {
        private readonly object _locker = new object();
        private readonly IDataTypeParser _datatypeParser;
        private readonly IFileDataReader _fileDataReader;

        private int _maxItemsAwatingToLoad;

        private readonly string _fileExtension;
        private string _directoryPath;
        private List<string> _blackList = new List<string>();
        private readonly Task[] _threadsPool;
        public bool IsCanceled { get; private set; } = true;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="directoryPath">Root directory to search files in it and subdirrectories</param>
        /// <param name="fileExtension">file extesion to use in search (with dot - ".XML" for example)</param>
        /// <param name="threadsCount">Active threads count of reading files process</param>
        /// <param name="maxItemsAwatingToLoad">max size of <see cref="DataQueue"/> to start sleeping</param>
        /// <param name="datatypeParser">realization of data type parser <see cref="IDataTypeParser"/></param>
        /// <param name="fileDataReader">realization of file data reader <see cref="IFileDataReader"/></param>
        public DirectoryReader(string directoryPath, string fileExtension, int threadsCount, int maxItemsAwatingToLoad, IDataTypeParser datatypeParser, IFileDataReader fileDataReader)
        {
            _fileDataReader = fileDataReader;
            _fileExtension = fileExtension;
            _maxItemsAwatingToLoad = maxItemsAwatingToLoad;
            _datatypeParser = datatypeParser;
            _directoryPath = directoryPath;
            _threadsPool = new Task[threadsCount];
        }

        private string GetNextFile(string directoryPath)
        {
            try
            {
                string[] files = Directory.GetFiles(directoryPath);
                if (files.Length > 0)
                {
                    foreach (var file in files)
                    {
                        if (!_blackList.Contains(file)) return file;
                    }
                }
                string[] directories = Directory.GetDirectories(directoryPath);
                if (directories.Length > 0)
                {
                    foreach (var directory in directories)
                    {
                        string file = GetNextFile(directory);
                        if (file != null) return file;
                    }
                }
            }
            catch (Exception)
            {
                //didnt fin directory - no matter for us just go forward
            }
            return null;
        }

        /// <summary>
        /// Starts loading process
        /// </summary>
        public void StartReaderProcess()
        {
            IsCanceled = false;
            Task.Run(StartReader);
        }
        public void Cancel()
        {
            IsCanceled = true;
        }
        private void StartReader()
        {
            for (int i = 0; i < _threadsPool.Length; i++)
            {
                _threadsPool[i] = Task.Run(Read);
            }
            while (!_threadsPool.All(pool => pool.IsCompleted || pool.IsFaulted || pool.IsCanceled))
            {
                Thread.Sleep(500);
            }
            while (true)
            {
                foreach (var task in _threadsPool)
                {
                    if (task.IsCompleted || task.IsFaulted || task.IsCanceled) task.Dispose();
                }
                if (_threadsPool.All(task => task.IsCompleted || task.IsFaulted || task.IsCanceled)) break;
                Thread.Sleep(500);
            }
            IsCanceled = true;
        }
        /// <summary>
        /// Read one file and load its data in <see cref="DataQueue"/>
        /// </summary>
        private void Read()
        {
            while (!IsCanceled)
            {
                if (DataQueue.Instance.DataReadyToLoadInDb.Count > _maxItemsAwatingToLoad)
                {
                    //sleep if too many data already in queue to not fill all memory
                    Thread.Sleep(5000);
                    continue;
                }
                string file = null;
                lock (_locker)
                {
                    file = GetNextFile(_directoryPath);
                    if (file == null)
                    {
                        Console.WriteLine($"End of reading.");
                        return;
                    }
                    Console.WriteLine($"Start reading {file}...");
                    if (file != null) _blackList.Add(file);
                }
                var fileInfo = new FileInfo(file);
                var dataType = _datatypeParser.ParseFileNameToModelType(fileInfo);
                if (fileInfo.Extension.ToUpper() != _fileExtension || dataType == null) return; // левый файл просто игнорим

                try
                {
                    //unfortunately i cant find a way to call template method (<>) with dataType without strong Reflection
                    Type ex = _fileDataReader.GetType();
                    MethodInfo mi = ex.GetMethod("ReadDataFromFile");
                    MethodInfo miConstructed = mi.MakeGenericMethod(dataType);
                    object[] args = { file };
                    var result = miConstructed.Invoke(_fileDataReader, args);

                    var dataList = ((IList)result).Cast<object>().ToList();
                    var dataWithPath = new Tuple<string, List<object>>(file, dataList);
                    DataQueue.Instance.DataReadyToLoadInDb.Enqueue(dataWithPath);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error while reading data from file -" + file + " - " + e.Message);
                }
            }
        }

        /// <summary>
        /// Call back function on operation with data from file ended successfull and it can be deleted from directory
        /// </summary>
        /// <param name="file"></param>
        public void OnFileLoadedSuccess(string file)
        {
            try
            {
                var fileInfo = new FileInfo(file);
                var directory = fileInfo.Directory;
                fileInfo.Delete();
                if (directory.GetFiles().Length == 0 && directory.GetDirectories().Length == 0)
                {
                    directory.Delete();
                }
                _blackList.Remove(file);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Console.WriteLine("File -" + file + " successfully loaded in Data Base.");
            }
            catch (Exception e)
            {
                Console.WriteLine("Error on Inserting data in Data Base - " + e.Message);
            }
        }
        /// <summary>
        /// Call back function on operation with data from file ended with error and file will hold in black list
        /// </summary>
        /// <param name="file"></param>
        public void ErrorOnFileLoaded(string file)
        {
            // will hold in black list no not try load file more
            Console.WriteLine("Error on Inserting data in Data Base - " + file);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
