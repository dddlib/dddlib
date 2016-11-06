// <copyright file="DateTimeConverter.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

#if DDDLIB
namespace dddlib.Sdk
#elif DISPATCHER
namespace dddlib.Persistence.EventDispatcher.Sdk
#elif PROJECTIONS
namespace dddlib.Projections.Sdk
#endif
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Web.Script.Serialization;

    /// <summary>
    /// Represents the <see cref="System.DateTime"/> converter for JavaScript serialization.
    /// </summary>
    /// <seealso cref="System.Web.Script.Serialization.JavaScriptConverter" />
    //// LINK (Cameron): http://blog.calyptus.eu/seb/2011/12/custom-datetime-json-serialization/
    public class DateTimeConverter : JavaScriptConverter
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        /// <summary>
        /// Gets a collection of the supported types.
        /// </summary>
        /// <value>The supported types.</value>
        public override IEnumerable<Type> SupportedTypes
        {
            get { return new[] { typeof(DateTime) }; }
        }

        /// <summary>
        /// Converts the provided dictionary into an object of the specified type.
        /// </summary>
        /// <param name="dictionary">An <see cref="T:System.Collections.Generic.IDictionary`2" /> instance of property data stored as name/value pairs.</param>
        /// <param name="type">The type of the resulting object.</param>
        /// <param name="serializer">The <see cref="T:System.Web.Script.Serialization.JavaScriptSerializer" /> instance.</param>
        /// <returns>The deserialized object.</returns>
        public override object Deserialize(IDictionary<string, object> dictionary, Type type, JavaScriptSerializer serializer)
        {
            return Serializer.ConvertToType(dictionary, type);
        }

        /// <summary>
        /// Builds a dictionary of name/value pairs.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="serializer">The object that is responsible for the serialization.</param>
        /// <returns>An object that contains key/value pairs that represent the object’s data.</returns>
        public override IDictionary<string, object> Serialize(object obj, JavaScriptSerializer serializer)
        {
            return obj is DateTime
                ? new DateTimeString((DateTime)obj)
                : null;
        }

        private class DateTimeString : Uri, IDictionary<string, object>
        {
            public DateTimeString(DateTime value)
              : base(value.ToUniversalTime().ToString("O"), UriKind.Relative)
            {
            }

            ICollection<string> IDictionary<string, object>.Keys
            {
                get { throw new NotImplementedException(); }
            }

            ICollection<object> IDictionary<string, object>.Values
            {
                get { throw new NotImplementedException(); }
            }

            int ICollection<KeyValuePair<string, object>>.Count
            {
                get { throw new NotImplementedException(); }
            }

            bool ICollection<KeyValuePair<string, object>>.IsReadOnly
            {
                get { throw new NotImplementedException(); }
            }

            object IDictionary<string, object>.this[string key]
            {
                get { throw new NotImplementedException(); }
                set { throw new NotImplementedException(); }
            }

            void IDictionary<string, object>.Add(string key, object value)
            {
                throw new NotImplementedException();
            }

            bool IDictionary<string, object>.ContainsKey(string key)
            {
                throw new NotImplementedException();
            }

            bool IDictionary<string, object>.Remove(string key)
            {
                throw new NotImplementedException();
            }

            bool IDictionary<string, object>.TryGetValue(string key, out object value)
            {
                throw new NotImplementedException();
            }

            void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item)
            {
                throw new NotImplementedException();
            }

            void ICollection<KeyValuePair<string, object>>.Clear()
            {
                throw new NotImplementedException();
            }

            bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item)
            {
                throw new NotImplementedException();
            }

            void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item)
            {
                throw new NotImplementedException();
            }

            IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
            {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }
        }
    }
}
