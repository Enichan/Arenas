using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Arenas {
    public class ManagedObjectPool<T> where T : class {
        public delegate T CreateInstanceDelegate();
        public delegate bool ResetInstanceDelegate(T instance);

        private int maxStoredInstances;
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
            if (resetInstance(instance) && (maxStoredInstances == 0 || freeList.Count < MaxStoredInstances)) {
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

        public int MaxStoredInstances {
            get { return maxStoredInstances; }
            set {
                maxStoredInstances = Math.Max(0, value);

                if (maxStoredInstances > 0) {
                    var popCount = freeList.Count - maxStoredInstances;
                    T val;

                    // alas ConcurrentStack does not give us a way to pop N objects off the stack
                    // without first allocating an array for them, so this is the best allocation
                    // free method that can be used here
                    while (popCount > 0) {
                        freeList.TryPop(out val);
                        popCount--;
                    }
                }
            }
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
