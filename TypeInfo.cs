using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using static Arenas.TypeHandle;

namespace Arenas {
    public unsafe class TypeInfo {
        public Type Type { get; private set; }
        public TypeHandle Handle { get; private set; }
        public int Size { get; private set; }
        public IArenaMethods ArenaContentsMethods { get; private set; }
        public bool IsArenaContents { get { return ArenaContentsMethods != null; } }

        private Func<IntPtr, string> toStringFunc;
        private Func<IntPtr, int> getHashCodeFunc;
        private Func<IntPtr, object> ptrToStructFunc;

        internal TypeInfo(Type type, TypeHandle handle, int size, Func<IntPtr, string> toStringFunc, Func<IntPtr, int> getHashCodeFunc, Func<IntPtr, object> ptrToStructFunc) {
            Type = type;
            Handle = handle;
            Size = size;
            this.toStringFunc = toStringFunc;
            this.getHashCodeFunc = getHashCodeFunc;
            this.ptrToStructFunc = ptrToStructFunc;
        }

        public string ToString(IntPtr instance) {
            return toStringFunc(instance);
        }

        public int GetHashCode(IntPtr instance) {
            return getHashCodeFunc(instance);
        }

        public object PtrToStruct(IntPtr instance) {
            return ptrToStructFunc(instance);
        }

        public bool TryFree(IntPtr instance) {
            if (ArenaContentsMethods == null) return false;
            ArenaContentsMethods.Free(instance);
            return true;
        }

        public bool TrySetArenaID(IntPtr instance, ArenaID id) {
            if (ArenaContentsMethods == null) return false;
            ArenaContentsMethods.SetArenaID(instance, id);
            return true;
        }

        public override string ToString() {
            return $"TypeInfo({Type})";
        }

        #region Static
        private static Dictionary<TypeHandle, TypeInfo> handleToInfo;
        private static object handleLock;
        private static Dictionary<Type, TypeInfo> typeToInfo;
        private static object typeLock;

        static TypeInfo() {
            handleToInfo = new Dictionary<TypeHandle, TypeInfo>();
            handleLock = new object();
            typeToInfo = new Dictionary<Type, TypeInfo>();
            typeLock = new object();
        }

        private static string ToStringFromPtr<T>(IntPtr ptr) where T : unmanaged {
            var inst = (T*)ptr;
            return inst->ToString();
        }

        private static int GetHashCodeFromPtr<T>(IntPtr ptr) where T : unmanaged {
            var inst = (T*)ptr;
            return inst->GetHashCode();
        }

        private static object CloneFromPtr<T>(IntPtr ptr) where T : unmanaged {
            var inst = (T*)ptr;
            return *inst;
        }

        public static TypeInfo GenerateTypeInfo<T>() where T : unmanaged {
            Type type = typeof(T);
            TypeInfo info;

            lock (typeLock) {
                if (!typeToInfo.TryGetValue(type, out info)) {
                    info = new TypeInfo(type, GetTypeHandle(type), sizeof(T), 
                        ToStringFromPtr<T>, GetHashCodeFromPtr<T>, CloneFromPtr<T>);
                    
                    if (typeof(IArenaContents).IsAssignableFrom(type)) {
                        var inst = default(T);
                        if (inst is IArenaContents) {
                            info.ArenaContentsMethods = ((IArenaContents)inst).ArenaMethods;
                        }
                    }

                    typeToInfo[type] = info;
                    handleToInfo[GetTypeHandle(typeof(T))] = info;
                }
            }

            return info;
        }

        public static bool TryGetTypeInfo(TypeHandle handle, out TypeInfo info) {
            lock (handleLock) {
                if (!handleToInfo.TryGetValue(handle, out info)) {
                    return false;
                }
            }
            return true;
        }

        public static TypeInfo GetTypeInfo(TypeHandle handle) {
            TypeInfo result;
            if (!TryGetTypeInfo(handle, out result)) {
                Type type;
                if (!TypeHandle.TryGetTypeFromHandle(handle, out type)) {
                    throw new KeyNotFoundException("No TypeInfo for handle to unknown type");
                }
                else {
                    throw new KeyNotFoundException($"No TypeInfo for handle to type {type}");
                }
            }
            return result;
        }

        public static bool TryGetTypeInfo(Type type, out TypeInfo info) {
            lock (typeLock) {
                if (!typeToInfo.TryGetValue(type, out info)) {
                    return false;
                }
            }
            return true;
        }

        public static TypeInfo GetTypeInfo(Type type) {
            TypeInfo result;
            if (!TryGetTypeInfo(type, out result)) {
                throw new KeyNotFoundException($"No TypeInfo for type {type}");
            }
            return result;
        }
        #endregion
    }
}
