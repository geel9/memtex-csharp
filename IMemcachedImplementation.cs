using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Memtex
{
    public interface IMemcachedImplementation
    {
        object Get(string key, ref ulong casToken);

        bool Add(string key, object value, long lifetime);

		bool Cas(string key, object value, ulong casToken, long lifetime);
    }
}
