using System;
using System.Collections.Generic;
using System.Text;

namespace FILE_To_DB_Loader.Interfaces
{
    /// <summary>
    /// Determine that service can be cancel
    /// </summary>
    public interface ICancelble
    {
        /// <summary>
        /// Service canceled
        /// </summary>
        bool IsCanceled { get; }
        /// <summary>
        /// Set service is canceled
        /// </summary>
        void Cancel();
    }
}
