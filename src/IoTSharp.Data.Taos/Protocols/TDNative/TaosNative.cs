using IoTSharp.Data.Taos;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using TDengineDriver;

namespace IoTSharp.Data.Taos.Protocols
{
    internal class TaosNative : ITaosProtocol
    {
        private static readonly ConcurrentDictionary<string, ConcurrentTaosQueue> g_pool = new ConcurrentDictionary<string, ConcurrentTaosQueue>();
        private ConcurrentTaosQueue _queue = null;
        private static bool _dll_isloaded = false;
        private readonly DateTime _dt1970;

        public TaosNative()
        {
            _dt1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        public void InitTaos(string configdir, int shell_activity_timer, string locale, string charset)
        {
            if (_dll_isloaded == false)
            {
                if (!string.IsNullOrEmpty(configdir) && !string.IsNullOrEmpty(configdir) && File.Exists(Path.Combine(configdir, "taos.cfg")))
                {
                    TDengine.Options((int)TDengineInitOption.TSDB_OPTION_CONFIGDIR, configdir);
                }
                else if (File.Exists(Path.Combine(AppContext.BaseDirectory, "taos.cfg")))
                {
                    TDengine.Options((int)TDengineInitOption.TSDB_OPTION_CONFIGDIR, AppContext.BaseDirectory);
                }
                else if (File.Exists(Path.Combine(Environment.CurrentDirectory, "taos.cfg")))
                {
                    TDengine.Options((int)TDengineInitOption.TSDB_OPTION_CONFIGDIR, Environment.CurrentDirectory);
                }
                else
                {
                    var configDir = "C:/TDengine/cfg";
#if NET5_0_OR_GREATER
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        configDir = "/etc/taos";
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        configDir = "C:/TDengine/cfg";
                    }
#else
                    configDir = "C:/TDengine/cfg";
#endif
                    var syscfg = new FileInfo(Path.Combine(configDir, "taos.cfg"));
                    if (syscfg.Exists)
                    {
                        TDengine.Options((int)TDengineInitOption.TSDB_OPTION_CONFIGDIR, configDir);
                    }
                }
                if (!string.IsNullOrEmpty(locale))
                {
                    TDengine.Options((int)TDengineInitOption.TSDB_OPTION_LOCALE, locale);
                }
                if (!string.IsNullOrEmpty(charset))
                {
                    TDengine.Options((int)TDengineInitOption.TSDB_OPTION_CHARSET, charset);
                }
                if (shell_activity_timer > 0)
                {
                    TDengine.Options((int)TDengineInitOption.TSDB_OPTION_SHELL_ACTIVITY_TIMER, $"{shell_activity_timer}");
                }
                TDengine.Init();
                Process.GetCurrentProcess().Disposed += (sender, e) =>
                {
                    TDengine.Cleanup();
                };
                _dll_isloaded = true;
            }
        }

        public string GetServerVersion()
        {
            var _version = string.Empty;
            var _taos = Take();
            if (_taos == IntPtr.Zero)
            {
                Return(_taos);
                TaosException.ThrowExceptionForRC(-10005, "Connection is not open", null);
            }
            else if (string.IsNullOrEmpty(_version))
            {
                _version = Marshal.PtrToStringAnsi(TDengine.GetServerInfo(_taos));
                Return(_taos);
            }
            return _version;
        }

        public nint Take()
        {
            return _queue.Take();
        }

        public void Return(nint taos)
        {
            _queue.Return(taos);
        }

        public string GetClientVersion()
        {
            return Marshal.PtrToStringAnsi(TDengine.GetClientInfo());
        }

        public bool Open(TaosConnectionStringBuilder builder)
        {
            var _connectionString = builder.ConnectionString;
            lock (g_pool)
            {
                _queue = g_pool.GetOrAdd(_connectionString, new ConcurrentTaosQueue() { Timeout = builder.ConnectionTimeout });
                _queue.AddRef();
                for (int i = 0; i < builder.PoolSize + 1; i++)
                {
                    var c = TDengine.Connect(builder.DataSource, builder.Username, builder.Password, "", (short)builder.Port);
                    if (c != IntPtr.Zero)
                    {
                        _queue.Return(c);
                    }
                }
                if (_queue.TaosQueue.IsEmpty)
                {
                    TaosException.ThrowExceptionForRC(new TaosErrorResult() { Code = -1, Error = "Can't open  connection." });
                }
            }
            return true;
        }

        public void Close(TaosConnectionStringBuilder builder)
        {
            var _connectionString = builder.ConnectionString;
            lock (g_pool)
            {
                _queue.RemoveRef();
                if (_queue.GetRef() == 0)
                {
                    for (int i = 0; i < _queue.TaosQueue.Count; i++)
                    {
                        try
                        {
                            var tk = _queue.Take();
                            if (tk != IntPtr.Zero)
                            {
                                TDengine.Close(tk);
                            }
                        }
                        catch (Exception)
                        {


                        }
                    }
                    _queue = null;
                    g_pool.TryRemove(_connectionString, out var va1l);
                }
            }
        }

        public bool ChangeDatabase(string databaseName)
        {
            var _sql = $"use {databaseName}";
            var ptr = _sql.ToUTF8IntPtr();
            var result = _queue.TaosQueue.ToList().TrueForAll(_taos =>
            {
                var req = TDengine.Query(_taos, ptr.ptr);
                int code = TDengine.ErrorNo(req);
                var msg = TDengine.Error(req);
                TDengine.FreeResult(req);
                return code == 0;
            });
            ptr.ptr.FreeUtf8IntPtr();
            return result;
        }

        public TaosDataReader ExecuteReader(CommandBehavior behavior, TaosCommand command)
        {
            var _taos = Take();
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
            if (!_connection.SelectedDataBase)
            {
                _connection.ChangeDatabase(_connection.Database);
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
#if DEBUG
                Debug.WriteLine($"_commandText:{_commandText}");
#endif
                int _affectRows = -1;
                IntPtr ptr = IntPtr.Zero;
                if (_parameters.IsValueCreated)
                {
                    var sql = StatementObject.ResolveCommandText(_commandText);
                    _commandText =sql.CommandText;
                    var stmt = TDengine.StmtInit(_taos);
                    if (stmt == IntPtr.Zero)
                    {
                        TaosException.ThrowExceptionForStmt(nameof(TDengine.StmtInit), _commandText, -1, stmt);
                    }
                    else
                    {
                        var pms = _parameters.Value;
                        int res = TDengine.StmtPrepare(stmt, _commandText);
                        if (res != 0)
                        {
                            TaosException.ThrowExceptionForStmt(nameof(TDengine.StmtPrepare), _commandText, res, stmt);
                        }
                        else
                        {
                            var isinsert = TDengine.StmtIsInsert(stmt);
                            BindParameters(pms, _taos, out var datas, out var tags,out var subtablename);
                            int ret = -1;
                            if (isinsert)
                            {
                                int tags_ret = -1;
                                if (tags.Count > 0 && !string.IsNullOrEmpty(subtablename))
                                {
                                    tags_ret = TDengine.StmtSetTbnameTags(stmt, subtablename, tags.ToArray());
                                    if (tags_ret != 0)
                                    {
                                        TaosException.ThrowExceptionForStmt(nameof(TDengine.StmtSetTbnameTags), _commandText, ret, stmt);
                                    }
                                }
                                else
                                {
                                    tags_ret = 0;
                                }

                                int param_ret = TDengine.StmtBindParamBatch(stmt, datas.ToArray());
                                if (param_ret != 0)
                                {
                                    TaosException.ThrowExceptionForStmt(nameof(TDengine.StmtBindParamBatch), _commandText, ret, stmt);
                                }
                                if (param_ret == 0 && tags_ret == 0)
                                {
                                    ret = TDengine.StmtAddBatch(stmt);
                                    if (ret != 0)
                                    {
                                        TaosException.ThrowExceptionForStmt(nameof(TDengine.StmtAddBatch), _commandText, ret, stmt);
                                    }
                                }
                                else
                                {
                                    TaosException.ThrowExceptionForStmt(nameof(TDengine.StmtBindParamBatch), _commandText, ret, stmt);
                                }
                            }
                            else
                            {
                                ret = TDengine.StmtBindParam(stmt, datas.ToArray());
                                if (ret != 0)
                                {
                                    TaosException.ThrowExceptionForStmt(nameof(TDengine.StmtBindParam), _commandText, ret, stmt);
                                }
                            }
                            if (ret == 0)
                            {
                                int re = TDengine.StmtExecute(stmt);
                                if (re == 0)
                                {
                                    if (!isinsert)
                                    {
                                        ptr = TDengine.StmtUseResult(stmt);
                                        if (ptr == IntPtr.Zero)
                                        {
                                            TaosException.ThrowExceptionForStmt(nameof(TDengine.StmtUseResult), _commandText, -2, stmt);
                                        }
                                    }
                                    _affectRows = TDengine.StmtAffected_rows(stmt);
                                }
                                else
                                {
                                    TaosException.ThrowExceptionForStmt(nameof(TDengine.StmtExecute), _commandText, re, stmt);
                                }
                            }
                            TaosMultiBind.FreeTaosBind(datas.ToArray());
                            TaosMultiBind.FreeTaosBind(tags.ToArray());
                            TDengine.StmtClose(stmt);
                        }
                    }
                }
                else
                {
                    ptr = TDengine.Query(_taos, _commandText);
                    if (ptr != IntPtr.Zero)
                    {
                        var code = TDengine.ErrorNo(ptr);
                        if (code != 0)
                        {
                            TaosException.ThrowExceptionForRC(_commandText, new TaosErrorResult() { Code = code, Error = TDengine.Error(ptr) });
                        }
                        else
                        {
                            _affectRows = TDengine.AffectRows(ptr);
                        }
                    }
                    else
                    {
                        var code = TDengine.ErrorNo(_taos);
                        var xc = new TaosErrorResult()
                        {
                            Code = code,
                            Error = TDengine.Error(_taos)
                        };
                    }
                }
                if (_affectRows >= 0)
                {
                    taosField[] metas = ptr!=IntPtr.Zero ? TDengine.FetchFields(ptr):new taosField[] { };
#if DEBUG
                    if (Debugger.IsAttached)
                    {
                        for (int j = 0; j < metas?.Length; j++)
                        {
                            var meta = metas[j];
                            Debug.WriteLine("index:" + j + ", type:" + meta.type + ", typename:" + meta.TypeName + ", name:" + meta.Name + ", size:" + meta.Size);
                        }
                    }
#endif
                    dataReader = new TaosDataReader(command, new TaosNativeContext(metas, closeConnection, _taos, ptr, _affectRows, metas?.Length ?? 0));
                    dataReader.OnDispose += (sender, e) => Return(_taos);
                }
                else
                {
                    TaosException.ThrowExceptionForRC(_commandText, new TaosErrorResult() { Code = -1, Error = "unknow error" });
                }
            }
            catch when (unprepared)
            {
                Return(_taos);
                throw;
            }

            return dataReader;
        }

        private void BindParameters(TaosParameterCollection pms, IntPtr _taos, out List<TAOS_MULTI_BIND> _datas, out List<TAOS_MULTI_BIND> _tags, out string _subtablename)
        {
            _datas = new List<TAOS_MULTI_BIND>();
            _tags = new List<TAOS_MULTI_BIND>();
            _subtablename = string.Empty;
            for (int i = 0; i < pms.Count; i++)
            {
                var tp = pms[i];
                TAOS_MULTI_BIND _bind = new TAOS_MULTI_BIND();
                switch (Type.GetTypeCode(tp.Value?.GetType()))
                {
                    case TypeCode.Boolean:
                        _bind = TaosMultiBind.MultiBindBool(new bool?[] { tp.Value as bool? });
                        break;

                    case TypeCode.Char:
                        _bind = TaosMultiBind.MultiBindNchar(new string[] { tp.Value as string });
                        break;

                    case TypeCode.Byte:
                        _bind = TaosMultiBind.MultiBindUTinyInt(new byte?[] { tp.Value as byte? });
                        break;

                    case TypeCode.SByte:
                        _bind = TaosMultiBind.MultiBindTinyInt(new sbyte?[] { (sbyte?)tp.Value });
                        break;

                    case TypeCode.DateTime:
                        var t0 = tp.Value as DateTime?;
                        if (!t0.HasValue)
                        {
                            throw new ArgumentException($"InvalidArgumentOfDateTime{tp.Value}");
                        }
                        _bind = TaosMultiBind.MultiBindTimestamp(new long[] { GetDateTimeFrom(t0.GetValueOrDefault(), _taos) });
                        break;

                    case TypeCode.Single:
                        _bind = TaosMultiBind.MultiBindFloat(new float?[] { tp.Value as float? });
                        break;

                    case TypeCode.Decimal:
                    case TypeCode.Double:
                        _bind = TaosMultiBind.MultiBindDouble(new double?[] { tp.Value as double? });
                        break;

                    case TypeCode.Int16:
                        _bind = TaosMultiBind.MultiBindSmallInt(new short?[] { tp.Value as short? });
                        break;

                    case TypeCode.Int32:
                        _bind = TaosMultiBind.MultiBindInt(new int?[] { tp.Value as int? });
                        break;

                    case TypeCode.Int64:
                        _bind = TaosMultiBind.MultiBindBigint(new long?[] { tp.Value as long? });
                        break;

                    case TypeCode.UInt16:
                        _bind = TaosMultiBind.MultiBindUSmallInt(new ushort?[] { tp.Value as ushort? });
                        break;

                    case TypeCode.UInt32:
                        _bind = TaosMultiBind.MultiBindUInt(new uint?[] { tp.Value as uint? });
                        break;

                    case TypeCode.UInt64:
                        _bind = TaosMultiBind.MultiBindUBigInt(new ulong?[] { tp.Value as ulong? });
                        break;

                    case TypeCode.String:
                        {
                            switch (tp.TaosType)
                            {
                                case TaosType.Text:
                                    _bind = TaosMultiBind.MultiBindNchar(new string[] { tp.Value as string });
                                    break;

                                case TaosType.Blob:
                                    _bind = TaosMultiBind.MultiBindBinary(new string[] { tp.Value as string });
                                    break;

                                default:
                                    break;
                            }
                        }
                        break;

                    case TypeCode.Object:
                        if (tp.Value?.GetType() == typeof(byte[]))//后期重写这里 ， 需要重写 MultiBindBinary
                        {
                            _bind = TaosMultiBind.MultiBindBinary(new string[] { Encoding.Default.GetString(tp.Value as byte[]) });
                        }
                        else if (tp.Value?.GetType() == typeof(char[]))
                        {
                            _bind = TaosMultiBind.MultiBindNchar(new string[] { new string(tp.Value as char[]) });
                        }
                        break;

                    default:
                        throw new NotSupportedException($"列{tp.ParameterName}的类型{tp.Value?.GetType()}({tp.DbType},{tp.TaosType})不支持");
                }
                if (_bind.buffer_type == 0 && _bind.buffer_length == 0 && _bind.buffer == IntPtr.Zero)
                {
                    throw new ArgumentNullException($"列{tp.ParameterName}的类型为空");
                }
                if (tp.ParameterName.StartsWith("$"))
                {
                    _tags.Add(_bind);
                }
                else if (tp.ParameterName.StartsWith("@"))
                {
                    _datas.Add(_bind);
                }
                else if (tp.ParameterName.StartsWith("#"))
                {
                    _subtablename = tp.Value as string;
                }
            }
        }

        internal long GetDateTimeFrom(DateTime dt, IntPtr _taos)
        {
            var val = dt.ToUniversalTime().Ticks - _dt1970.Ticks;
            //double tsp;
            var _dateTimePrecision = (TSDB_TIME_PRECISION)TDengine.ResultPrecision(_taos);
            switch (_dateTimePrecision)
            {
                /*
                * ticks为100纳秒，必须乘以10才能达到微秒级的区分度
                * 1秒s    = 1000毫秒ms
                * 1毫秒ms = 1000微秒us
                * 1微秒us = 1000纳秒ns
                * 因此， 1毫秒ms = 1000000纳秒ns = 10000ticks
                */
                case TSDB_TIME_PRECISION.TSDB_TIME_PRECISION_NANO:
                    val *= 100;
                    break;

                case TSDB_TIME_PRECISION.TSDB_TIME_PRECISION_MICRO:
                    val /= 10;
                    break;

                case TSDB_TIME_PRECISION.TSDB_TIME_PRECISION_MILLI:
                default:
                    val /= 10000;
                    break;
            }
            return val;
        }

        public int ExecuteBulkInsert(string[] lines, TDengineSchemalessProtocol protocol, TDengineSchemalessPrecision precision)
        {
            int affectedRows = 0;
            var _taos = Take();
            IntPtr res = TDengine.SchemalessInsert(_taos, lines, lines.Length, (int)protocol, (int)precision);
            if (TDengine.ErrorNo(res) != 0)
            {
                var tdr = new TaosErrorResult() { Code = TDengine.ErrorNo(res), Error = TDengine.Error(res) };
                Return(_taos);
                TaosException.ThrowExceptionForRC(tdr);
            }
            else
            {
                affectedRows = TDengine.AffectRows(res);
                TDengine.FreeResult(res);
                Return(_taos);
            }
            return affectedRows;
        }
    }
}
