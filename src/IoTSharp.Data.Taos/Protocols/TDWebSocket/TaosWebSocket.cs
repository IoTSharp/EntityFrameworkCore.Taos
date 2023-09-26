using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using TDengineDriver;

namespace IoTSharp.Data.Taos.Protocols.TDWebSocket
{
    internal partial class TaosWebSocket : ITaosProtocol
    {
        private ClientWebSocket _ws_client = null;
        private ClientWebSocket _stmt_client = null;
        private ClientWebSocket _schemaless_client = null;
        private string _databaseName;
        private TaosConnectionStringBuilder _builder;

        public bool ChangeDatabase(string databaseName)
        {
            Close(_builder);
            _databaseName = databaseName;
            _builder.DataBase = _databaseName;
            var result = Open(_builder);
            return result;
        }

        public void Close(TaosConnectionStringBuilder connectionStringBuilder)
        {
#if NET46

#else
            _ws_client?.Dispose();
            _stmt_client?.Dispose();
            _schemaless_client?.Dispose();
#endif
        }

        public TaosDataReader ExecuteReader(CommandBehavior behavior, TaosCommand command)
        {
            var _commandText = command._commandText;
            var _connection = command._connection;
            var _parameters = command._parameters;
            if ((behavior & ~(CommandBehavior.Default | CommandBehavior.SequentialAccess | CommandBehavior.SingleResult
                              | CommandBehavior.SingleRow | CommandBehavior.CloseConnection)) != 0)
            {
                throw new ArgumentException($"InvalidCommandBehavior{behavior}");
            }

            if (_connection?.State != ConnectionState.Open)
            {
                _connection.Open();
                if (_connection?.State != ConnectionState.Open)
                {
                    throw new InvalidOperationException($"CallRequiresOpenConnection{nameof(ExecuteReader)}");
                }
            }

            if (string.IsNullOrEmpty(_commandText))
            {
                throw new InvalidOperationException($"CallRequiresSetCommandText{nameof(ExecuteReader)}");
            }
            var unprepared = false;
            TaosDataReader dataReader = null;
            var closeConnection = (behavior & CommandBehavior.CloseConnection) != 0;
            try
            {
                if (_parameters.IsValueCreated && _parameters.Value.Count > 0)
                {
                    var tr = ExecuteStmt(_commandText, _parameters);
                    dataReader = new TaosDataReader(command, new TaosWebSocketContext(tr));
                }
                else
                {
                    var tr = Execute(_commandText);
                    dataReader = new TaosDataReader(command, new TaosWebSocketContext(tr));
                }
            }
            catch when (unprepared)
            {
                throw;
            }
            return dataReader;
        }

        private TaosWSResult ExecuteStmt(string _commandText, Lazy<TaosParameterCollection> _parameters)
        {
            TaosWSResult wSResult = new TaosWSResult(); ;
            var pms = _parameters.Value;
            var req_id = 0;
            var _init = WSExecute<WSStmtRsp>(_stmt_client, "init", new { req_id });
            var stmt_id = _init.stmt_id;

            if (_init.code != 0) TaosException.ThrowExceptionForRC(new TaosErrorResult() { Code = _init.code, Error = _init.message });
            req_id++;
            var sql = StatementObject.ResolveCommandText(_commandText);
            var _prepare = WSExecute<WSStmtRsp>(_stmt_client, "prepare", new { req_id, stmt_id, sql = sql.CommandText });
            if (_prepare.code != 0) TaosException.ThrowExceptionForRC(new TaosErrorResult() { Code = _prepare.code, Error = _prepare.message });

            BindParameters(pms, out var columns, out var tags, out var subtablename);
            req_id++;
            if (!string.IsNullOrEmpty(subtablename))
            {
                var _set_table_name = WSExecute<WSStmtRsp>(_stmt_client, "set_table_name", new { req_id, stmt_id, name = subtablename });
                if (_set_table_name.code != 0) TaosException.ThrowExceptionForRC(new TaosErrorResult() { Code = _set_table_name.code, Error = _set_table_name.message });
                req_id++;
            }
            if (tags.Count > 0)
            {
                var _set_tags = WSExecute<WSStmtRsp>(_stmt_client, "set_tags", new { req_id, stmt_id, tags });
                if (_set_tags.code != 0) TaosException.ThrowExceptionForRC(new TaosErrorResult() { Code = _set_tags.code, Error = _set_tags.message });
                req_id++;
            }
            var _bind = WSExecute<WSStmtRsp>(_stmt_client, "bind", new { req_id, stmt_id, columns });
            if (_bind.code != 0) TaosException.ThrowExceptionForRC(new TaosErrorResult() { Code = _bind.code, Error = _bind.message });
            req_id++;

            var _add_batch = WSExecute<WSStmtRsp>(_stmt_client, "add_batch", new { req_id, stmt_id });
            if (_add_batch.code != 0) TaosException.ThrowExceptionForRC(new TaosErrorResult() { Code = _add_batch.code, Error = _add_batch.message });
            var _exec = WSExecute<WSStmtExecRsp>(_stmt_client, "exec", new { req_id, stmt_id });
            wSResult = new TaosWSResult() { StmtExec = _exec };
            if (_exec.code != 0) TaosException.ThrowExceptionForRC(new TaosErrorResult() { Code = _exec.code, Error = _exec.message });
            req_id++;
            //https://github.com/taosdata/taosadapter/issues/129#issuecomment-1369468337
            WSExecute(_stmt_client, "close", new { req_id, stmt_id });
            return wSResult;
        }

        private void BindParameters(TaosParameterCollection pms, out List<object[]> _datas, out List<string> _tags, out string _subtablename)
        {
            _datas = new List<object[]>();
            _tags = new List<string>();
            _subtablename = string.Empty;
            for (int i = 0; i < pms.Count; i++)
            {
                var tp = pms[i];
                var _bind = new KeyValuePair<string, object>();
                switch (Type.GetTypeCode(tp.Value?.GetType()))
                {
                    case TypeCode.DBNull:
                    case TypeCode.Empty:
                        _bind = new KeyValuePair<string, object>(tp.ParameterName, tp.Value);
                        break;
                    case TypeCode.Boolean:
                        _bind = new KeyValuePair<string, object>(tp.ParameterName, tp.Value as bool?);
                        break;

                    case TypeCode.Char:
                        _bind = new KeyValuePair<string, object>(tp.ParameterName, tp.Value as string);
                        break;

                    case TypeCode.Byte:
                        _bind = new KeyValuePair<string, object>(tp.ParameterName, tp.Value as byte?);
                        break;

                    case TypeCode.SByte:
                        _bind = new KeyValuePair<string, object>(tp.ParameterName, tp.Value as sbyte?);
                        break;

                    case TypeCode.DateTime:
                        var t0 = tp.Value as DateTime?;
                        if (!t0.HasValue)
                        {
                            throw new ArgumentException($"InvalidArgumentOfDateTime{tp.Value}");
                        }
                        _bind = new KeyValuePair<string, object>(tp.ParameterName, t0);
                        break;

                    case TypeCode.Single:
                        _bind = new KeyValuePair<string, object>(tp.ParameterName, tp.Value as float?);
                        break;

                    case TypeCode.Decimal:
                    case TypeCode.Double:
                        _bind = new KeyValuePair<string, object>(tp.ParameterName, tp.Value as double?);
                        break;

                    case TypeCode.Int16:
                        _bind = new KeyValuePair<string, object>(tp.ParameterName, tp.Value as short?);
                        break;

                    case TypeCode.Int32:
                        _bind = new KeyValuePair<string, object>(tp.ParameterName, tp.Value as int?);
                        break;

                    case TypeCode.Int64:
                        _bind = new KeyValuePair<string, object>(tp.ParameterName, tp.Value as long?);
                        break;

                    case TypeCode.UInt16:
                        _bind = new KeyValuePair<string, object>(tp.ParameterName, tp.Value as ushort?);
                        break;

                    case TypeCode.UInt32:
                        _bind = new KeyValuePair<string, object>(tp.ParameterName, tp.Value as uint?);
                        break;

                    case TypeCode.UInt64:
                        _bind = new KeyValuePair<string, object>(tp.ParameterName, tp.Value as ulong?);
                        break;

                    case TypeCode.String:
                        {
                            switch (tp.TaosType)
                            {
                                case TaosType.Text:
                                    _bind = new KeyValuePair<string, object>(tp.ParameterName, tp.Value as string);
                                    break;

                                case TaosType.Blob:
                                    _bind = new KeyValuePair<string, object>(tp.ParameterName, tp.Value as string);
                                    break;

                                default:
                                    break;
                            }
                        }
                        break;

                    case TypeCode.Object:
                        if (tp.Value?.GetType() == typeof(byte[]))//后期重写这里 ， 需要重写 MultiBindBinary
                        {
                            _bind = new KeyValuePair<string, object>(tp.ParameterName, Encoding.Default.GetString(tp.Value as byte[]));
                        }
                        else if (tp.Value?.GetType() == typeof(char[]))
                        {
                            _bind = new KeyValuePair<string, object>(tp.ParameterName, new string(tp.Value as char[]));
                        }
                        break;

                    default:
                        throw new NotSupportedException($"列{tp.ParameterName}的类型{tp.Value?.GetType()}({tp.DbType},{tp.TaosType})不支持");
                }
                //if (_bind.Value == null || string.IsNullOrEmpty(_bind.Value?.ToString()))
                //{
                //    throw new ArgumentNullException($"列{tp.ParameterName}的类型为空");
                //}
                //JObject jo = new()
                //{
                //        { _bind.Key, new JValue(_bind.Value) }
                //};
                if (tp.ParameterName.StartsWith("$"))
                {
                    //_tags.Add(jo.ToString());
                    _tags.Add(_bind.Value?.ToString());
                }
                else if (tp.ParameterName.StartsWith("@"))
                {
                    _datas.Add(new object[] { _bind.Value });
                }
                else if (tp.ParameterName.StartsWith("#"))
                {
                    _subtablename = tp.Value as string;
                }
            }
        }

        private R WSExecute<R>(ClientWebSocket _client, string _action, object req, Action<byte[], int> _deserialize_binary = null)
        {
            return WSExecute<R, object>(_client, new WSActionReq<object>() { Action = _action, Args = req }, _deserialize_binary);
        }

        private void WSExecute<T>(ClientWebSocket _client, string _action, T req)
        {
            var token = CancellationToken.None;
            var _req = JsonConvert.SerializeObject(new WSActionReq<object>() { Action = _action, Args = req });
            _client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(_req)), WebSocketMessageType.Text, true, CancellationToken.None).Wait(TimeSpan.FromSeconds(_builder.ConnectionTimeout));
        }
        private R WSExecute<R, T>(ClientWebSocket _client, string _action, T req, Action<byte[], int> _deserialize_binary = null)
        {
            return WSExecute<R, T>(_client, new WSActionReq<T>() { Action = _action, Args = req }, _deserialize_binary);
        }
        private R WSExecute<R, T>(ClientWebSocket _client, WSActionReq<T> req, Action<byte[], int> _deserialize_binary = null)
        {
            R _result = default;
            var token = CancellationToken.None;
            var _req = Newtonsoft.Json.JsonConvert.SerializeObject(req);
            Debug.WriteLine(_req);
            _client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(_req)), WebSocketMessageType.Text, true, CancellationToken.None).Wait(TimeSpan.FromSeconds(_builder.ConnectionTimeout));
            int bufferSize = 1024 * 1024 * 4;
            var buffer = new byte[bufferSize];
            var offset = 0;
            var free = buffer.Length;
            WebSocketMessageType _msgType;
            while (true)
            {
                var result = _client.ReceiveAsync(new ArraySegment<byte>(buffer, offset, free), token).GetAwaiter().GetResult();
                offset += result.Count;
                free -= result.Count;
                if (result.EndOfMessage)
                {
                    _msgType = result.MessageType;
                    break;
                }
                if (free == 0)
                {
                    // No free space
                    // Resize the outgoing buffer
                    var newSize = buffer.Length + bufferSize;
                    // Check if the new size exceeds a limit
                    // It should suit the data it receives
                    // This limit however has a max value of 2 billion bytes (2 GB)
                    if (newSize > 1024 * 1024 * 1024)
                    {
                        throw new Exception("Maximum size exceeded");
                    }
                    var newBuffer = new byte[newSize];
                    Array.Copy(buffer, 0, newBuffer, 0, offset);
                    buffer = newBuffer;
                    free = buffer.Length - offset;
                }
            }

            switch (_msgType)
            {
                case WebSocketMessageType.Binary:
                    _deserialize_binary?.Invoke(buffer, offset);
                    break;

                case WebSocketMessageType.Close:
                    break;

                case WebSocketMessageType.Text:
                    var json = Encoding.UTF8.GetString(buffer, 0, offset);
                    Debug.WriteLine($"TaosWebSocket WSExecute Json:{json}");
                    _result = Newtonsoft.Json.JsonConvert.DeserializeObject<R>(json);
                    break;

                default:
                    break;
            }
            return _result;
        }

        //https://github.com/taosdata/taosadapter/blob/e57b466a3f243901bc93b15519b57a26d649612a/controller/rest/ws_test.go
        //https://github.com/taosdata/taosadapter/blob/e57b466a3f243901bc93b15519b57a26d649612a/controller/rest/ws.go#L152
        private static volatile int _reqid = 0;

        private TaosWSResult Execute(string _commandText)
        {
            var dt = DateTime.Now;
            TaosWSResult wSResult = new TaosWSResult(); ;
            _reqid++;
            if (_reqid > 99999) _reqid = 0;
            var repquery = WSExecute<WSQueryRsp, WSQueryReq>(_ws_client, new WSActionReq<WSQueryReq>() { Action = "query", Args = new WSQueryReq() { req_id = _reqid, sql = _commandText } });
            if (repquery.code != 0)
            {
                TaosException.ThrowExceptionForRC(new TaosErrorResult() { Code = repquery.code, Error = repquery.message });
            }
            if (!repquery.is_update)
            {
                var repfetch = WSExecute<WSFetchRsp, WSFetchReq>(_ws_client, new WSActionReq<WSFetchReq>() { Action = "fetch", Args = new WSFetchReq { req_id = repquery.req_id, id = repquery.id } });
                if (repfetch.code == 0)
                {
                    List<byte> data = new List<byte>();
                    int _rows = repfetch.rows;
                    if (_rows > 0)
                    {
                        do
                        {
                            byte[] buffer = new byte[] { };
                            var repfetch_block = WSExecute<byte[], WSFetchReq>
                               (
                                   _ws_client, new WSActionReq<WSFetchReq>()
                                   {
                                       Action = "fetch_block",
                                       Args = new WSFetchReq() { req_id = repquery.req_id, id = repfetch.id }
                                   },
                                   (byte[] bytes, int len) =>
                                   {
                                       buffer = new byte[len];
                                       Array.Copy(bytes, buffer, len);
                                   }
                             );
                            repfetch = WSExecute<WSFetchRsp, WSFetchReq>(_ws_client, new WSActionReq<WSFetchReq>() { Action = "fetch", Args = new WSFetchReq { req_id = repquery.req_id, id = repquery.id } });
                            // _rows += repfetch.rows;
                            data.AddRange(buffer);
                        } while (!repfetch.completed);
                        WSExecute(_ws_client, "free_result", new { repquery.req_id, repquery.id });
                    }
                    wSResult = new TaosWSResult() { data = data.ToArray(), meta = repquery, rows = _rows };
                }
                else
                {
                    TaosException.ThrowExceptionForRC(new TaosErrorResult() { Code = repfetch.code, Error = repfetch.message });
                }
            }
            else
            {
                wSResult = new TaosWSResult() { meta = repquery };
            }
            return wSResult;
        }

        public string GetClientVersion()
        {
            return typeof(TaosWebSocket).Assembly.GetName().Version.ToString();
        }

        public string GetServerVersion()
        {
            var rep = WSExecute<WSVersionRsp, string>(_ws_client, new WSActionReq<string>() { Action = "version", Args = "" });
            return rep.version;
        }

        public void InitTaos(string configdir, int shell_activity_timer, string locale, string charset)
        {
        }

        public bool Open(TaosConnectionStringBuilder connectionStringBuilder)
        {
            _builder = connectionStringBuilder;
            var builder = connectionStringBuilder;
            string _timez = string.IsNullOrEmpty(builder.TimeZone) ? "" : $"?tz={builder.TimeZone}";
            _ws_client = new ClientWebSocket();
            _stmt_client = new ClientWebSocket();
            var ws_ok = _open_ws(builder);
            var stmt_ok = _open_stmt(builder);
            return ws_ok && stmt_ok;
        }

        private bool _open_stmt(TaosConnectionStringBuilder builder)
        {
            _open__ws_client(builder, _stmt_client, "/rest/stmt");
            return _ws_conn(builder, _stmt_client);
        }

        private bool _open_ws(TaosConnectionStringBuilder builder)
        {
            _open__ws_client(builder, _ws_client, "/rest/ws");
            return _ws_conn(builder, _ws_client);
        }

        private bool _ws_conn(TaosConnectionStringBuilder builder, ClientWebSocket _client)
        {
            var rep = WSExecute<WSConnRsp, WSConnReq>(_client, new WSActionReq<WSConnReq>() { Action = "conn", Args = new WSConnReq() { user = builder.Username, password = builder.Password, req_id = 0, db = builder.DataBase } });
            if (rep.code == 899)
            {
                rep = WSExecute<WSConnRsp, WSConnReq>(_client, new WSActionReq<WSConnReq>() { Action = "conn", Args = new WSConnReq() { user = builder.Username, password = builder.Password, req_id = 0 } });
            }
            if (rep.code != 0)
            {
                TaosException.ThrowExceptionForRC(new TaosErrorResult() { Code = rep.code, Error = rep.message });
            }
            return rep.code == 0;
        }

        public void Return(nint taos)
        {
        }

        public nint Take()
        {
            return IntPtr.Zero;
        }

        private void _open__schemaless(TaosConnectionStringBuilder builder)
        {
            if (_schemaless_client == null) _schemaless_client = new ClientWebSocket();
            _open__ws_client(builder, _schemaless_client, "/rest/schemaless");
            WSExecute(_schemaless_client, "conn", new { user = builder.Username, password = builder.Password });
        }
        private void _open__ws_client(TaosConnectionStringBuilder builder, ClientWebSocket _client, string path)
        {
            var url = $"ws://{builder.DataSource}:{builder.Port}{path}";
            if (string.IsNullOrEmpty(builder.Token))
            {
                _client.Options.Credentials = new NetworkCredential(builder.Username, builder.Password);
            }
            else
            {
                url += $"?token={builder.Token}";
            }
            _client.ConnectAsync(new Uri(url), CancellationToken.None).Wait(TimeSpan.FromSeconds(builder.ConnectionTimeout));
            if (_client.State != WebSocketState.Open)
            {
                TaosException.ThrowExceptionForRC(new TaosErrorResult() { Code = (int)_client.CloseStatus, Error = _client.CloseStatusDescription });
            }
        }

        public int ExecuteBulkInsert(string[] lines, TDengineSchemalessProtocol protocol, TDengineSchemalessPrecision _precision)
        {
            if (_schemaless_client == null || _schemaless_client.State != WebSocketState.Open)
            {
                _open__schemaless(_builder);
            }
            string precision = SchemalessPrecisionToString(_precision);
            var _insert = WSExecute<WSSchemalessRsp>(_schemaless_client, "insert", new
            {
                db = _builder.DataBase,
                protocol = (int)protocol,
                precision,
                ttl = 1000,
                data = string.Join("\n", lines)
            }
            );
            if (_insert.code != 0)
            {
                TaosException.ThrowExceptionForRC(new TaosErrorResult() { Code = _insert.code, Error = _insert.message });
            }
            return lines.Length;
        }

        private static string SchemalessPrecisionToString(TDengineSchemalessPrecision _precision)
        {
            var precision = "ms";
            switch (_precision)
            {
                case TDengineSchemalessPrecision.TSDB_SML_TIMESTAMP_SECONDS:
                    precision = "s";
                    break;

                case TDengineSchemalessPrecision.TSDB_SML_TIMESTAMP_MICRO_SECONDS:
                    precision = "us";
                    break;

                case TDengineSchemalessPrecision.TSDB_SML_TIMESTAMP_NANO_SECONDS:
                    precision = "ns";
                    break;

                case TDengineSchemalessPrecision.TSDB_SML_TIMESTAMP_MILLI_SECONDS:
                default:
                    precision = "ms";
                    break;
            }

            return precision;
        }
    }
}
