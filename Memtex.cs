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
    /// A Memtex instance represents a single "server" -- attempting to re-acquire a mutex using a Memtex that it owns will return true immediately.
    /// Using Memtex for mutual exclusion amongst multiple threads on one application requires unique Memtex instances; that said, consider using actual Mutexes.
    /// </summary>
    public class Memtex
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
        /// Instantiates a new Memtex instance
        /// </summary>
        /// <param name="client">An implementation of IMemcachedImplementation to communicate with a memcached server</param>
        public Memtex(IMemcachedImplementation client, string GUID = "")
        {
            this.MemcachedClient = client;

            if(string.IsNullOrEmpty(GUID))
                GUID = Guid.NewGuid().ToString();

            this.GUID = GUID;
        }

        public bool AcquireMutex(string name, long msTimeout, long msLifetime)
        {
            if (OwnsMutex(name)) return true;

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

            string casToken = "";

            while (((DateTime.Now - beganAcquiring).TotalMilliseconds < msTimeout) || msTimeout == -1)
            {
                object currentOwnerRaw = this.MemcachedClient.Get(memcachedKey, ref casToken);
                if (currentOwnerRaw == null)
                {
                    bool setOwner = casToken == null ? this.MemcachedClient.Add(memcachedKey, this.GUID, msLifetime)
                                                     : this.MemcachedClient.Cas(memcachedKey, this.GUID, casToken, msLifetime);
                    if (setOwner)
                    {
                        string newCasToken = "";
                        string newOwner = (string)this.MemcachedClient.Get(memcachedKey, ref newCasToken);

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

        public void ReleaseMutex(string name)
        {
            if (!OwnsMutex(name)) return;
            MutexInfo mutex = _ownedMutexes[name];
            this.MemcachedClient.Cas(this.KeyPrefix + mutex.Name, null, mutex.CasToken, 1);
            this.SetOwned(mutex, false);
        }

        public bool OwnsMutex(string name)
        {
            if (!_ownedMutexes.ContainsKey(name)) return false;
            return _ownedMutexes[name].IsAcquired;
        }

        public void SetOwned(MutexInfo info, bool isAcquired)
        {
            info.Attached = isAcquired;
            if (isAcquired) info.TimeAcquired = DateTime.Now;
            _ownedMutexes[info.Name] = info;
        }
    }
}
