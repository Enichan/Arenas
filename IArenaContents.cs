using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    public interface IArenaContents {
        void Free();
        void SetArenaID(ArenaID id);
        IArenaContentsMethodProvider ArenaContentsMethods { get; }
    }

    public interface IArenaContentsMethodProvider {
        void Free(IntPtr ptr);
        void SetArenaID(IntPtr ptr, ArenaID id);
    }

    public unsafe abstract class ArenaContentsMethodsBase<TValue, TSelf> : ArenaContentsMethodsBase<TSelf>, IArenaContentsMethodProvider where TValue : unmanaged, IArenaContents where TSelf : class, IArenaContentsMethodProvider, new() {
        public void Free(IntPtr ptr) {
            ((TValue*)ptr)->Free();
        }

        public void SetArenaID(IntPtr ptr, ArenaID id) {
            ((TValue*)ptr)->SetArenaID(id);
        }
    }

    public abstract class ArenaContentsMethodsBase<TSelf> where TSelf : class, IArenaContentsMethodProvider, new() {
        public readonly static IArenaContentsMethodProvider MethodProviderInstance;

        static ArenaContentsMethodsBase() {
            MethodProviderInstance = new TSelf();
        }
    }
}
