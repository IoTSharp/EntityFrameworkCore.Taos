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

namespace IoTSharp.Data.Taos.Protocols.TDRESTful
{
    internal class TaosRESTful : ITaosProtocol
    {
        private System.Net.Http.HttpClient _client = null;
        private Uri _uri;
        private string _databaseName;
        private TaosConnectionStringBuilder _builder;

        public bool ChangeDatabase(string databaseName)
        {
            _databaseName = databaseName;
            _builder.DataBase = _databaseName;
            ResetClient(_builder);
            return true;
        }

        public void Close(TaosConnectionStringBuilder connectionStringBuilder)
        {
            _client?.Dispose();
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
                                    v = $"\"{p.Value}\"";
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
            TaosResult result = null;
#if DEBUG
            Console.WriteLine($"_commandText:{_commandText}");
#endif
            var body = _commandText;
            var rest = new HttpRequestMessage(HttpMethod.Post, _uri);
            rest.Content = new StringContent(body);
            HttpResponseMessage response = null;
            string context = string.Empty;
            var task = Task.Run(async () =>
             {
                 response = await _client.SendAsync(rest);
                 context = await response.Content?.ReadAsStringAsync();
             });
            try
            {
                var isok = Task.WaitAll(new[] { task }, _client.Timeout);
                if (isok)
                {
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
                }
                else
                {
                    TaosException.ThrowExceptionForRC(_commandText, new TaosErrorResult() { Code = -1, Error = task.Exception?.Message + "\n" + task.Exception?.InnerException?.Message });
                }
            }
            catch (Exception ex)
            {
                TaosException.ThrowExceptionForRC(_commandText, new TaosErrorResult() { Code = -2, Error = ex.Message + "\n" + ex.InnerException?.Message });
            }
            return result;
        }

        private static T JsonDeserialize<T>(string context)
        {
            //#if NET46_OR_GREATER || NETSTANDARD2_0_OR_GREATER
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(context);
            //#else
            //            return System.Text.Json.JsonSerializer.Deserialize<T>(context);
            //#endif
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
            ResetClient(_builder);
            return true;
        }

        private void ResetClient(TaosConnectionStringBuilder connectionStringBuilder)
        {
            var builder = connectionStringBuilder;
            string _timez = string.IsNullOrEmpty(builder.TimeZone) ? "" : $"?tz={builder.TimeZone}";
            var httpClientHandler = new HttpClientHandler();
            _client = new HttpClient(httpClientHandler);
            _uri = new Uri($"http://{builder.DataSource}:{builder.Port}/rest/sql{(!string.IsNullOrEmpty(builder.DataBase) ? "/" : "")}{builder.DataBase}{_timez}");
            _client.Timeout = TimeSpan.FromSeconds(builder.ConnectionTimeout);
            var authToken = Encoding.ASCII.GetBytes($"{builder.Username}:{builder.Password}");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));
            _client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/plain"));
            _client.DefaultRequestHeaders.AcceptCharset.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("utf-8"));
            var _name = typeof(TaosRESTful).Assembly.GetName();
            _client.DefaultRequestHeaders.Add("User-Agent", $"{_name.Name}/{_name.Version}");
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