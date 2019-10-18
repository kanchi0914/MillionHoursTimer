using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MHTimer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class IEnumerableExtensions
    {
        private sealed class CommonSelector<T, TKey> : IEqualityComparer<T>
        {
            private Func<T, TKey> m_selector;

            public CommonSelector(Func<T, TKey> selector)
            {
                m_selector = selector;
            }

            public bool Equals(T x, T y)
            {
                return m_selector(x).Equals(m_selector(y));
            }

            public int GetHashCode(T obj)
            {
                return m_selector(obj).GetHashCode();
            }
        }

        public static bool SequenceEqual<T, TKey>(
            this IEnumerable<T> source,
            IEnumerable<T> second,
            Func<T, TKey> selector
        )
        {
            return source.SequenceEqual(
                second,
                new CommonSelector<T, TKey>(selector)
            );
        }
    }
}
