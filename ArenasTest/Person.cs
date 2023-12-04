using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Arenas {
    [StructLayout(LayoutKind.Sequential)]
    unsafe public struct Person : IArenaContents {
        // this boilerplate requires changing one generic type from struct to struct
        public class ArenaContentsMethods : ArenaContentsMethodsBase<Person, ArenaContentsMethods> { }

        private ArenaID arenaID;
        private ManagedRef firstName;
        private ManagedRef lastName;

        public override string ToString() {
            return $"{FirstName} {LastName}";
        }

        void IArenaContents.Free() {
            // free managed references by setting to null
            FirstName = null;
            LastName = null;
        }

        public string FirstName {
            get { return firstName.Get<string>(); }
            set { firstName = firstName.Set(Arena.Get(arenaID), value); }
        }
        public string LastName {
            get { return lastName.Get<string>(); }
            set { lastName = lastName.Set(Arena.Get(arenaID), value); }
        }

        // boilerplate, this all stays identical from struct to struct
        void IArenaContents.SetArenaID(ArenaID id) { arenaID = id; }
        IArenaContentsMethodProvider IArenaContents.ArenaContentsMethods { 
            get => ArenaContentsMethodsBase<ArenaContentsMethods>.MethodProviderInstance; 
        }
    }
}
