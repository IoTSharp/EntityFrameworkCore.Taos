using System;
using System.Data;
using System.Net.Http;
using System.Net;
using TDengineDriver;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using IoTSharp.Data.Taos.Protocols.TDWebSocket;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Net.NetworkInformation;
using System.Threading;
using System.Security.Cryptography;

namespace IoTSharp.Data.Taos.Protocols.TDRESTful
{
    internal class TaosRESTful : ITaosProtocol
    {
        //private System.Net.Http.HttpClient _client = null;
        private string _databaseName;
        private TaosConnectionStringBuilder _builder;

        public bool ChangeDatabase(string databaseName)
        {
            _databaseName = databaseName;
            _builder.DataBase = _databaseName;
            //ResetClient(_builder);
            return true;
        }

        public void Close(TaosConnectionStringBuilder connectionStringBuilder)
        {
            //_client?.Dispose();
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

            //if (_connection?.State != ConnectionState.Open)
            //{
            //    _connection.Open();
            //    if (_connection?.State != ConnectionState.Open)
            //    {
            //        throw new InvalidOperationException($"CallRequiresOpenConnection{nameof(ExecuteReader)}");
            //    }
            //}

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
                    var pms = new List<TaosParameter>();
                    foreach (TaosParameter p in _parameters.Value)
                    {
                        pms.Add(p);
                    }
                    pms.OrderByDescending(o => o.ParameterName?.Length ?? 0)
                        .ToList().ForEach(p =>
                    {
                        var v = "''";
                        switch (p.TaosType)
                        {
                            case TaosType.Integer:
                                v = p.Value.ToString();
                                break;
                            case TaosType.Real:
                                v = p.Value.ToString();
                                break;
                            case TaosType.Text:
                                if (string.IsNullOrWhiteSpace(p.Value?.ToString()))
                                {
                                    v = "null";
                                }
                                else if (p.Value is DateTime pvt)
                                {
                                    v = $"\"{pvt.ToString("yyyy-MM-dd HH:mm:ss.fffffff")}\"";
                                }
                                else
                                {
                                    v = $"\"{ToLiteral(p.Value.ToString())}\"";
                                }
                                break;
                            case TaosType.Blob:
                                v = $"{p.Value}";
                                break;
                            default:
                                break;
                        }
                        _commandText = _commandText.Replace(p.ParameterName, v);
                    });
                }
                var tr = Execute(_commandText);
                dataReader = new TaosDataReader(command, new TaosRESTfulContext(tr));

            }
            catch when (unprepared)
            {
                throw;
            }
            return dataReader;
        }

        private TaosResult Execute(string _commandText)
        {
            var cmdPackage = CombineCommandPackage.CreatePackage(_commandText, this);
            return cmdPackage.ExeTask.Result;
        }



        internal class CombineCommandPackage
        {
            private const int taosSqlMaxLength = 1048576;
            private static System.Collections.Concurrent.ConcurrentQueue<CombineCommandPackage> packagesQueue = new System.Collections.Concurrent.ConcurrentQueue<CombineCommandPackage>();
            internal Task<TaosResult> ExeTask { get; private set; }
            public string CommandText { get; }
            //internal TaosResult Result { get; private set; }


            public bool IsInsertPackage { get; }

            private TaosRESTful taosRESTful;
            private TaskCompletionSource<TaosResult> _tcs;
            internal DateTime CreateTime { get; private set; }


            static CombineCommandPackage()
            {
                Task.Run(() =>
                {
                    var insertPackages = new List<CombineCommandPackage>();
                    while (true)
                    {
                        SpinWait.SpinUntil(() => packagesQueue.Count > 0 || (insertPackages != null && insertPackages.Count > 0));

                        if (packagesQueue.TryDequeue(out var package))
                        {

                            if (package.IsInsertPackage)
                            {
                                insertPackages.Add(package);
                            }
                            else
                            {
                                _ = CombineExecute(new List<CombineCommandPackage> { package });
                            }
                        }
                        if (
                            insertPackages != null &&
                            insertPackages.Count > 0 &&
                            (
                                DateTime.Now - insertPackages[0].CreateTime > TimeSpan.FromMilliseconds(4) ||
                                insertPackages.Sum(s => s.CommandText?.Length ?? 0) * 1.1 > taosSqlMaxLength
                            )
                           )
                        {
                            _ = CombineExecute(insertPackages);
                            insertPackages = new List<CombineCommandPackage>();
                        }
                    }
                });
            }
            internal static CombineCommandPackage CreatePackage(string _commandText, TaosRESTful taosRESTful)
            {
                var package = new CombineCommandPackage(_commandText, taosRESTful);
                packagesQueue.Enqueue(package);

                return package; ;
            }


            private async static Task CombineExecute(List<CombineCommandPackage> packages)
            {
                if (packages == null || packages.Count == 0) return;
                //聚合insert 语句

                var _commandText = string.Join("", packages.Select(s => s.CommandText));

                if (packages[0].IsInsertPackage)
                {
                    _commandText = _commandText.Replace(";INSERT INTO ", " ");
                }


                TaosResult result = null;
#if DEBUG
                Debug.WriteLine($"_commandText:{_commandText}");
#endif
                var body = _commandText;
                var rest = new HttpRequestMessage(HttpMethod.Post, "");
                rest.Content = new StringContent(body);
                string context = string.Empty;
                using var _client = packages[0].taosRESTful.GetClient();
                try
                {
                    var response = await _client.SendAsync(rest);

                    context = await response.Content?.ReadAsStringAsync();

                    result = JsonDeserialize<TaosResult>(context);
                    if (response.IsSuccessStatusCode)
                    {

                        Debug.WriteLine($"Exec code {result.code},rows:{result.rows},cols:{result.column_meta?.Count}");
                        if (result.code != 0)
                        {
                            TaosException.ThrowExceptionForRC(_commandText, new TaosErrorResult() { Code = result.code, Error = result.desc });
                        }
                    }
                    else if (result != null)
                    {
                        TaosException.ThrowExceptionForRC(_commandText, new TaosErrorResult() { Code = result.code, Error = result.desc });
                    }
                    else
                    {
                        TaosException.ThrowExceptionForRC(_commandText, new TaosErrorResult() { Code = (int)response.StatusCode, Error = response.ReasonPhrase });
                    }


                    packages.ForEach(p =>
                    {
                        p._tcs.SetResult(result);
                    });
                }
                catch (Exception ex)
                {
                    packages.ForEach(p =>
                    {
                        p._tcs.SetException(ex);

                    });
                    //TaosException.ThrowExceptionForRC(_commandText, new TaosErrorResult() { Code = -2, Error = ex.Message + "\n" + ex.InnerException?.Message });
                }


            }

            private CombineCommandPackage(string _commandText, TaosRESTful _taosRESTful)
            {
                _tcs = new TaskCompletionSource<TaosResult>();
                IsInsertPackage = _commandText?.ToLower()?.StartsWith("insert ") ?? false;
                taosRESTful = _taosRESTful;
                ExeTask = _tcs.Task;
                CreateTime = DateTime.Now;
                CommandText = _commandText?.Trim();
            }
        }

        private static T JsonDeserialize<T>(string context)
        {
            //#if NET46_OR_GREATER || NETSTANDARD2_0_OR_GREATER
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(context);
            //#else
            //            return System.Text.Json.JsonSerializer.Deserialize<T>(context);
            //#endif
        }
        private static string ToLiteral(string input, bool addQuote = false)
        {
            StringBuilder literal = new StringBuilder(input.Length + 2);
            if (addQuote) literal.Append("\"");
            foreach (var c in input)
            {
                switch (c)
                {
                    case '\"': literal.Append("\\\""); break;
                    case '\\': literal.Append(@"\\"); break;
                    case '\0': literal.Append(@"\0"); break;
                    case '\a': literal.Append(@"\a"); break;
                    case '\b': literal.Append(@"\b"); break;
                    case '\f': literal.Append(@"\f"); break;
                    case '\n': literal.Append(@"\n"); break;
                    case '\r': literal.Append(@"\r"); break;
                    case '\t': literal.Append(@"\t"); break;
                    case '\v': literal.Append(@"\v"); break;
                    default:
                        // ASCII printable character
                        if (c >= 0x20 && c <= 0x7e)
                        {
                            literal.Append(c);
                            // As UTF16 escaped character
                        }
                        else
                        {
                            literal.Append(@"\u");
                            literal.Append(((int)c).ToString("x4"));
                        }
                        break;
                }
            }
            if (addQuote) literal.Append("\"");
            return literal.ToString();
        }

        private static string JsonSerialize<T>(T obj)
        {
#if NET46_OR_GREATER || NETSTANDARD2_0_OR_GREATER
            return Newtonsoft.Json.JsonConvert.SerializeObject(obj);
#else
            return System.Text.Json.JsonSerializer.Serialize(obj);
#endif
        }

        public string GetClientVersion()
        {
            return Execute("SELECT CLIENT_VERSION()")?.Scalar as string;
        }

        public string GetServerVersion()
        {
            return Execute("SELECT SERVER_VERSION()")?.Scalar as string;
        }

        public void InitTaos(string configdir, int shell_activity_timer, string locale, string charset)
        {
        }

        public bool Open(TaosConnectionStringBuilder connectionStringBuilder)
        {
            _builder = connectionStringBuilder;
            //ResetClient(_builder);
            return true;
        }

        private HttpClient GetClient()
        {
            var builder = this._builder;
            string _timez = string.IsNullOrEmpty(builder.TimeZone) ? "" : $"?tz={builder.TimeZone}";
            var httpClientHandler = new HttpClientHandler();
            var _client = new HttpClient(httpClientHandler);
            var _uri = new Uri($"http://{builder.DataSource}:{builder.Port}/rest/sql{(!string.IsNullOrEmpty(builder.DataBase) ? "/" : "")}{builder.DataBase}{_timez}");
            _client.BaseAddress = _uri;
            _client.Timeout = TimeSpan.FromSeconds(builder.ConnectionTimeout);
            var authToken = Encoding.ASCII.GetBytes($"{builder.Username}:{builder.Password}");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));
            _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/plain"));
            _client.DefaultRequestHeaders.AcceptCharset.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("utf-8"));
            var _name = typeof(TaosRESTful).Assembly.GetName();
            _client.DefaultRequestHeaders.Add("User-Agent", $"{_name.Name}/{_name.Version}");

            return _client;
        }

        public void Return(nint taos)
        {
        }

        public nint Take()
        {
            return IntPtr.Zero;
        }

        public int ExecuteBulkInsert(string[] lines, TDengineSchemalessProtocol protocol, TDengineSchemalessPrecision precision)
        {
            throw new NotSupportedException("RESTful  不支持 ExecuteBulkInsert");
        }
    }
}