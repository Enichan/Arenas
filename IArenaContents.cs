using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    public interface IArenaContents {
        void SetArenaID(Guid value);
        void Free();
    }
}
