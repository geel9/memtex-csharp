using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemtexCS
{
    public class MutexInfo
    {
        /// <summary>
        /// The name of this networked Mutex
        /// </summary>
        public string Name;

        /// <summary>
        /// The time we acquired this mutex.
        /// </summary>
        public DateTime TimeAcquired;

        /// <summary>
        /// The total length of the mutex's lifetime; -1 for infinite.
        /// </summary>
        public long Lifetime;

        /// <summary>
        /// The UUID assigned to the Memtex instance
        /// </summary>
        public string ServerGUID;

        /// <summary>
        /// Used for safely releasing a mutex
        /// </summary>
        public ulong CasToken;

        /// <summary>
        /// True if we currently own this mutex
        /// </summary>
        public bool IsAcquired
        {
            get
            {
                return Attached && (((DateTime.Now - TimeAcquired).TotalMilliseconds < Lifetime) || Lifetime == -1);
            }
        }

        /// <summary>
        /// Internal tracking of whether we are maintaining this mutex.
        /// </summary>
        internal bool Attached;
    }
}
