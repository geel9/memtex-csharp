# memtex-csharp
Memtex-CSharp (**Mem**cached mu**tex**) is the C# version of the Memtex protocol -- a Memcached-backed distributed "mutex" that provides mutex-like locking amongst servers, threads and processes.

##Distributed Mutex
Memtex essentially allows you to distribute a mutex over a network. For instance, if you have multiple C# programs running on one server, or five servers, or 100 servers, they can all share a mutex--or multiple mutexes--over a single memcached server. This is more of a simple protocol than a specific library; bindings are/will be available in numerous languages like PHP, Python, and anything else that has a memcached client library.

##Cross-everything##
Memtex is more of a simple protocol than an actual library. It allows you to share a mutex over anything -- individual threads in a single application (why, though?), multiple processes on a single server, multiple servers, **multiple languages**, or any combination thereof. If it can connect to memcached, you can use the Memtex protocol.

##How to Use
* Clone the repository or download a ZIP containing its source
* Import the project into your solution
* Create a class that extends IMemcachedClient. More on that below.
* Create an instance of Memtex and start mutexing!

##IMemcachedClient
Because there are numerous memcached libraries for .NET, memtex-csharp is not built against any of them. Instead, you must create a small class that implements the IMemcachedClient interface, and provide that in the Memtex constructor. This allows memtex-csharp to interface with any C# memcached library. In future, there will probably be classes written already to plug and play.