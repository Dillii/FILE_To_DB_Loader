using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace FILE_To_DB_Loader
{
    /// <summary>
    /// Realization thread safe SINGLETON queue of data awating to load in DataBase or elsewhere 
    /// </summary>
    public sealed class DataQueue
    {
        private static DataQueue instance = null;
        private static readonly object padlock = new object();

        /// <summary>
        /// Main queue of loaded data from files
        /// </summary>
        public ConcurrentQueue<Tuple<string, List<object>>> DataReadyToLoadInDb { get; } = new ConcurrentQueue<Tuple<string, List<object>>>();


        DataQueue()
        {
        }

        /// <summary>
        /// Instance
        /// </summary>
        public static DataQueue Instance
        {
            get
            {
                lock (padlock)
                {
                    if (instance == null)
                    {
                        instance = new DataQueue();
                    }
                    return instance;
                }
            }
        }
    }
}
