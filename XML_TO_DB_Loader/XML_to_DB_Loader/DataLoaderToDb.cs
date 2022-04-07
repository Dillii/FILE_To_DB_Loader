using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XML_To_DB_Loader.Interfaces;

namespace XML_To_DB_Loader
{
    /// <summary>
    /// Implements methods to load data in Data Base in multiple threads
    /// </summary>
    public class DataLoaderToDb : ICancelble
    {
        private readonly object _locker = new object();
        private readonly ICancelble _filesLoader;

        private readonly Task[] _threadsPool;

        /// <summary>
        /// Service canceled
        /// </summary>
        public bool IsCanceled { get; private set; } = true;

        public delegate void DataSuccessfullyLoadedDelegate(string path);
        public delegate void DataErrorOnLoadDelegate(string path);

        public event DataSuccessfullyLoadedDelegate DataSuccessfullyLoaded;
        public event DataErrorOnLoadDelegate DataErrorOnLoad;

        private IDBManager _dBManager;
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="IDBManager">Data Base manager</param>
        /// <param name="threadsCount">Working Threads Count</param>
        /// <param name="filesLoader">Working XML reader - that who actually putting data in <see cref="DataQueue"/></param>
        public DataLoaderToDb(IDBManager IDBManager, int threadsCount, ICancelble filesLoader)
        {
            _filesLoader = filesLoader;
            _dBManager = IDBManager;
            _threadsPool = new Task[threadsCount];
        }

        /// <summary>
        /// Loads list of data into Data Base
        /// </summary>
        /// <param name="loadData"></param>
        private void LoadDataToDataBase(Action<List<object>> loadData)
        {
            IsCanceled = false;
            while (true)
            {
                if (IsCanceled) return;
                Tuple<string, List<object>> dataTuple = null;
                try
                {
                    lock (_locker)
                    {
                        DataQueue.Instance.DataReadyToLoadInDb.TryDequeue(out dataTuple); // getting avaliable data to load
                    }
                    if (dataTuple != null)
                    {
                        Console.WriteLine($"Loading data from file - {dataTuple.Item1} into Data Base with method - " + loadData.Method.Name);
                        loadData(dataTuple.Item2);
                        dataTuple.Item2.Clear();
                        DataSuccessfullyLoaded.Invoke(dataTuple.Item1);
                    }
                    else if (_filesLoader.IsCanceled) return; // no avaliable data and filesReader already stopped his work - means no more data will be avaliable
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error! - {e.Message} \nWhile Loading data from file - {dataTuple.Item1} into Data Base with method - " + loadData.Method.Name);
                    dataTuple?.Item2?.Clear();
                    DataErrorOnLoad.Invoke(dataTuple == null ? "" : dataTuple.Item1);
                }
            }
        }
        /// <summary>
        /// Starting loading process
        /// Fast load big amount of new data in table
        /// 
        /// Should not use if you have any data to update cous you will get Conflict Exception
        /// </summary>
        public void StartLoaderProcess()
        {
            IsCanceled = false;
            Task.Run(() => { StartActionProcess(_dBManager.CopyDataInDataBase); });
        }
        /// <summary>
        /// Starting Updating Process
        /// Not so fast as StartLoaderProcess but this method can update rows
        /// 
        /// This method should be used if you have data to update
        /// </summary>
        public void StartUpdaterProcess()
        {
            IsCanceled = false;
            Task.Run(() => { StartActionProcess(_dBManager.MergeDataInDataBase); });
        }

        /// <summary>
        /// The eternal cycle for load all avaliable data while xml reader still working
        /// </summary>
        /// <param name="loadData"></param>
        private void StartActionProcess(Action<List<object>> loadData)
        {
            for (int i = 0; i < _threadsPool.Length; i++)
            {
                _threadsPool[i] = Task.Run(() => LoadDataToDataBase(loadData));
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
        /// Set service is canceled
        /// </summary>
        public void Cancel()
        {
            IsCanceled = true;
        }
    }
}
