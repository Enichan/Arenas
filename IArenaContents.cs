using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    public interface IArenaContents {
        void SetArenaID(Guid value);
        void Free();
    }

    public delegate void FreeDelegate(IntPtr pointer);

    public unsafe static class ArenaContentsHelper {
        [ThreadStatic]
        private static Dictionary<Type, FreeDelegate> _freeDelegates;
        private static Dictionary<Type, FreeDelegate> freeDelegates { get { return _freeDelegates ?? (_freeDelegates = new Dictionary<Type, FreeDelegate>()); } }

        private static readonly MethodInfo freeMethodBase;

        static ArenaContentsHelper() {
            foreach (var method in typeof(ArenaContentsHelper).GetMethods(BindingFlags.Public | BindingFlags.Static)) {
                if (method.Name == "Free" && method.IsGenericMethod && method.GetParameters()[0].ParameterType == typeof(IntPtr)) {
                    freeMethodBase = method;
                    break;
                }
            }

            if (freeMethodBase == null) {
                throw new MissingMethodException(nameof(ArenaContentsHelper), "Expected method `void Free<T>(IntPtr pointer) where T : unmanaged` in ArenaContentsHelper");
            }
        }

        #region Free
        // prevent boxing of IArenaContents when calling Free
        private class ArenaContentsFree : InterfaceAction {
            public static void Free<T>(T* self) where T : unmanaged, IArenaContents {
                self->Free();
            }

            private static readonly Type interfaceType = typeof(IArenaContents);
            public override Type InterfaceType { get { return interfaceType; } }
            public override MethodInfo RoutingMethod { get { return typeof(ArenaContentsFree).GetMethod("Free"); } }
        }

        [ThreadStatic]
        private static ArenaContentsFree _arenaContentsFree;
        private static ArenaContentsFree arenaContentsFree {
            get {
                return _arenaContentsFree ?? (_arenaContentsFree = new ArenaContentsFree());
            }
        }

        public static void Free<T>(T* pointer) where T : unmanaged {
            if (pointer == null) {
                return;
            }
            if (typeof(IArenaContents).IsAssignableFrom(typeof(T))) {
                arenaContentsFree.GetDelegate<T>().Invoke(pointer);
            }
        }

        public static void Free<T>(IntPtr pointer) where T : unmanaged {
            Free((T*)pointer);
        }

        public static void Free(IntPtr pointer, Type type) {
            GetFreeDelegate(type).Invoke(pointer);
        }

        public static FreeDelegate GetFreeDelegate(Type type) {
            var delegates = freeDelegates;

            FreeDelegate free;
            if (!delegates.TryGetValue(type, out free)) {
                var genericFree = freeMethodBase.MakeGenericMethod(type);
                free = (FreeDelegate)Delegate.CreateDelegate(typeof(FreeDelegate), genericFree);
            }

            return free;
        }
        #endregion

        #region SetArenaID
        // prevent boxing of IArenaContents when calling SetArenaID
        private class ArenaContentsSetID : InterfaceAction<Guid> {
            public static void SetArenaID<T>(T* self, Guid value) where T : unmanaged, IArenaContents {
                self->SetArenaID(value);
            }

            private static readonly Type interfaceType = typeof(IArenaContents);
            public override Type InterfaceType { get { return interfaceType; } }
            public override MethodInfo RoutingMethod { get { return typeof(ArenaContentsSetID).GetMethod("SetArenaID"); } }
        }

        [ThreadStatic]
        private static ArenaContentsSetID _arenaContentsSetArenaID;
        private static ArenaContentsSetID arenaContentsSetArenaID {
            get {
                return _arenaContentsSetArenaID ?? (_arenaContentsSetArenaID = new ArenaContentsSetID());
            }
        }

        public static void SetArenaID<T>(T* pointer, Guid value) where T : unmanaged {
            if (pointer == null) {
                return;
            }
            if (typeof(IArenaContents).IsAssignableFrom(typeof(T))) {
                arenaContentsSetArenaID.GetDelegate<T>().Invoke(pointer, value);
            }
        }
        #endregion

        #region Unboxed interface call helper classes
        private delegate void UnboxedInterfaceAction<TStruct>(TStruct* value) where TStruct : unmanaged;

        private abstract class InterfaceAction {
            private Dictionary<Type, Delegate> delegateMap;
            private MethodInfo cachedRoutingMethod;

            public InterfaceAction() {
                delegateMap = new Dictionary<Type, Delegate>();
                cachedRoutingMethod = RoutingMethod;
            }

            public UnboxedInterfaceAction<TStruct> GetDelegate<TStruct>() where TStruct : unmanaged {
                Delegate del;
                var structType = typeof(TStruct);
                if (!delegateMap.TryGetValue(structType, out del)) {
                    var genericMethod = cachedRoutingMethod.MakeGenericMethod(structType);
                    var genericType = typeof(UnboxedInterfaceAction<TStruct>);
                    del = Delegate.CreateDelegate(genericType, genericMethod);
                    delegateMap[structType] = del;
                }
                return (UnboxedInterfaceAction<TStruct>)del;
            }

            public abstract Type InterfaceType { get; }
            public abstract MethodInfo RoutingMethod { get; }
        }

        public delegate void UnboxedInterfaceAction<TStruct, T1>(TStruct* value, T1 arg1) where TStruct : unmanaged;

        public abstract class InterfaceAction<T1> {
            private Dictionary<Type, Delegate> delegateMap;
            private MethodInfo cachedRoutingMethod;

            public InterfaceAction() {
                delegateMap = new Dictionary<Type, Delegate>();
                cachedRoutingMethod = RoutingMethod;
            }

            public UnboxedInterfaceAction<TStruct, T1> GetDelegate<TStruct>() where TStruct : unmanaged {
                Delegate del;
                var structType = typeof(TStruct);
                if (!delegateMap.TryGetValue(structType, out del)) {
                    var genericMethod = cachedRoutingMethod.MakeGenericMethod(structType);
                    var genericType = typeof(UnboxedInterfaceAction<TStruct, T1>);
                    del = Delegate.CreateDelegate(genericType, genericMethod);
                    delegateMap[structType] = del;
                }
                return (UnboxedInterfaceAction<TStruct, T1>)del;
            }

            public abstract Type InterfaceType { get; }
            public abstract MethodInfo RoutingMethod { get; }
        }
        #endregion
    }
}
