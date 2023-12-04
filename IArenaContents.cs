using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    public interface IArenaContents {
        IArenaContentsMethodProvider ArenaContentsMethods { get; }
    }

    public interface IArenaContentsMethodProvider {
        void Free(IntPtr ptr);
        void SetArenaID(IntPtr ptr, ArenaID id);
    }

    public unsafe abstract class ArenaContentsMethodsBase<TValue, TSelf> : ArenaContentsMethodsBase<TSelf>, IArenaContentsMethodProvider where TValue : unmanaged where TSelf : class, IArenaContentsMethodProvider, new() {
        public void Free(IntPtr ptr) {
            Free((TValue*)ptr);
        }

        public void SetArenaID(IntPtr ptr, ArenaID id) {
            SetArenaID((TValue*)ptr, id);
        }

        public abstract void Free(TValue* self);
        public abstract void SetArenaID(TValue* self, ArenaID id);
    }

    public abstract class ArenaContentsMethodsBase<TSelf> where TSelf : class, IArenaContentsMethodProvider, new() {
        public readonly static IArenaContentsMethodProvider MethodProviderInstance;

        static ArenaContentsMethodsBase() {
            MethodProviderInstance = new TSelf();
        }
    }
}
