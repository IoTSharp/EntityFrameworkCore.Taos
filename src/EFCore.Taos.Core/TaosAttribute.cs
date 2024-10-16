using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IoTSharp.EntityFrameworkCore.Taos
{
    [Serializable]
    public class TaosAttribute : Attribute
    {

        /// <summary>
        /// 表名
        /// </summary>
        public string TableName { get; }
        public bool IsSuperTable { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="superTableName">超级表名</param>     
        public TaosAttribute(string tableName, bool isSuperTable = false)
        {
            TableName = tableName;
            IsSuperTable = isSuperTable;
        }
    }
    [Serializable]
    public class TaosColumnAttribute : Attribute
    {
        /// <summary>
        /// 列名字
        /// </summary>
        public string ColumnName { get; set; }
        /// <summary>
        /// 列类型
        /// </summary>
        public TaosDataType ColumnType { get; set; }
        public int ColumnLength { get; set; }
        /// <summary>
        /// 是否标签
        /// </summary>
        public bool IsTag { get; set; } = false;
        /// <summary>
        /// 是否为表名 超级表时有用
        /// </summary>
        public bool IsSubTableName { get; set; }
        public TaosColumnAttribute(string columnName, TaosDataType columnType, int columnLength = 0, bool isTag = false, bool isTableName = false)
        {
            ColumnName = columnName;
            ColumnType = columnType;
            ColumnLength = columnLength;
            IsTag = isTag;
            IsSubTableName = isTableName;

            switch (columnType)
            {
                case TaosDataType.TIMESTAMP:
                    ColumnLength = 8;
                    break;
                case TaosDataType.INT:
                    ColumnLength = 4;
                    break;
                case TaosDataType.INT_UNSIGNED:
                    ColumnLength = 8;
                    break;
                case TaosDataType.BIGINT:
                    ColumnLength = 8;
                    break;
                case TaosDataType.BIGINT_UNSIGNED:
                    ColumnLength = 8;
                    break;
                case TaosDataType.FLOAT:
                    ColumnLength = 4;
                    break;
                case TaosDataType.DOUBLE:
                    ColumnLength = 8;
                    break;
                case TaosDataType.BINARY:
                    break;
                case TaosDataType.SMALLINT:
                    ColumnLength = 2;
                    break;
                case TaosDataType.SMALLINT_UNSIGNED:
                    ColumnLength = 2;
                    break;
                case TaosDataType.TINYINT:
                    ColumnLength = 1;
                    break;
                case TaosDataType.TINYINT_UNSIGNED:
                    ColumnLength = 1;
                    break;
                case TaosDataType.BOOL:
                    ColumnLength = 1;
                    break;
                case TaosDataType.NCHAR:
                    break;
                case TaosDataType.JSON:
                    IsTag = true;
                    break;
                case TaosDataType.VARCHAR:
                    break;
                case TaosDataType.GEOMETRY:
                    break;
                case TaosDataType.VARBINARY:
                    break;
                default:
                    break;
            }

        }
    }

    /// <summary>
    /// 涛思时序数据库10种数据类型
    /// https://docs.taosdata.com/taos-sql/data-type/
    /// </summary>
    public enum TaosDataType
    {
        /// <summary>
        ///	8	时间戳。缺省精度毫秒，可支持微秒和纳秒，详细说明见上节。
        /// </summary>
        TIMESTAMP,
        /// <summary>
        /// 4	整型，范围 [-2^31, 2^31-1]
        /// </summary>
        INT,
        /// <summary>
        /// 4	无符号整数，[0, 2^32-1]
        /// </summary>
        INT_UNSIGNED,
        /// <summary>
        /// 8	长整型，范围 [-2^63, 2^63-1]
        /// </summary>
        BIGINT,
        /// <summary>
        /// 8	长整型，范围 [0, 2^64-1]
        /// </summary>
        BIGINT_UNSIGNED,
        /// <summary>
        /// 4	浮点型，有效位数 6-7，范围 [-3.4E38, 3.4E38]
        /// </summary>
        FLOAT,
        /// <summary>
        /// 8	双精度浮点型，有效位数 15-16，范围 [-1.7E308, 1.7E308]
        /// </summary>
        DOUBLE,
        /// <summary>
        /// 自定义	记录单字节字符串，建议只用于处理 ASCII 可见字符，中文等多字节字符需使用 NCHAR
        /// </summary>
        BINARY,
        /// <summary>
        /// 2	短整型， 范围 [-32768, 32767]
        /// </summary>
        SMALLINT,
        /// <summary>
        /// 2	无符号短整型，范围 [0, 65535]
        /// </summary>
        SMALLINT_UNSIGNED,
        /// <summary>
        /// 1	单字节整型，范围 [-128, 127]
        /// </summary>
        TINYINT,
        /// <summary>
        /// 1	无符号单字节整型，范围 [0, 255]
        /// </summary>
        TINYINT_UNSIGNED,
        /// <summary>
        /// 1	布尔型，{true, false}
        /// </summary>
        BOOL,
        /// <summary>
        /// 自定义	记录包含多字节字符在内的字符串，如中文字符。每个 NCHAR 字符占用 4 字节的存储空间。
        /// 字符串两端使用单引号引用，字符串内的单引号需用转义字符 \'。
        /// NCHAR 使用时须指定字符串大小，类型为 NCHAR(10) 的列表示此列的字符串最多存储 10 个 NCHAR 字符。
        /// 如果用户字符串长度超出声明长度，将会报错。
        /// </summary>
        NCHAR,
        /// <summary>
        /// JSON 数据类型， 只有 Tag 可以是 JSON 格式
        /// </summary>
        JSON,
        /// <summary>
        /// 自定义	BINARY 类型的别名
        /// </summary>
        VARCHAR,
        /// <summary>
        /// 自定义	几何类型
        /// </summary>
        GEOMETRY,
        /// <summary>
        /// 自定义	可变长的二进制数据
        /// </summary>
        VARBINARY
    }
}
