using System;
using System.Collections.Generic;
using System.Text;

namespace Arenas {
    public interface IArenaPoolable {
        bool ResetForPool();
    }

    public class ArenaPool : ManagedObjectPool<Arena> {
        private IMemoryAllocator allocator;
        private int pageSize;

        public ArenaPool(IMemoryAllocator allocator = null, int pageSize = Arena.DefaultPageSize) 
            : base() {
            this.allocator = allocator;
            this.pageSize = pageSize;
            createInstance = CreateInstance;
            resetInstance = ResetInstance;
        }

        private Arena CreateInstance() {
            return new Arena(allocator ?? Arena.DefaultAllocator, pageSize);
        }

        private bool ResetInstance(Arena arena) {
            return ((IArenaPoolable)arena).ResetForPool();
        }

        private static ArenaPool defaultPool = new ArenaPool();
        public static ArenaPool Default { get { return defaultPool; } set { defaultPool = value; } }
    }
}
