using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    public interface IArenaContents {
        void SetArenaID(ArenaID value);
        void Free();
    }
}
