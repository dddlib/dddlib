// <copyright file="Mapper.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Runtime
{
    using System;
    using System.Collections.Generic;

    internal class Mapper
    {
        private readonly Dictionary<Key, object> contents = new Dictionary<Key, object>(new KeyComparer());

        public void AddMap<TSource, TDestination>(Func<TSource, TDestination> map)
        {
            var key = new Key { Source = typeof(TSource), Destination = typeof(TDestination), IsAction = false };
            this.contents[key] = map;
        }

        public void AddMap<TSource, TDestination>(Action<TSource, TDestination> map)
        {
            var key = new Key { Source = typeof(TSource), Destination = typeof(TDestination), IsAction = true };
            this.contents[key] = map;
        }

        public Func<TSource, TDestination> GetFuncMap<TSource, TDestination>()
        {
            var key = new Key { Source = typeof(TSource), Destination = typeof(TDestination), IsAction = false };

            object map;
            if (!this.contents.TryGetValue(key, out map))
            {
                throw new RuntimeException("Map doesn't exist.");
            }

            return map as Func<TSource, TDestination>;
        }

        public Action<TSource, TDestination> GetActionMap<TSource, TDestination>()
        {
            var key = new Key { Source = typeof(TSource), Destination = typeof(TDestination), IsAction = true };

            object map;
            if (!this.contents.TryGetValue(key, out map))
            {
                throw new RuntimeException("Map doesn't exist.");
            }

            return map as Action<TSource, TDestination>;
        }

        private class Key
        {
            public Type Source { get; set; }

            public Type Destination { get; set; }

            public bool IsAction { get; set; }
        }

        private class KeyComparer : IEqualityComparer<Key>
        {
            public bool Equals(Key x, Key y)
            {
                return x.Source == y.Source && x.Destination == y.Destination && x.IsAction == y.IsAction;
            }

            public int GetHashCode(Key obj)
            {
                unchecked
                {
                    int hash = 17;
                    hash = (hash * 23) + obj.Source.GetHashCode();
                    hash = (hash * 23) + obj.Destination.GetHashCode();
                    hash = (hash * 23) + obj.IsAction.GetHashCode();
                    return hash;
                }
            }

            private static int GetHashCodeOrDefault<T1>(T1 value)
            {
                return object.Equals(value, default(T1)) ? 0 : value.GetHashCode();
            }
        }
    }
}
