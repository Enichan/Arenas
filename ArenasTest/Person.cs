using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Arenas {
    [StructLayout(LayoutKind.Sequential)]
    unsafe public struct Person : IArenaContents {
        private ArenaID arenaID;
        private ManagedRef firstName;
        private ManagedRef lastName;

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

        public class ArenaContentsMethods : ArenaContentsMethodsBase<Person, ArenaContentsMethods> {
            public override void Free(Person* self) {
                self->FirstName = null;
                self->LastName = null;
            }

            public override void SetArenaID(Person* self, ArenaID id) {
                self->arenaID = id;
            }
        }

        IArenaContentsMethodProvider IArenaContents.ArenaContentsMethods { 
            get => ArenaContentsMethodsBase<ArenaContentsMethods>.MethodProviderInstance; 
        }
    }
}
