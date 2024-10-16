using Carbunql.Analysis;
using IoTSharp.Data.Taos;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityFrameworkCore.Taos.Tests
{
    [TestClass]
    public class SqModelTest
    {
        [TestMethod]
        public void TestExecuteBulkInsert_Json()
        {
            const string SQLDemo1 = "";
            const string SQLDemo2 = @"SELECT DISTINCT tbname,point_code,pol_code,station_code,station_name/* 站点名 */
    ,station_uid,longitude,latitude,mn,dev_code,pol_name,pol_unit,pol_id FROM st_pmcm WHERE point_code = @p1;";
            const string SQLDemo3 = "SELECT * FROM bytable WHERE t1 = @t1 AND t2 LIKE @t11 LIMIT @t3;";
           // const string SQLDemo4 = "INSERT INTO t_status USING st_status (status_key) TAGS ('s1') (status_time, val) VALUES (@x1, @x2);";
            var s1 = SelectQueryParser.Parse(SQLDemo1);
            Assert.AreEqual(0, ((int?)s1.Parameters?.Count).GetValueOrDefault());
            var s2 = SelectQueryParser.Parse(SQLDemo2);
            Assert.AreEqual(5, ((int?)s2.Parameters?.Count).GetValueOrDefault());

            Assert.AreEqual("@p1", s2.Parameters?.Keys.ToArray()[0]);

            var pn2 = s2.Parameters;
            Assert.AreEqual(1, pn2.Count);
            Assert.AreEqual("@p1", pn2.Keys.ToArray()[0]);
            var s3 = SelectQueryParser.Parse(SQLDemo3);
            Assert.AreEqual(7, s3.Parameters?.Count);
            Assert.AreEqual("@t1", s3.Parameters?.ToArray()[1].Key);
            Assert.AreEqual("@t11", s3.Parameters?.ToArray()[3].Key);
        //    Assert.AreEqual(" LIMIT ",s3.);
            //Assert.AreEqual("@t3", s3[5].OriginalText);
            //Assert.AreEqual(";", s3[6].OriginalText);
            //Assert.AreEqual("SELECT * FROM bytable WHERE t1 = ? AND t2 LIKE ? LIMIT ?;", s3.CommandText);
            //var pn3 = s3.ParameterNames;
            //Assert.AreEqual(3, pn3.Length);
            //Assert.AreEqual("@t1", pn3[0]);
            //Assert.AreEqual("@t11", pn3[1]);
            //Assert.AreEqual("@t3", pn3[2]);
            //var s4 = StatementObject.ResolveCommandText(SQLDemo4);
            //Assert.AreEqual(7, s4.Count);
            //Assert.AreEqual("INSERT INTO t_status USING st_status (status_key) TAGS (", s4[0].OriginalText);
            //Assert.AreEqual("'s1'", s4[1].OriginalText);
            //Assert.AreEqual(") (status_time, val) VALUES (", s4[2].OriginalText);
            //Assert.AreEqual("@x1", s4[3].OriginalText);
            //Assert.AreEqual(", ", s4[4].OriginalText);
            //Assert.AreEqual("@x2", s4[5].OriginalText);
            //Assert.AreEqual(");", s4[6].OriginalText);
            //Assert.AreEqual("INSERT INTO t_status USING st_status (status_key) TAGS ('s1') (status_time, val) VALUES (?, ?);", s4.CommandText);
            //var pn4 = s4.ParameterNames;
            //Assert.AreEqual(2, pn4.Length);
            //Assert.AreEqual("@x1", pn4[0]);
            //Assert.AreEqual("@x2", pn4[1]);
           
            var s5 = SelectQueryParser.Parse("insert into #subtable using stable tags($t1,$t2,$t3,$t4,$t5,$t6,$t7,$t8,$t9,$t10,$t11,$t12,$t13) values (@t1,@t2,@t3,@t4,@t5,@t6,@t7,@t8,@t9,@t10,@t11,@t12,@t13,@t14)");
            Assert.AreEqual(14, s5.Parameters?.Count);
            Assert.AreEqual("#subtable", s5.Parameters?.ToArray()[0].Key);
            Assert.AreEqual(13, s5.Parameters?.Count(k=>k.Key.StartsWith('$')));
         //   Assert.AreEqual("insert into ? using stable tags(?,?,?,?,?,?,?,?,?,?,?,?,?) values (?,?,?,?,?,?,?,?,?,?,?,?,?,?)", s4.CommandText);

        }
    }
}
