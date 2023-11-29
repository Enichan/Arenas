using System;
using System.Runtime.InteropServices;

namespace Arenas {
    [StructLayout(LayoutKind.Sequential)]
    unsafe public struct Person : IArenaContents {
        private Guid arenaID;
        private ManagedRef firstName;
        private ManagedRef lastName;

        void IArenaContents.Free() {
            // free managed references by setting to null
            FirstName = null; 
            LastName = null;
        }

        public override string ToString() {
            return $"{FirstName} {LastName}";
        }

        public string FirstName {
            get { return firstName.Get<string>(); }
            set { firstName = firstName.Set(Arena.Get(arenaID), value); }
        }
        public string LastName {
            get { return lastName.Get<string>(); }
            set { lastName = lastName.Set(Arena.Get(arenaID), value); }
        }

        void IArenaContents.SetArenaID(Guid value) { arenaID = value; }
    }
}
