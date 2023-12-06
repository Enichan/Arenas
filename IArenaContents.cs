using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// This has become a bit of a mess thanks to NativeAOT not being great with reflection and because of a
// desire to reduce excessive boilerplate. The gist of it is this:
//
// 1. Structs that can have ManagedRefs and/or be freed by an arena must implement IArenaContents.
// 2. The Free method is used to free any resources (like Dispose) and the ArenaID is used to get the
//    arena the struct is allocated to.
// 3. The ArenaMethods property returns an object implementing IArenaMethods. When a type is first
//    allocated on an arena this object is requested once and cached for the remaining runtime of the
//    program. It exposes Free and SetArenaID methods that are used on structs which implement the
//    IArenaContents interface and are expected to call the Free method and set the ArenaID property.
// 4. The IArenaContents implementing struct of type T should return an instance of ArenaMethods<T>,
//    which takes an IntPtr, casts it to a pointer to the struct, and calls Free or assigns ArenaID.
//    Because of this arenas can take an unmanaged struct T, detect if it implements IArenaContents,
//    request the cached version of ArenaMethods<T>, and pass a pointer to the struct in order to
//    perform necessary operations on the struct without boxing and without reflection.
// 5. User code should not be going around manipulating the ArenaID of arena-bound structs so this
//    property should be implemented explicitly to avoid being used by user code.
// 6. But if the ArenaID property is explicit it's also hidden from the struct's code which means it
//    can't interact with the arena it's in. This is solved via IArenaContentsExtensions.GetArena
//    which indirectly allows the struct to retrieve its own arena.
//
// This leads to only two lines of boilerplate:
//
// ArenaID IArenaContents.ArenaID { get; set; }
// IArenaMethods IArenaContents.ArenaMethods { get => ArenaMethods<T>.Instance; }
//
// Where T should be altered to the type of the containing struct.
namespace Arenas {
    public interface IArenaContents {
        void Free();
        ArenaID ArenaID { get; set; }
        IArenaMethods ArenaMethods { get; }
    }

    public interface IArenaMethods {
        void Free(IntPtr ptr);
        void SetArenaID(IntPtr ptr, ArenaID id);
    }

    public unsafe sealed class ArenaMethods<T> : IArenaMethods where T : unmanaged, IArenaContents {
        public void Free(IntPtr ptr) {
            ((T*)ptr)->Free();
        }

        public void SetArenaID(IntPtr ptr, ArenaID id) {
            ((T*)ptr)->ArenaID = id;
        }

        private static readonly ArenaMethods<T> inst = new ArenaMethods<T>();
        public static ArenaMethods<T> Instance { get { return inst; } }
    }

    public static class IArenaContentsExtensions {
        public static Arena GetArena<T>(ref this T inst) where T : unmanaged, IArenaContents {
            return Arena.Get(inst.ArenaID);
        }
    }
}
