using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Arenas {
    public class ManagedObjectPool<T> where T : class {
        public delegate T CreateInstanceDelegate();
        public delegate bool ResetInstanceDelegate(T instance);

        private ConcurrentStack<T> freeList;
        protected CreateInstanceDelegate createInstance;
        protected ResetInstanceDelegate resetInstance;

        public ManagedObjectPool(CreateInstanceDelegate createInstance, ResetInstanceDelegate resetInstance) 
            : this() {
            this.createInstance = createInstance;
            this.resetInstance = resetInstance;
        }

        protected ManagedObjectPool() {
            freeList = new ConcurrentStack<T>();
        }

        public void EnsureCount(int count) {
            while (freeList.Count < count) {
                freeList.Push(createInstance());
            }
        }

        public void Return(T instance) {
            if (resetInstance(instance)) {
                freeList.Push(instance);
            }
        }

        public T Get() {
            if (!freeList.TryPop(out var instance)) {
                return createInstance();
            }
            return instance;
        }

        public PooledAutoReturn<T> Borrow(out T instance) {
            instance = Get();
            return new PooledAutoReturn<T>(this, instance);
        }
    }

    public readonly struct PooledAutoReturn<T> : IDisposable where T : class {
        public readonly ManagedObjectPool<T> Pool;
        public readonly T Value;

        public PooledAutoReturn(ManagedObjectPool<T> pool, T value) {
            Pool = pool ?? throw new ArgumentNullException(nameof(pool));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public void Dispose() {
            if (Value == null || Pool == null) {
                return;
            }
            Pool.Return(Value);
        }
    }
}
