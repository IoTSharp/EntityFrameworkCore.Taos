using Ductus.FluentDocker.Model.Containers;
using Ductus.FluentDocker.Services;
using Ductus.FluentDocker.Services.Extensions;
using IoTSharp.Data.Taos;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Taos.Ado.Tests
{
    [TestClass]
    public class UnitTestTaos
    {
        private IContainerService container;
        private string database = "";
        private TaosConnectionStringBuilder builder;

        [TestInitialize]
        public void Initialize()
        {
            database = "db_" + DateTime.Now.ToString("yyyyMMddHHmmss");
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            DbProviderFactories.RegisterFactory("TDengine", TaosFactory.Instance);
            var hosts = new Hosts().Discover();
            var _docker = hosts.FirstOrDefault(x => x.IsNative) ?? hosts.FirstOrDefault(x => x.Name == "default");
            container = _docker.Create("tdengine/tdengine:2.4.0.12", false, new ContainerCreateParams
            {
                PortMappings = new[] { "6030:6030", "6035:6035", "6041:6041", "6030:6030/udp", "6035:6035/udp" },
                Hostname = System.Net.Dns.GetHostName()
            });
            container.Start();
            var f = container.GetRunningProcesses().Rows.ToList();
            container.WaitForProcess("taosd -c /tmp/taos", (long)TimeSpan.FromSeconds(60).TotalMilliseconds);
            container.WaitForProcess("taosadapter", (long)TimeSpan.FromSeconds(60).TotalMilliseconds);
            container.WaitForHttp($"http://{System.Net.Dns.GetHostName()}:6041/rest/login/root/taosdata", (int)TimeSpan.FromSeconds(60).TotalMilliseconds, (Ductus.FluentDocker.Common.RequestResponse response, int stat) =>
              {
                  var jo = JObject.Parse(response.Body);
                  int result = stat;
                  if (jo.TryGetValue("code", out JToken code))
                  {
                      result = (int)(code?.Value<int>());
                  }
                  return result;
              });
            builder = new TaosConnectionStringBuilder()
            {
                DataSource = System.Net.Dns.GetHostName(),
                DataBase = database,
                Username = "root",
                Password = "taosdata",
                Port = 6030
            };
            try
            {
                NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), DllImportResolver);
            }
            catch (Exception ex)
            {
                Debug.Write(ex.Message);
            }
            using (var connection = new TaosConnection(builder.ConnectionString))
            {
                connection.Open();
                connection.CreateCommand($"create database {database};").ExecuteNonQuery();
                connection.ChangeDatabase(database);
            }
        }
        private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName == "taos")
            {
                // On systems with AVX2 support, load a different library.
                if (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.Is64BitProcess)
                {
                    return NativeLibrary.Load("taos_win_x64.dll");
                }
                else if (Environment.OSVersion.Platform == PlatformID.Win32NT && !Environment.Is64BitProcess)
                {
                    return NativeLibrary.Load("taos_win_x86.dll");
                }
                else if (Environment.OSVersion.Platform == PlatformID.Unix && Environment.Is64BitProcess)
                {
                    return NativeLibrary.Load("libtaos_linux_x64.so");
                }
            }

            // Otherwise, fallback to default import resolver.
            return IntPtr.Zero;
        }
        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                container?.Stop();
                container?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.Write(ex.Message);
            }
        }
        [TestMethod]
        public void TestExecuteBulkInsert_Json()
        {
            using (var connection = new TaosConnection(builder.ConnectionString))
            {
                connection.Open();
                connection.ChangeDatabase(database);
                var payload = new JObject();
                var tags = new JObject();
                payload.Add("metric", "stb3_0");

                var timestamp = new JObject
                {
                    { "value", 1626006833 },
                    { "type", "s" }
                };
                payload.Add("timestamp", timestamp);
                static JObject AddTag(JObject tags, string name, object value, string type)
                {
                    var tag = new JObject
                    {
                        { "value", true },
                        { "type", "bool" }
                    };
                    tags.Add(name, tag);
                    return tag;
                }
                var metric_val = new JObject
                {
                    { "value", "hello" },
                    { "type", "nchar" }
                };
                payload.Add("value", metric_val);
                AddTag(tags, "t1", true, "bool");
                AddTag(tags, "t2", false, "bool");
                AddTag(tags, "t3", 127, "tinyint");
                AddTag(tags, "t4", 32767, "smallint");
                AddTag(tags, "t5", 127, "2147483647");
                AddTag(tags, "t6", (double)9223372036854775807, "bigint");
                AddTag(tags, "t7", 11.12345, "float");
                AddTag(tags, "t8", 22.1234567890, "double");
                AddTag(tags, "t9", "binary_val", "binary");
                AddTag(tags, "t10", "你好", "nchar");
                payload.Add("tags", tags);
                int result = connection.ExecuteBulkInsert(new JObject[] { payload });
                Assert.AreEqual(1, result);
                connection.Close();
            }
        }

        [TestMethod]
        public void TestExecuteBulkInsert_Lines()
        {
            using (var connection = new TaosConnection(builder.ConnectionString))
            {
                connection.Open();
                connection.ChangeDatabase(database);
                string[] lines = {
                    "meters,location=Beijing.Haidian,groupid=2 current=11.8,voltage=221,phase=0.28 1648432611249",
                    "meters,location=Beijing.Haidian,groupid=2 current=13.4,voltage=223,phase=0.29 1648432611250",
                    "meters,location=Beijing.Haidian,groupid=3 current=10.8,voltage=223,phase=0.29 1648432611249",
                    "meters,location=Beijing.Haidian,groupid=3 current=11.3,voltage=221,phase=0.35 1648432611250"
                };
                int result = connection.ExecuteLineBulkInsert(lines);
                Assert.AreEqual(lines.Length, result);
                connection.Close();
            }
        }

        [TestMethod]
        public void TestExecuteBulkInsert_Memory()
        {
            long start = GC.GetTotalMemory(false);
            Console.WriteLine($"{GC.GetTotalMemory(false)}");
            long allinc = 0;
            using (var connection = new TaosConnection(builder.ConnectionString))
            {
                connection.Open();
                connection.ChangeDatabase(database);
                using var cmd = connection.CreateCommand("create table test4(ts timestamp,c1 int,c2 int,c3 int,c4 int,c5 int,c6 int,c7 binary(10),c8 binary(10),c9 binary(10));");
                cmd.ExecuteScalar();
                for (int i = 0; i < 100000; i++)
                {
                    var total = GC.GetTotalMemory(false);
                    using var command = connection.CreateCommand();
                    try
                    {
                        command.CommandText = "insert into test4 values(now,1,2,3,4,5,6,'111111111','222222222','333333333');";
                        command.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail(ex.Message);
                    }
                    var _inc = GC.GetTotalMemory(false) - total;
                    allinc += _inc;
                    Console.WriteLine($"INC:{_inc}");
                }
                connection.Close();
            }
            Console.WriteLine($"开始之前内存:{start},总计新增:{allinc}，当前内存:{GC.GetTotalMemory(false)}");
            GC.Collect();
            Console.WriteLine($"回收后:{GC.GetTotalMemory(false)}");
        }

      
    }
}