using IoTSharp.Data.Taos;
using IoTSharp.EntityFrameworkCore.Taos;

using Microsoft.EntityFrameworkCore;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace TaosADODemo
{
    [Taos("sensor", true)]
    public class Sensor
    {
        [TaosColumn("tableName", TaosDataType.NCHAR, 50, isTableName: true)]
        public string tableName { get; set; }
        //[Key]
        [TaosColumn("ts", TaosDataType.TIMESTAMP)]
        public DateTime ts { get; set; }

        [TaosColumn("productCode", TaosDataType.NCHAR, 50, true)]
        public string productCode { get; set; }

        [TaosColumn("deviceCode", TaosDataType.NCHAR, 50, true)]
        public string deviceCode { get; set; }
        [TaosColumn("propertyCode", TaosDataType.NCHAR, 50, true)]
        public string propertyCode { get; set; }
        [TaosColumn("content", TaosDataType.NCHAR, 50)]
        public string content { get; set; }
        [TaosColumn("v", TaosDataType.DOUBLE)]
        public double? value { get; set; }
        [TaosColumn("pm25", TaosDataType.INT)]
        public int pm25 { get; set; }
        [TaosColumn("degree", TaosDataType.DOUBLE)]
        public double degree { get; set; }
    }

    [Taos("DeviceData", true)]
    public class DeviceData
    {
        [TaosColumn("stb", TaosDataType.NCHAR, 100, isTableName: true)]
        public string SubTableName { get; set; }
        [TaosColumn("id", TaosDataType.VARCHAR, 100)]
        public string Identifier { get; set; }
        [TaosColumn("product", TaosDataType.NCHAR, 100, true)]
        public string ProductCode { get; internal set; }
        [TaosColumn("device", TaosDataType.NCHAR, 100, true)]
        public string DeviceCode { get; set; }
        [TaosColumn("property", TaosDataType.NCHAR, 100, true)]
        public string PropertyCode { get; set; }
        [TaosColumn("v", TaosDataType.DOUBLE, 100)]
        public double? Data { get; set; }
        /// <summary>
        /// 用于存储非数值内容
        /// </summary>
        [TaosColumn("cxt", TaosDataType.NCHAR, 500)]
        public string Content { get; set; }
        /// <summary>
        /// 必须utc 时间
        /// </summary>
        [TaosColumn("ts", TaosDataType.TIMESTAMP)]
        public DateTime Time { get; set; }
    }


    public class TaosContext : DbContext
    {
        public TaosContext(DbContextOptions options) : base(options)
        {

        }
        //public DbSet<Sensor> Sensor { get; set; }
        public DbSet<DeviceData> DeviceData { get; set; }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
        }
        //protected override void OnModelCreating(ModelBuilder modelBuilder)
        //{
        //    modelBuilder.Entity<Sensor>();
        //}
    }
}
