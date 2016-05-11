// <copyright file="CustomDataReader.cs" company="dddlib contributors">
//  Copyright (c) dddlib contributors. All rights reserved.
// </copyright>

namespace dddlib.Persistence.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Web.Script.Serialization;
    using dddlib.Sdk;

    internal sealed class CustomDataReader : IDataReader, IDisposable, IDataRecord
    {
        // NOTE (Cameron): This is nonsense and should be moved out of here.
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();
        private static readonly Dictionary<string, int> OrdinalLookup = new Dictionary<string, int>
        {
            { "Index", 0 },
            { "PayloadTypeName", 1 },
            { "Metadata", 2 },
            { "Payload", 3 },
        };

        private readonly IEnumerator<object> dataEnumerator = null;
        private readonly string metadata;

        private int index;
        private bool isDisposed;

        static CustomDataReader()
        {
            Serializer.RegisterConverters(new[] { new DateTimeConverter() });
        }

        public CustomDataReader(IEnumerable<object> events, object metadata)
        {
            Guard.Against.Null(() => events);

            this.dataEnumerator = events.GetEnumerator();
            this.metadata = Serializer.Serialize(metadata);
        }

        public int FieldCount
        {
            get { return 4; }
        }

        public int Depth
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool IsClosed
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public int RecordsAffected
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public object this[string name]
        {
            get { throw new NotImplementedException(); }
        }

        public object this[int i]
        {
            get { throw new NotImplementedException(); }
        }

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.isDisposed = true;

            this.dataEnumerator.Dispose();
        }

        public string GetName(int i)
        {
            throw new NotImplementedException();
        }

        public string GetDataTypeName(int i)
        {
            throw new NotImplementedException();
        }

        public Type GetFieldType(int i)
        {
            throw new NotImplementedException();
        }

        public object GetValue(int i)
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            switch (i)
            {
                case 0:
                    return this.index;

                case 1:
                    return this.dataEnumerator.Current.GetType().GetSerializedName();

                case 2:
                    return this.metadata;

                case 3:
                    return Serializer.Serialize(this.dataEnumerator.Current);
            }

            throw new ArgumentOutOfRangeException(Guard.Expression.Parse(() => i), i, "Column ordinal out of range.");
        }

        public int GetValues(object[] values)
        {
            throw new NotImplementedException();
        }

        public int GetOrdinal(string name)
        {
            throw new NotImplementedException();
        }

        public bool GetBoolean(int i)
        {
            throw new NotImplementedException();
        }

        public byte GetByte(int i)
        {
            throw new NotImplementedException();
        }

        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
        {
            throw new NotImplementedException();
        }

        public char GetChar(int i)
        {
            throw new NotImplementedException();
        }

        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
        {
            throw new NotImplementedException();
        }

        public Guid GetGuid(int i)
        {
            throw new NotImplementedException();
        }

        public short GetInt16(int i)
        {
            throw new NotImplementedException();
        }

        public int GetInt32(int i)
        {
            throw new NotImplementedException();
        }

        public long GetInt64(int i)
        {
            throw new NotImplementedException();
        }

        public float GetFloat(int i)
        {
            throw new NotImplementedException();
        }

        public double GetDouble(int i)
        {
            throw new NotImplementedException();
        }

        public string GetString(int i)
        {
            throw new NotImplementedException();
        }

        public decimal GetDecimal(int i)
        {
            throw new NotImplementedException();
        }

        public DateTime GetDateTime(int i)
        {
            throw new NotImplementedException();
        }

        public IDataReader GetData(int i)
        {
            throw new NotImplementedException();
        }

        public bool IsDBNull(int i)
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        public DataTable GetSchemaTable()
        {
            throw new NotImplementedException();
        }

        public bool NextResult()
        {
            throw new NotImplementedException();
        }

        public bool Read()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }

            this.index += 1;

            return this.dataEnumerator.MoveNext();
        }

        private class Data
        {
            public string Payload { get; set; }
        }
    }
}
