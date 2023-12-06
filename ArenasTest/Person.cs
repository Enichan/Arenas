using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Arenas {
    [StructLayout(LayoutKind.Sequential)]
    unsafe public struct Person : IArenaContents {
        // these two lines are boilerplate for IArenaContents structs
        ArenaID IArenaContents.ArenaID { get; set; }
        IArenaMethods IArenaContents.ArenaMethods { get => ArenaMethods<Person>.Instance; }

        private ManagedRef firstName;
        private ManagedRef lastName;

        public override string ToString() {
            return $"{FirstName} {LastName}";
        }

        public void Free() {
            // free managed references by setting to null
            FirstName = null;
            LastName = null;
        }

        public string FirstName {
            get { return firstName.Get<string>(); }
            set { firstName = firstName.Set(ref this, value); }
        }
        public string LastName {
            get { return lastName.Get<string>(); }
            set { lastName = lastName.Set(ref this, value); }
        }
    }
}
