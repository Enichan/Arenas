using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    public interface IArenaContents {
        Guid ArenaID { get; set; }
        void Free();
    }
}
