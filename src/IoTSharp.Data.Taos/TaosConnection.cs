// Copyright (c)  maikebing All rights reserved.
//// Licensed under the MIT License, See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using TDengineDriver;
using System.Linq;
using IoTSharp.Data.Taos.Driver;
using System.Collections;
using IoTSharp.Data.Taos.Protocols;
using IoTSharp.Data.Taos.Protocols.TDRESTful;
using IoTSharp.Data.Taos.Protocols.TDWebSocket;
#if NETCOREAPP
using System.Text.Json.Nodes;
#endif
namespace IoTSharp.Data.Taos
{
    /// <summary>
    ///     Represents a connection to a Taos database.
    /// </summary>
    public partial class TaosConnection : DbConnection
    {

        private readonly IList<WeakReference<TaosCommand>> _commands = new List<WeakReference<TaosCommand>>();

        private string _connectionString;
        private ConnectionState _state;
        internal ITaosProtocol taos;
        private bool disposed = false;


        public TaosConnection() : this(string.Empty)
        {

        }
        public TaosConnection(string connectionString) : this(connectionString, string.Empty)
        {

        }
        public TaosConnection(string connectionString, string configdir) : this(connectionString, configdir, 60, string.Empty, string.Empty, string.Empty)
        {

        }

        /// <summary>
        /// 初始化链接
        /// </summary>
        /// <param name="connectionString">连接字符串</param>
        /// <param name="configdir">配置目录 顺序 configdir > BaseDirectory > CurrentDirectory > "/etc/taos" || "C:/TDengine/cfg" </param>
        /// <param name="shell_activity_timer">Shell 活动定时器</param>
        /// <param name="locale">区域 'en_US.UTF-8'</param>
        /// <param name="charset">字符集 'UTF-8'</param>
        /// <param name="timezone">时区 例如  'Asia/Shanghai' </param>
        public TaosConnection(string connectionString, string configdir, int shell_activity_timer, string locale, string charset, string timezone)
        {
            if (!string.IsNullOrEmpty(connectionString) && !string.IsNullOrWhiteSpace(connectionString))
            {
                ConnectionStringBuilder = new TaosConnectionStringBuilder(connectionString);
                ConnectionString = connectionString;
            }
            switch (ConnectionStringBuilder?.Protocol)
            {
                case TaosConnectionStringBuilder.Protocol_WebSocket:
                    taos = new TaosWebSocket();
                    break;
                case TaosConnectionStringBuilder.Protocol_RESTful:
                    taos = new TaosRESTful();
                    break;
                case TaosConnectionStringBuilder.Protocol_Native:
                default:
                    taos = new TaosNative();
                    break;
            }
            taos.InitTaos(configdir, shell_activity_timer, locale, charset);
        }


        /// <summary>
        ///     Gets or sets a string used to open the connection.
        /// </summary>
        /// <value>A string used to open the connection.</value>
        /// <seealso cref="TaosConnectionStringBuilder" />
        public override string ConnectionString
        {
            get => _connectionString;
            set
            {
                _connectionString = value;
                ConnectionStringBuilder = new TaosConnectionStringBuilder(value);
            }
        }

        internal TaosConnectionStringBuilder ConnectionStringBuilder { get; set; }

        public override int ConnectionTimeout => ConnectionStringBuilder?.ConnectionTimeout ?? 0;


        /// <summary>
        ///     Gets the path to the database file. Will be absolute for open connections.
        /// </summary>
        /// <value>The path to the database file.</value>
        public override string DataSource
        {
            get
            {
                string dataSource = null;

                return dataSource ?? (ConnectionStringBuilder?.DataSource);
            }
        }

        /// <summary>
        ///     Gets or sets the default <see cref="TaosCommand.CommandTimeout"/> value for commands created using
        ///     this connection. This is also used for internal commands in methods like
        ///     <see cref="BeginTransaction()"/>.
        /// </summary>
        /// <value>The default <see cref="TaosCommand.CommandTimeout"/> value</value>
        public virtual int DefaultTimeout { get; set; } = 60;


        string _version = string.Empty;
        /// <summary>
        ///     Gets the version of Taos used by the connection.
        /// </summary>
        /// <value>The version of Taos used by the connection.</value>
        public override string ServerVersion
        {
            get
            {
                if (string.IsNullOrEmpty(_version))
                {
                    _version = taos.GetServerVersion();
                }
                return _version;
            }
        }
        string _client_version = string.Empty;
        public string ClientVersion
        {
            get
            {
                if (string.IsNullOrEmpty(_client_version))
                {
                    _client_version = taos.GetClientVersion();
                }
                return _client_version;
            }
        }
        /// <summary>
        ///     Gets the current state of the connection.
        /// </summary>
        /// <value>The current state of the connection.</value>
        public override ConnectionState State
            => _state;

        /// <summary>
        ///     Gets the <see cref="DbProviderFactory" /> for this connection.
        /// </summary>
        /// <value>The <see cref="DbProviderFactory" />.</value>
        protected override DbProviderFactory DbProviderFactory
            => TaosFactory.Instance;

        /// <summary>
        ///     Gets or sets the transaction currently being used by the connection, or null if none.
        /// </summary>
        /// <value>The transaction currently being used by the connection.</value>
        protected internal virtual TaosTransaction Transaction { get; set; }

        public override string Database => ConnectionStringBuilder?.DataBase;
        public int PoolSize => ConnectionStringBuilder?.PoolSize ?? 0;


        private void SetState(ConnectionState value)
        {
            var originalState = _state;
            if (originalState != value)
            {
                _state = value;
                OnStateChange(new StateChangeEventArgs(originalState, value));
            }
        }

        /// <summary>
        ///     Opens a connection to the database using the value of <see cref="ConnectionString" />. If
        ///     <c>Mode=ReadWriteCreate</c> is used (the default) the file is created, if it doesn't already exist.
        /// </summary>
        /// <exception cref="TaosException">A Taos error occurs while opening the connection.</exception>
        public override void Open()
        {
            if (State == ConnectionState.Open)
            {
                return;
            }

            var result = taos.Open(ConnectionStringBuilder);

            if (result)
            {
                SetState(ConnectionState.Open);
                this.ChangeDatabase(ConnectionStringBuilder?.DataBase);
            }
        }

        /// <summary>
        ///     Closes the connection to the database. Open transactions are rolled back.
        /// </summary>
        public override void Close()
        {

            //Debug.Write($"TaosConnection {GetHashCode()} Close");

            if (State != ConnectionState.Closed)
            {
                taos.Close(ConnectionStringBuilder);
            }
            Transaction?.Dispose();
            _nowdatabase = string.Empty;

            SetState(ConnectionState.Closed);


        }

        /// <summary>
        ///     Releases any resources used by the connection and closes it.
        /// </summary>
        /// <param name="disposing">
        ///     true to release managed and unmanaged resources; false to release only unmanaged resources.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    Close();

                    if (_commands != null && _commands.Count > 0)
                    {
                        foreach (var reference in _commands)
                        {
                            if (reference.TryGetTarget(out var command))
                            {
                                command.Dispose();
                            }
                        }

                        _commands.Clear();
                    }
                }
                base.Dispose(disposing);
                disposed = true;
            }
        }

        /// <summary>
        ///     Creates a new command associated with the connection.
        /// </summary>
        /// <returns>The new command.</returns>
        /// <remarks>
        ///     The command's <seealso cref="TaosCommand.Transaction" /> property will also be set to the current
        ///     transaction.
        /// </remarks>
        public new virtual TaosCommand CreateCommand()
            => new() { Connection = this, CommandTimeout = DefaultTimeout, Transaction = Transaction };
        public virtual TaosCommand CreateCommand(string commandtext)
          => new() { Connection = this, CommandText = commandtext, CommandTimeout = DefaultTimeout, Transaction = Transaction };

        /// <summary>
        ///     Creates a new command associated with the connection.
        /// </summary>
        /// <returns>The new command.</returns>
        protected override DbCommand CreateDbCommand()
            => CreateCommand();

        internal void AddCommand(TaosCommand command)
            => _commands.Add(new WeakReference<TaosCommand>(command));

        internal void RemoveCommand(TaosCommand command)
        {
            for (var i = _commands.Count - 1; i >= 0; i--)
            {
                if (!_commands[i].TryGetTarget(out var item)
                    || item == command)
                {
                    _commands.RemoveAt(i);
                }
            }
        }

        /// <summary>
        ///     Create custom collation.
        /// </summary>
        /// <param name="name">Name of the collation.</param>
        /// <param name="comparison">Method that compares two strings.</param>
        public virtual void CreateCollation(string name, Comparison<string> comparison)
            => CreateCollation(name, null, comparison != null ? (_, s1, s2) => comparison(s1, s2) : (Func<object, string, string, int>)null);

        /// <summary>
        ///     Create custom collation.
        /// </summary>
        /// <typeparam name="T">The type of the state object.</typeparam>
        /// <param name="name">Name of the collation.</param>
        /// <param name="state">State object passed to each invocation of the collation.</param>
        /// <param name="comparison">Method that compares two strings, using additional state.</param>
        public virtual void CreateCollation<T>(string name, T state, Func<T, string, string, int> comparison)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (State != ConnectionState.Open)
            {
                throw new InvalidOperationException($"CallRequiresOpenConnection{nameof(CreateCollation)}");
            }


        }

        /// <summary>
        ///     Begins a transaction on the connection.
        /// </summary>
        /// <returns>The transaction.</returns>
        public new virtual TaosTransaction BeginTransaction()
            => BeginTransaction(IsolationLevel.Unspecified);

        /// <summary>
        ///     Begins a transaction on the connection.
        /// </summary>
        /// <param name="isolationLevel">The isolation level of the transaction.</param>
        /// <returns>The transaction.</returns>
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => BeginTransaction(isolationLevel);

        /// <summary>
        ///     Begins a transaction on the connection.
        /// </summary>
        /// <param name="isolationLevel">The isolation level of the transaction.</param>
        /// <returns>The transaction.</returns>
        public new virtual TaosTransaction BeginTransaction(IsolationLevel isolationLevel)
        {
            if (State != ConnectionState.Open)
            {
                throw new InvalidOperationException($"CallRequiresOpenConnection{nameof(BeginTransaction)}");
            }
            if (Transaction != null)
            {
                throw new InvalidOperationException($"ParallelTransactionsNotSupported");
            }

            return Transaction = new TaosTransaction(this, isolationLevel);
        }
        internal string _nowdatabase = string.Empty;

        internal bool SelectedDataBase => _nowdatabase != string.Empty;


        /// <summary>
        ///    更改当前数据库.  
        /// </summary>
        /// <param name="databaseName">要使用的数据库名称</param>
        /// <remarks>当使用WebSocket协议时会自动重新连接数据库。</remarks>
        public override void ChangeDatabase(string databaseName)
        {
            var result = taos.ChangeDatabase(databaseName);
            if (result)
            {
                _nowdatabase = databaseName;
            }
        }
        /// <summary>
        /// 判断数据库是否存在
        /// </summary>
        /// <param name="databaseName">指定的数据库名称</param>
        /// <returns></returns>
        public bool DatabaseExists(string databaseName)
        {
            var _sql = "SHOW DATABASES";
            var ds = CreateCommand(_sql).ExecuteReader().ToObject<DatabaseSchema>();
            return (bool)(ds?.Any(d => d.name == databaseName));
        }
        /// <summary>
        /// 使用 TSDB_SML_LINE_PROTOCOL 协议
        /// </summary>
        /// <param name="lines">
        /// 示例:
        ///     "meters,location=Beijing.Haidian,groupid=2 current=11.8,voltage=221,phase=0.28 1648432611249",
        ///     "meters,location=Beijing.Haidian,groupid=2 current=13.4,voltage=223,phase=0.29 1648432611250",
        ///     "meters,location=Beijing.Haidian,groupid=3 current=10.8,voltage=223,phase=0.29 1648432611249",
        ///     "meters,location=Beijing.Haidian,groupid=3 current=11.3,voltage=221,phase=0.35 1648432611250"
        /// </param>
        /// <param name="precision"></param>
        /// <returns>返回数量</returns>
        public int ExecuteLineBulkInsert(string[] lines, TDengineSchemalessPrecision precision = TDengineSchemalessPrecision.TSDB_SML_TIMESTAMP_MILLI_SECONDS) =>
          taos.ExecuteBulkInsert(lines, TDengineSchemalessProtocol.TSDB_SML_LINE_PROTOCOL, precision);

#if NETCOREAPP
        /// <summary>
        /// 使用Schemales的 TSDB_SML_LINE_PROTOCOL 将 <paramref name="data"/>插入TDengine
        /// </summary>
        /// <returns>返回数量</returns>
        public int ExecuteBulkInsert(RecordData data)
        {
            return ExecuteBulkInsert(new RecordData[] { data }, TDengineSchemalessPrecision.TSDB_SML_TIMESTAMP_NOT_CONFIGURED, null);
        }
        /// <summary>
        /// 使用Schemales的 TSDB_SML_LINE_PROTOCOL 将 <paramref name="data"/>插入TDengine
        /// </summary>
        /// <returns>返回数量</returns>
        public int ExecuteBulkInsert(IEnumerable<RecordData> data)
        {
            return ExecuteBulkInsert(data, TDengineSchemalessPrecision.TSDB_SML_TIMESTAMP_NOT_CONFIGURED, null);
        }
        /// <summary>
        /// 使用Schemales的 TSDB_SML_LINE_PROTOCOL 将 <paramref name="data"/>插入TDengine
        /// </summary>
        /// <param name="data">使用InfluxDB的RecordData方式合成数据</param>
        /// <returns>返回数量</returns>
        public int ExecuteBulkInsert(IEnumerable<RecordData> data, RecordSettings settings)
        {
            return ExecuteBulkInsert(data, TDengineSchemalessPrecision.TSDB_SML_TIMESTAMP_NOT_CONFIGURED, settings);
        }
        /// <summary>
        /// 使用Schemales的 TSDB_SML_LINE_PROTOCOL 将 <paramref name="data"/>插入TDengine
        /// </summary>
        /// <param name="data">使用InfluxDB的RecordData方式合成数据</param>
        /// <param name="precision">时间精度 </param>
        /// <returns>返回数量</returns>
        public int ExecuteBulkInsert(IEnumerable<RecordData> data, TDengineSchemalessPrecision precision)
        {
            return ExecuteBulkInsert(data, precision, null);
        }
        /// <summary>
        /// 使用Schemales的 TSDB_SML_LINE_PROTOCOL 将 <paramref name="data"/>插入TDengine
        /// </summary>
        /// <param name="data">使用InfluxDB的RecordData方式合成数据</param>
        /// <param name="precision">时间精度 </param>
        /// <param name="settings">RecordSettings配置，请参考 InfluxDB的用法。</param>
        /// <returns>返回数量</returns>
        public int ExecuteBulkInsert(IEnumerable<RecordData> data, TDengineSchemalessPrecision precision, RecordSettings settings)
        {
            Arguments.CheckNotEmpty(data, "ExecuteBulkInsert(IEnumerable<RecordData> data)");
            if (precision == TDengineSchemalessPrecision.TSDB_SML_TIMESTAMP_NOT_CONFIGURED)
            {
                switch (data.First().Precision)
                {
                    case TimePrecision.Ms://毫秒
                        precision = TDengineSchemalessPrecision.TSDB_SML_TIMESTAMP_MILLI_SECONDS;
                        break;
                    case TimePrecision.S://秒
                        precision = TDengineSchemalessPrecision.TSDB_SML_TIMESTAMP_SECONDS;
                        break;
                    case TimePrecision.Us://微秒
                        precision = TDengineSchemalessPrecision.TSDB_SML_TIMESTAMP_MICRO_SECONDS;
                        break;
                    case TimePrecision.Ns: //纳秒
                        precision = TDengineSchemalessPrecision.TSDB_SML_TIMESTAMP_NANO_SECONDS;
                        break;
                    default:
                        precision = TDengineSchemalessPrecision.TSDB_SML_TIMESTAMP_MILLI_SECONDS;
                        break;
                }
            }
            var lines = data.Select(rd => rd.ToLineProtocol(settings)).ToArray();
            return taos.ExecuteBulkInsert(lines, TDengineSchemalessProtocol.TSDB_SML_LINE_PROTOCOL, precision);
        }
#endif
        /// <summary>
        /// 使用Schemales的 TSDB_SML_JSON_PROTOCOL 将<paramref name="array"/>插入TDengine
        /// </summary>
        /// <param name="array">对象数组， 在.Net6/7中会使用 System.Text.Json序列化，其他情况下使用Newtonsoft.Json序列化</param>
        /// <param name="precision">时间精度 默认值为 TSDB_SML_TIMESTAMP_MILLI_SECONDS</param>
        /// <returns>返回数量</returns>
        public int ExecuteBulkInsert<T>(IEnumerable<T> array, TDengineSchemalessPrecision precision = TDengineSchemalessPrecision.TSDB_SML_TIMESTAMP_MILLI_SECONDS)
        {
#if NETCOREAPP
            var lines = array.Select(x => System.Text.Json.JsonSerializer.Serialize(x)).ToArray();
#else
            var lines = array.Select(x => Newtonsoft.Json.JsonConvert.SerializeObject(x)).ToArray();
#endif
            return taos.ExecuteBulkInsert(lines, TDengineSchemalessProtocol.TSDB_SML_JSON_PROTOCOL, precision);
        }

        /// <summary>
        /// 使用Schemales的 TSDB_SML_JSON_PROTOCOL 将<paramref name="array"/>插入TDengine
        /// </summary>
        /// <param name="array"> Json数组  </param>
        /// <param name="precision">时间精度 默认值为 TSDB_SML_TIMESTAMP_MILLI_SECONDS</param>
        /// <returns>返回数量</returns>
        public int ExecuteBulkInsert(JArray array, TDengineSchemalessPrecision precision = TDengineSchemalessPrecision.TSDB_SML_TIMESTAMP_MILLI_SECONDS)
        {
            var lines = array.Children().Select(x => x.ToString()).ToArray();
            return taos.ExecuteBulkInsert(lines, TDengineSchemalessProtocol.TSDB_SML_JSON_PROTOCOL, precision);
        }
#if NETCOREAPP
        /// <summary>
        /// 使用Schemales的 TSDB_SML_JSON_PROTOCOL 将<paramref name="array"/>插入TDengine
        /// </summary>
        /// <param name="array"> Json数组  </param>
        /// <param name="precision">时间精度，默认值为 TSDB_SML_TIMESTAMP_MILLI_SECONDS</param>
        /// <returns>返回数量</returns>
        public int ExecuteBulkInsert(JsonArray array, TDengineSchemalessPrecision precision = TDengineSchemalessPrecision.TSDB_SML_TIMESTAMP_MILLI_SECONDS)
        {
            var lines = array.Select(x => x.ToString()).ToArray();
            return taos.ExecuteBulkInsert(lines, TDengineSchemalessProtocol.TSDB_SML_JSON_PROTOCOL, precision);
        }
#endif
        /// <summary>
        /// 使用Schemales的 TSDB_SML_TELNET_PROTOCOL 将数据插入TDengine
        /// </summary>
        /// <param name="array">字符串数据</param>
        /// <param name="precision">时间精度</param>
        /// <returns>返回数量</returns>
        public int ExecuteTelnetBulkInsert(string[] array, TDengineSchemalessPrecision precision = TDengineSchemalessPrecision.TSDB_SML_TIMESTAMP_NOT_CONFIGURED)
        {
            return taos.ExecuteBulkInsert(array, TDengineSchemalessProtocol.TSDB_SML_TELNET_PROTOCOL, precision);
        }





        private class AggregateContext<T>
        {
            public AggregateContext(T seed)
                => Accumulate = seed;

            public T Accumulate { get; set; }
            public Exception Exception { get; set; }
        }
    }
}
