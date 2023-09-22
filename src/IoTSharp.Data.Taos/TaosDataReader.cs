// Copyright (c)  maikebing All rights reserved.
//// Licensed under the MIT License, See License.txt in the project root for license information.

using IoTSharp.Data.Taos.Protocols;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using TDengineDriver;

namespace IoTSharp.Data.Taos
{
    /// <summary>
    ///     Provides methods for reading the result of a command executed against a Taos database.
    /// </summary>
    public class TaosDataReader : DbDataReader
    {
        private readonly TaosCommand _command;
        private bool _hasRows;
        private readonly int _recordsAffected;
        private bool _closed;
        private bool _closeConnection;
        private int _fieldCount;

        internal ITaosProtocol taos => _command?._connection?.taos;
        ITaosContext _context;
        internal TaosDataReader(TaosCommand taosCommand, ITaosContext context)
        {
            _command = taosCommand;
            _closeConnection = context.CloseConnection;
            _fieldCount = context.FieldCount;
            _hasRows = context.AffectRows > 0;
            _recordsAffected = context.AffectRows;
            _closed = _closeConnection;
            _context = context;
        }



        /// <summary>
        ///     Gets the depth of nesting for the current row. Always zero.
        /// </summary>
        /// <value>The depth of nesting for the current row.</value>
        public override int Depth => 0;

        /// <summary>
        ///     Gets the number of columns in the current row.
        /// </summary>
        /// <value>The number of columns in the current row.</value>
        public override int FieldCount => _fieldCount;

        /// <summary>
        ///     Gets a value indicating whether the data reader contains any rows.
        /// </summary>
        /// <value>A value indicating whether the data reader contains any rows.</value>
        public override bool HasRows
            => _hasRows;

        public TaosException LastTaosException => _context.LastException();

        int ErrorNo => _context.GetErrorNo();
        /// <summary>
        ///     Gets a value indicating whether the data reader is closed.
        /// </summary>
        /// <value>A value indicating whether the data reader is closed.</value>
        public override bool IsClosed
            => _closed;

        /// <summary>
        ///     Gets the number of rows inserted, updated, or deleted. -1 for SELECT statements.
        /// </summary>
        /// <value>The number of rows inserted, updated, or deleted.</value>
        public override int RecordsAffected
        {
            get
            {
                return _recordsAffected; ;
            }
        }

        /// <summary>
        ///     Gets the value of the specified column.
        /// </summary>
        /// <param name="name">The name of the column. The value is case-sensitive.</param>
        /// <returns>The value.</returns>
        public override object this[string name]
            => this[GetOrdinal(name)];

        /// <summary>
        ///     Gets the value of the specified column.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value.</returns>
        public override object this[int ordinal]
            => GetValue(ordinal);

        /// <summary>
        ///     Gets an enumerator that can be used to iterate through the rows in the data reader.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public override IEnumerator GetEnumerator()
            => new DbEnumerator(this, closeReader: false);


        /// <summary>
        ///     Advances to the next row in the result set.
        /// </summary>
        /// <returns>true if there are more rows; otherwise, false.</returns>
        public override bool Read()
        {
            if (_closed)
            {
                throw new InvalidOperationException($"DataReaderClosed{nameof(Read)}");
            }
            return _context.Read();
        }

        /// <summary>
        ///     Advances to the next result set for batched statements.
        /// </summary>
        /// <returns>true if there are more result sets; otherwise, false.</returns>
        public override bool NextResult()
        {
            return Read();
        }

        /// <summary>
        ///     Closes the data reader.
        /// </summary>
        public override void Close()
            => Dispose(true);


        internal event EventHandler OnDispose;
        /// <summary>
        ///     Releases any resources used by the data reader and closes it.
        /// </summary>
        /// <param name="disposing">
        ///     true to release managed and unmanaged resources; false to release only unmanaged resources.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }
            _command.DataReader = null;

            _closed = true;
            _context?.Dispose();
            _context = null;
            OnDispose?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        ///     Gets the name of the specified column.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The name of the column.</returns>
        public override string GetName(int ordinal)
        {
            return _context.GetName(ordinal);
        }

        /// <summary>
        ///     Gets the ordinal of the specified column.
        /// </summary>
        /// <param name="name">The name of the column.</param>
        /// <returns>The zero-based column ordinal.</returns>
        public override int GetOrdinal(string name) => _context.GetOrdinal(name);

        public override string GetDataTypeName(int ordinal)
        {
            return GetFieldType(ordinal).Name;
        }

        /// <summary>
        ///     Gets the data type of the specified column.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The data type of the column.</returns>
        public override Type GetFieldType(int ordinal) => _context.GetFieldType(ordinal);

        public int GetFieldSize(int ordinal) => _context.GetFieldSize(ordinal);
        /// <summary>
        ///     Gets a value indicating whether the specified column is <see cref="DBNull" />.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>true if the specified column is <see cref="DBNull" />; otherwise, false.</returns>
        public override bool IsDBNull(int ordinal)
                => GetValue(ordinal) == DBNull.Value;

        /// <summary>
        ///     Gets the value of the specified column as a <see cref="bool" />.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value of the column.</returns>
        public override bool GetBoolean(int ordinal) => _context.GetBoolean(ordinal);

        /// <summary>
        ///     Gets the value of the specified column as a <see cref="byte" />.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value of the column.</returns>
        public override byte GetByte(int ordinal) => _context.GetByte(ordinal);

        /// <summary>
        ///     Gets the value of the specified column as a <see cref="char" />.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value of the column.</returns>
        public override char GetChar(int ordinal) => GetFieldValue<char>(ordinal);

        /// <summary>
        ///     Gets the value of the specified column as a <see cref="DateTime" />.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value of the column.</returns>
        public override DateTime GetDateTime(int ordinal)
        {
            return _context.GetDataTime(ordinal);
        }

        /// <summary>
        ///     Gets the value of the specified column as a <see cref="DateTimeOffset" />.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value of the column.</returns>
        public virtual DateTimeOffset GetDateTimeOffset(int ordinal)
        {
            return GetDateTime(ordinal);
        }

        /// <summary>
        ///     Gets the value of the specified column as a <see cref="TimeSpan" />.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value of the column.</returns>
        public virtual TimeSpan GetTimeSpan(int ordinal) => _context.GetTimeSpan(ordinal);


        /// <summary>
        ///     Gets the value of the specified column as a <see cref="decimal" />.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value of the column.</returns>
        public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal));

        /// <summary>
        ///     Gets the value of the specified column as a <see cref="double" />.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value of the column.</returns>
        public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal));

        /// <summary>
        ///     Gets the value of the specified column as a <see cref="float" />.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value of the column.</returns>
        public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal));

        /// <summary>
        ///     Gets the value of the specified column as a <see cref="Guid" />.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value of the column.</returns>
        public override Guid GetGuid(int ordinal) => GetFieldValue<Guid>(ordinal);

        /// <summary>
        ///     Gets the value of the specified column as a <see cref="short" />.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value of the column.</returns>
        public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));

        /// <summary>
        ///     Gets the value of the specified column as a <see cref="ushort" />.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value of the column.</returns>
        public ushort GetUInt16(int ordinal) => Convert.ToUInt16(GetValue(ordinal));

        /// <summary>
        ///     Gets the value of the specified column as a <see cref="int" />.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value of the column.</returns>
        public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));

        /// <summary>
        ///     Gets the value of the specified column as a <see cref="uint" />.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value of the column.</returns>
        public uint GetUInt32(int ordinal) => Convert.ToUInt32(GetValue(ordinal));

        /// <summary>
        ///     Gets the value of the specified column as a <see cref="long" />.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value of the column.</returns>
        public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));

        /// <summary>
        ///     Gets the value of the specified column as a <see cref="ulong" />.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value of the column.</returns>
        public ulong GetUInt64(int ordinal) => Convert.ToUInt64(GetValue(ordinal));

        /// <summary>
        ///     Gets the value of the specified column as a <see cref="string" />.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value of the column.</returns>
        public override string GetString(int ordinal) => Convert.ToString(GetValue(ordinal));
#if NET5_0_OR_GREATER
        public System.Text.Json.JsonDocument GetJsonDocument(int ordinal) => System.Text.Json.JsonDocument.Parse(GetString(ordinal));
#endif 
        public Newtonsoft.Json.Linq.JToken GetJToken(int ordinal) => Newtonsoft.Json.Linq.JToken.Parse(GetString(ordinal));

        /// <summary>
        ///     Reads a stream of bytes from the specified column. Not supported.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <param name="dataOffset">The index from which to begin the read operation.</param>
        /// <param name="buffer">The buffer into which the data is copied.</param>
        /// <param name="bufferOffset">The index to which the data will be copied.</param>
        /// <param name="length">The maximum number of bytes to read.</param>
        /// <returns>The actual number of bytes read.</returns>
        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
                    => _context.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);


        /// <summary>
        ///     Reads a stream of characters from the specified column. Not supported.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <param name="dataOffset">The index from which to begin the read operation.</param>
        /// <param name="buffer">The buffer into which the data is copied.</param>
        /// <param name="bufferOffset">The index to which the data will be copied.</param>
        /// <param name="length">The maximum number of characters to read.</param>
        /// <returns>The actual number of characters read.</returns>
        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
           => throw new NotSupportedException();

        /// <summary>
        ///     Retrieves data as a Stream. If the reader includes rowid (or any of its aliases), a
        ///     <see cref="TaosBlob"/> is returned. Otherwise, the all of the data is read into memory and a
        ///     <see cref="MemoryStream"/> is returned.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The returned object.</returns>
        public override Stream GetStream(int ordinal) => _context.GetStream(ordinal);


        /// <summary>
        ///     Gets the value of the specified column.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value of the column.</returns>
        public override T GetFieldValue<T>(int ordinal) => (T)Convert.ChangeType(GetValue(ordinal), typeof(T));


        /// <summary>
        ///     Gets the value of the specified column.
        /// </summary>
        /// <param name="ordinal">The zero-based column ordinal.</param>
        /// <returns>The value of the column.</returns>
        public override object GetValue(int ordinal) => _context.GetValue(ordinal);





        /// <summary>
        ///     Gets the column values of the current row.
        /// </summary>
        /// <param name="values">An array into which the values are copied.</param>
        /// <returns>The number of values copied into the array.</returns>
        public override int GetValues(object[] values)
        {
            int count = 0;
            for (int i = 0; i < _fieldCount; i++)
            {
                var obj = GetValue(i);
                if (obj != null)
                {
                    values[i] = obj;
                    count++;
                }
            }
            return count;
        }


        /// <summary>
        ///     Returns a System.Data.DataTable that describes the column metadata of the System.Data.Common.DbDataReader.
        /// </summary>
        /// <returns>A System.Data.DataTable that describes the column metadata.</returns>
        public override DataTable GetSchemaTable() => _command.GetSchemaTable(GetName, GetFieldType, GetDataTypeName, GetFieldSize, _fieldCount);

    }
}