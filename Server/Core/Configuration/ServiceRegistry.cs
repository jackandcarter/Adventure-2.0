using System;
using System.Collections.Generic;

namespace Adventure.Server.Core.Configuration
{
    public class ServiceRegistry
    {
        private readonly Dictionary<Type, Func<ServiceRegistry, object>> factories = new();
        private readonly Dictionary<Type, object> singletons = new();

        public ServiceRegistry AddInstance<TService>(TService instance) where TService : class
        {
            singletons[typeof(TService)] = instance!;
            return this;
        }

        public ServiceRegistry AddSingleton<TService>(Func<ServiceRegistry, TService> factory) where TService : class
        {
            factories[typeof(TService)] = sp => factory(sp)!;
            return this;
        }

        public TService Get<TService>() where TService : class
        {
            var type = typeof(TService);
            if (singletons.TryGetValue(type, out var instance))
            {
                return (TService)instance;
            }

            if (!factories.TryGetValue(type, out var factory))
            {
                throw new InvalidOperationException($"Service of type {type.Name} is not registered.");
            }

            var created = factory(this);
            singletons[type] = created;
            return (TService)created;
        }

        public bool TryGet<TService>(out TService? service) where TService : class
        {
            var type = typeof(TService);
            if (singletons.TryGetValue(type, out var instance))
            {
                service = (TService)instance;
                return true;
            }

            if (factories.TryGetValue(type, out var factory))
            {
                var created = factory(this);
                singletons[type] = created;
                service = (TService)created;
                return true;
            }

            service = default;
            return false;
        }
    }
}
