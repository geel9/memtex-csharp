using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MemtexCS;
using System.Threading;
namespace Memtex
{
    /// <summary>
	/// A MemtexClient instance represents a single "server" -- attempting to re-acquire a mutex using a MemtexClient that it owns will return true immediately.
	/// Using MemtexClient for mutual exclusion amongst multiple threads on one application requires unique MemtexClient instances; that said, consider using actual Mutexes.
    /// </summary>
    public class MemtexClient
    {
        /// <summary>
        /// The client that Memtex uses to communicate with the memcached server. You should write your own implementation or use one of the provided classes on the github repo.
        /// </summary>
        public IMemcachedImplementation MemcachedClient;

        /// <summary>
        /// The network identifier for this Memtex instance. Used to mark owners of a Memtex.
        /// </summary>
        public string GUID;

        /// <summary>
        /// The prefix assigned to memcached key names. Must be consistent amongst all servers on the memtex pool.
        /// </summary>
        public string KeyPrefix = "memtex-";

        /// <summary>
        /// Milliseconds to sleep between each failed Mutex acquisition
        /// </summary>
        public int SleepTime = 50;


        /// <summary>
        /// The Mutexes we own or have previously owned.
        /// </summary>
        private Dictionary<string, MutexInfo> _ownedMutexes = new Dictionary<string, MutexInfo>();

        /// <summary>
		/// Instantiates a new MemtexClient instance
        /// </summary>
        /// <param name="client">An implementation of IMemcachedImplementation to communicate with a memcached server</param>
		public MemtexClient(IMemcachedImplementation client, string GUID = "")
        {
            this.MemcachedClient = client;

            if(string.IsNullOrEmpty(GUID))
                GUID = Guid.NewGuid().ToString();

            this.GUID = GUID;
        }

        /// <summary>
        /// Acquires a mutex.
        /// </summary>
        /// <param name="name">The name of the mutex to acquire</param>
        /// <param name="msTimeout">The milliseconds to wait before giving up on acquiring</param>
        /// <param name="msLifetime">The maximum length of time the mutex will last after being acquired; -1 for infinity.</param>
        /// <returns>True on success, false on failure.</returns>
        public bool AcquireMutex(string name, long msTimeout, long msLifetime)
        {
			if(msLifetime < -1 || msLifetime == 0) throw new ArgumentOutOfRangeException("msLifetime", "msLifetime must be either -1 or a positive integer.");

            if (OwnsMutex(name)) return true;

	        long memcachedLifetime = (msLifetime == -1) ? 0 : msLifetime; //Memcached requires a lifetime of 0 for infinite-length mutexes

            MutexInfo mutex;
            if (!_ownedMutexes.ContainsKey(name))
            {
                mutex = new MutexInfo()
                {
                    Name = name,
                    Lifetime = msLifetime,
                    ServerGUID = this.GUID,
                };
            }
            else
            {
                mutex = _ownedMutexes[name];
                mutex.Lifetime = msLifetime;
            }

            DateTime beganAcquiring = DateTime.Now;
            string memcachedKey = this.KeyPrefix + name;

            ulong casToken = 0;

            while (((DateTime.Now - beganAcquiring).TotalMilliseconds < msTimeout) || msTimeout == -1)
            {
                object currentOwnerRaw = this.MemcachedClient.Get(memcachedKey, ref casToken);
                if (currentOwnerRaw == null)
                {
                    bool setOwner = casToken == 0 ? this.MemcachedClient.Add(memcachedKey, this.GUID, memcachedLifetime)
                                                     : this.MemcachedClient.Cas(memcachedKey, this.GUID, casToken, memcachedLifetime);
                    if (setOwner)
                    {
                        ulong newCasToken = 0;
                        string newOwner = (string)this.MemcachedClient.Get(memcachedKey, ref newCasToken);

						Console.WriteLine(newCasToken);

                        if (newOwner == this.GUID)
                        {
                            mutex.CasToken = newCasToken;
                            this.SetOwned(mutex, true);
                        }
                    }
                }
                else
                {
                    string currentOwner = (string)currentOwnerRaw;
                    if (currentOwner == this.GUID)
                    {
                        this.SetOwned(mutex, true);
                        return true;
                    }
                }
                Thread.Sleep(this.SleepTime);

            }

            this.SetOwned(mutex, false);
            return false;
        }


        /// <summary>
        /// Releases the specified mutex, if owned.
        /// </summary>
        /// <param name="name">The name of the mutex to release</param>
        public void ReleaseMutex(string name)
        {
            if (!OwnsMutex(name)) return;
            MutexInfo mutex = _ownedMutexes[name];
            this.MemcachedClient.Cas(this.KeyPrefix + mutex.Name, null, mutex.CasToken, 1);
            this.SetOwned(mutex, false);
        }

        /// <summary>
        /// Finds and returns whether or not this Memtex instance owns the specified mutex.
        /// </summary>
        /// <param name="name">The name of the mutex to check against</param>
        /// <returns>True if the mutex is owned, false otherwise.</returns>
        public bool OwnsMutex(string name)
        {
            if (!_ownedMutexes.ContainsKey(name)) return false;
            return _ownedMutexes[name].IsAcquired;
        }


        /// <summary>
        /// Simple helper method to set the acquired state and time acquired of a MutexInfo.
        /// </summary>
        /// <param name="info">The MutexInfo to set the status of</param>
        /// <param name="isAcquired">True if the mutex is owned; false otherwise.</param>
        private void SetOwned(MutexInfo info, bool isAcquired)
        {
            info.Attached = isAcquired;
            if (isAcquired) info.TimeAcquired = DateTime.Now;
            _ownedMutexes[info.Name] = info;
        }
    }
}
