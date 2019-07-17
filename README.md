# Maikebing.EntityFrameworkCore.Taos

## ���
 

Entity, Framework, EF, Core, Data, O/RM, entity-framework-core,TDengine

Maikebing.Data.Taos  ��һ������TDengine ��RESTful Connector������ADO.Net �ṩ���� �⽫������ͨ��.Net Core ����TDengine���ݿ⡣

Maikebing.EntityFrameworkCore.Taos ��һ��Entity Framework Core ���ṩ���� ����Maikebing.Data.Taos������ �˹�����δ������ɣ� ��ʱ����ʹ�á�

## ״̬

[![Build status](https://ci.appveyor.com/api/projects/status/8krjmvsoiilo2r10?svg=true)](https://ci.appveyor.com/project/MaiKeBing/maikebing-entityframeworkcore-taos)
[![License](https://img.shields.io/github/license/maikebing/Maikebing.EntityFrameworkCore.Taos.svg)](https://github.com/maikebing/Maikebing.EntityFrameworkCore.Taos/blob/master/LICENSE)
![Nuget](https://img.shields.io/nuget/v/Maikebing.Data.Taos.svg)



##  ��װʹ��:
 ` Install-Package Maikebing.Data.Taos -Version 1.0.0 `

## Maikebing.Data.Taos ʹ�÷���

```C#

string database = "db_" + DateTime.Now.ToString("yyyyMMddHHmmss");
var builder = new TaosConnectionStringBuilder()
{
    DataSource = "http://td.gitclub.cn/rest/sql",
    DataBase = database,
    Token = "cm9vdDp0YW9zZGF0YQ=="
};
using (var connection = new TaosConnection(builder.ConnectionString))
{
    connection.Open();
    Console.WriteLine("create {0} {1}", database, connection.CreateCommand($"create database {database};").ExecuteNonQuery());
    Console.WriteLine("create table t {0} {1}", database, connection.CreateCommand($"create table {database}.t (ts timestamp, cdata int);").ExecuteNonQuery());
    Console.WriteLine("insert into t values  {0}  ", connection.CreateCommand($"insert into {database}.t values ('{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ms")}', 10);").ExecuteNonQuery());
    Console.WriteLine("insert into t values  {0} ", connection.CreateCommand($"insert into {database}.t values ('{DateTime.Now.AddMonths(1).ToString("yyyy-MM-dd HH:mm:ss.ms")}', 20);").ExecuteNonQuery());
    var cmd_select = connection.CreateCommand();
    cmd_select.CommandText = $"select * from {database}.t";
    var reader = cmd_select.ExecuteReader();
    List<Dictionary<string, object>> valuePairs = new List<Dictionary<string, object>>();
    while (reader.Read())
    {
        Dictionary<string, object> pairs = new Dictionary<string, object>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            pairs.Add(reader.GetName(i), reader.GetValue(i));
        }
        valuePairs.Add(pairs);
    }
    Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(valuePairs));
    Console.WriteLine("DROP TABLE  {0} {1}", database, connection.CreateCommand($"DROP TABLE  {database}.t;").ExecuteNonQuery());
    Console.WriteLine("DROP DATABASE {0} {1}", database, connection.CreateCommand($"DROP DATABASE   {database}").ExecuteNonQuery());
    Console.ReadKey();
    connection.Close();
}
```