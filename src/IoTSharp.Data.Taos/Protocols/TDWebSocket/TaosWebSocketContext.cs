using IoTSharp.Data.Taos.Driver;
using IoTSharp.Data.Taos.Protocols.TDRESTful;

using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using TDengineDriver;

namespace IoTSharp.Data.Taos.Protocols.TDWebSocket
{
    internal class TaosWebSocketContext : ITaosContext
    {
        private readonly int _rows;
        private WSQueryRsp _meta;
        private readonly DateTime _dt1970;
        private IntPtr ptr = IntPtr.Zero;
        int _index = -1;
        private int numOfRows;
        private int numOfCols;
        private List<SColumnInfoData> lstpColInfoData = new List<SColumnInfoData>();
        private int version;
        private int dataLen;
        private uint hasColumnInfo;
        private long groupId;
        public TaosWebSocketContext(TaosWSResult tr)
        {
            _dt1970 = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            if (tr.meta != null)//query 
            {
                _meta = tr.meta;
                AffectRows = tr.meta.affected_rows;
                FieldCount = tr.meta.fields_count;
                if (!_meta.is_update && tr.data.Length > 0)
                {
                    blockDecode(tr.data);
                }
                _rows = tr.rows;
                if (numOfRows != tr.rows)
                {
                    TaosException.ThrowExceptionForRC(new TaosErrorResult() { Code = -1, Error = "数据解析格式异常。" });
                }
            }
            else if (tr.StmtExec != null)//insert 
            {
                AffectRows = tr.StmtExec.affected;
            }
        }
        public int AffectRows { get; set; }
        public int FieldCount { get; set; }
        public bool CloseConnection { get; set; }


        const uint NBIT = (3u);
        //#define BitmapLen(_n) 
        int BitmapLen(int _n)
        {
            return (((_n) + ((1 << (byte)NBIT) - 1)) >> (byte)NBIT);
        }
        //https://github.com/taosdata/TDengine/blob/main/source/common/src/tdatablock.c#L2288
        void blockDecode(byte[] data)
        {
            //请求耗时(uint64)+result_id (uint64)
            ptr = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, ptr, data.Length);
            int pStart = 0;

            var _block_timing = Marshal.ReadInt64(ptr, pStart);
            Debug.WriteLine($"耗时:{TimeSpan.FromMilliseconds(_block_timing)}");
            pStart += sizeof(Int64);
            var _result_id = Marshal.ReadInt64(ptr, pStart);
            Debug.WriteLine($"请求ID:{_result_id}");
            pStart += sizeof(Int64);
            //int32_t version = *(int32_t*)pStart;
            //pStart += sizeof(int32_t);
            //ASSERT(version == 1);
            version = Marshal.ReadInt32(ptr, pStart);
            Debug.WriteLine($"版本:{version}");
            pStart += sizeof(Int32);
            //// total length sizeof(int32_t)
            //int32_t dataLen = *(int32_t*)pStart;
            //pStart += sizeof(int32_t);
            dataLen = Marshal.ReadInt32(ptr, pStart);
            pStart += sizeof(Int32);
            Debug.WriteLine($"数据长度:{dataLen}");

            //// total rows sizeof(int32_t)
            //int32_t numOfRows = *(int32_t*)pStart;
            //pStart += sizeof(int32_t);
            numOfRows = Marshal.ReadInt32(ptr, pStart);
            pStart += sizeof(Int32);
            Debug.WriteLine($"行数:{numOfRows}");
            //// total columns sizeof(int32_t)
            //int32_t numOfCols = *(int32_t*)pStart;
            //pStart += sizeof(int32_t);
            numOfCols = Marshal.ReadInt32(ptr, pStart);
            pStart += sizeof(Int32);
            Debug.WriteLine($"列数:{numOfCols}");

            //// has column info segment
            //int32_t flagSeg = *(int32_t*)pStart;
            //int32_t hasColumnInfo = (flagSeg >> 31);
            //pStart += sizeof(int32_t);
            var flagSeg = Marshal.ReadInt32(ptr, pStart);
            hasColumnInfo = ((uint)(flagSeg >> 31));
            pStart += sizeof(Int32);


            //// group id sizeof(uint64_t)
            //pBlock->info.groupId = *(uint64_t*)pStart;
            //pStart += sizeof(uint64_t);
            groupId = Marshal.ReadInt64(ptr, pStart);
            pStart += sizeof(Int64);


            lstpColInfoData = new List<SColumnInfoData>();
            for (int i = 0; i < numOfCols; ++i)
            {
                SColumnInfoData pColInfoData = new SColumnInfoData();
                pColInfoData.info.type = Marshal.ReadByte(ptr, pStart);
                pStart += sizeof(byte);
                pColInfoData.info.bytes = Marshal.ReadInt32(ptr, pStart);
                pStart += sizeof(Int32);
                lstpColInfoData.Add(pColInfoData);
            }

            int[] colLen = new int[numOfCols];
            Marshal.Copy(IntPtr.Add(ptr, pStart), colLen, 0, numOfCols);
            pStart += sizeof(Int32) * numOfCols;

            for (int i = 0; i < numOfCols; ++i)
            {
                // colLen[i] = BitConverter.ToInt32(BitConverter.GetBytes(colLen[i]).Reverse().ToArray(),0);
                Debug.Assert(colLen[i] >= 0);
                SColumnInfoData pColInfoData = lstpColInfoData[i];
                if (pColInfoData.info.IS_VAR_DATA_TYPE())
                {
                    pColInfoData.varmeta.offset = new Int32[numOfRows];
                    Marshal.Copy(IntPtr.Add(ptr, pStart), pColInfoData.varmeta.offset, 0, numOfRows);
                    pStart += sizeof(Int32) * numOfRows;
                    if (colLen[i] > 0 && pColInfoData.varmeta.allocLen < colLen[i])
                    {
                        pColInfoData.varmeta.allocLen = colLen[i];
                    }
                    pColInfoData.varmeta.length = colLen[i];
                }
                else
                {
                    int mlen = BitmapLen(numOfRows);
                    pColInfoData.nullbitmap = new byte[mlen];
                    Marshal.Copy(IntPtr.Add(ptr, pStart), pColInfoData.nullbitmap, 0, mlen);
                    pStart += mlen;
                }

                if (colLen[i] > 0)
                {
                    pColInfoData.pData = IntPtr.Add(ptr, pStart);
                    pColInfoData.pDataLen = colLen[i];
                }

                // TODO
                // setting this flag to true temporarily so aggregate function on stable will
                // examine NULL value for non-primary key column
                pColInfoData.hasNull = true;
                pStart += colLen[i];


            }
        }
        public void Dispose()
        {
            Marshal.FreeHGlobal(ptr);
        }

        public bool GetBoolean(int ordinal)
        {
            return GetByte(ordinal) == 0 ? false : true;
        }

        public byte GetByte(int ordinal)
        {
            var dt = GetValuePtr(ordinal);
            return Marshal.ReadByte(dt.data);
        }

        public long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            var dt = GetValuePtr(ordinal);
            if (dataOffset >= dt.len || dataOffset + bufferOffset + length > dt.len || bufferOffset > dt.len)
            {
                throw new IndexOutOfRangeException($"Data Len:{dt.len}, but dataOffset={dataOffset},bufferOffset={bufferOffset} and length={length}");
            }
            Marshal.Copy(IntPtr.Add(dt.data, (int)dataOffset), buffer, bufferOffset, length);
            return buffer.Length;
        }

        public DateTime GetDataTime(int ordinal)
        {
            var dt = GetValuePtr(ordinal);
            return GetDateTimeFrom(dt.data, dt.precision);
        }

        public int GetErrorNo()
        {
            return _meta.code;
        }

        public int GetFieldSize(int ordinal)
        {
            var info = lstpColInfoData[ordinal].info;
            return info.IS_VAR_DATA_TYPE() ? info.bytes - 2 : info.bytes;
        }



        public Type GetFieldType(int ordinal)
        {
            var tx = (TDengineDataType)lstpColInfoData[ordinal].info.type;
            return tx.ToCrlType();
        }

        public string GetName(int ordinal)
        {
            return _meta.fields_names[ordinal];
        }

        public int GetOrdinal(string name)
        {
            return _meta.fields_names.IndexOf(name);
        }

        public Stream GetStream(int ordinal)
        {
            var dt = GetValuePtr(ordinal);
            var buffer = new byte[dt.len];
            Marshal.Copy(dt.data, buffer, 0, dt.len);
            return new MemoryStream(buffer);
        }

        public TimeSpan GetTimeSpan(int ordinal)
        {
            var dt = GetValuePtr(ordinal);
            var val = Marshal.ReadInt64(dt.data);
            switch (dt.precision)
            {
                /*
                * ticks为100纳秒，必须乘以10才能达到微秒级的区分度
                * 1秒s    = 1000毫秒ms
                * 1毫秒ms = 1000微秒us
                * 1微秒us = 1000纳秒ns
                * 因此， 1毫秒ms = 1000000纳秒ns = 10000ticks
                */
                case TSDB_TIME_PRECISION.TSDB_TIME_PRECISION_NANO:
                    return TimeSpan.FromTicks(val / 100);

                case TSDB_TIME_PRECISION.TSDB_TIME_PRECISION_MICRO:
                    return TimeSpan.FromTicks(val * 10);

                case TSDB_TIME_PRECISION.TSDB_TIME_PRECISION_MILLI:
                default:
                    return TimeSpan.FromTicks(val * 10000);
            }
        }


        public object GetValue(int ordinal)
        {
            object result = null;
            if (ordinal >= 0 && ordinal < FieldCount)
            {
                var dr = GetValuePtr(ordinal);
                IntPtr data = dr.data;
                short len = dr.len;
                if (data != IntPtr.Zero)
                {
                    switch (dr.type)
                    {
                        case TDengineDataType.TSDB_DATA_TYPE_BOOL:
                            bool v1 = Marshal.ReadByte(data) == 0 ? false : true;
                            result = v1;
                            break;

                        case TDengineDataType.TSDB_DATA_TYPE_TINYINT:
                            sbyte v2s = (sbyte)Marshal.ReadByte(data);
                            result = v2s;
                            break;

                        case TDengineDataType.TSDB_DATA_TYPE_UTINYINT:
                            byte v2 = Marshal.ReadByte(data);
                            result = v2;
                            break;

                        case TDengineDataType.TSDB_DATA_TYPE_SMALLINT:
                            short v3 = Marshal.ReadInt16(data);
                            result = v3;
                            break;

                        case TDengineDataType.TSDB_DATA_TYPE_USMALLINT:
                            ushort v12 = (ushort)Marshal.ReadInt16(data);
                            result = v12;
                            break;

                        case TDengineDataType.TSDB_DATA_TYPE_INT:
                            int v4 = Marshal.ReadInt32(data);
                            result = v4;
                            break;

                        case TDengineDataType.TSDB_DATA_TYPE_UINT:
                            uint v13 = (uint)Marshal.ReadInt32(data);
                            result = v13;
                            break;

                        case TDengineDataType.TSDB_DATA_TYPE_BIGINT:
                            long v5 = Marshal.ReadInt64(data);
                            result = v5;
                            break;

                        case TDengineDataType.TSDB_DATA_TYPE_UBIGINT:
                            ulong v14 = (ulong)Marshal.ReadInt64(data);
                            result = v14;
                            break;

                        case TDengineDataType.TSDB_DATA_TYPE_FLOAT:
                            float v6 = (float)Marshal.PtrToStructure(data, typeof(float));
                            result = v6;
                            break;

                        case TDengineDataType.TSDB_DATA_TYPE_DOUBLE:
                            double v7 = (double)Marshal.PtrToStructure(data, typeof(double));
                            result = v7;
                            break;

                        case TDengineDataType.TSDB_DATA_TYPE_VARCHAR:
                            {

                                byte[] buffer = new byte[len];
                                Marshal.Copy(data, buffer, 0, len);
                                string v8 = Encoding.UTF8.GetString(buffer);
                                result = v8?.RemoveNull();
                            }
                            break;

                        case TDengineDataType.TSDB_DATA_TYPE_TIMESTAMP:
                            {
                                result = GetDateTimeFrom(data, (TSDB_TIME_PRECISION)_meta.precision);
                            }
                            break;

                        case TDengineDataType.TSDB_DATA_TYPE_JSON:
                        case TDengineDataType.TSDB_DATA_TYPE_NCHAR:
                            {
                                string v10 = string.Empty;
                                int contentLength = len;
                                if (contentLength > 0)// https://github.com/maikebing/Maikebing.EntityFrameworkCore.Taos/issues/99
                                {
                                    byte[] bf = new byte[contentLength];
                                    Marshal.Copy(data, bf, 0, contentLength);

                                    if (bf.IsUTF8Bytes() || (bf[0] == 0xEF && bf[1] == 0xBB && bf[2] == 0xBF))
                                    {
                                        v10 = System.Text.Encoding.UTF32.GetString(bf)?.RemoveNull();
                                    }
                                    else
                                    {
                                        v10 = System.Text.Encoding.GetEncoding(936).GetString(bf)?.RemoveNull();
                                    }
                                }
                                result = v10;
                            }
                            break;

                        case TDengineDataType.TSDB_DATA_TYPE_NULL:
                            result = null;
                            break;
                    }
                }
                else
                {
                    result = null;
                }
            }
            return result;
        }

        private WSDataRow GetValuePtr(int ordinal)
        {
            var col = lstpColInfoData[ordinal];
            int offset = 0;
            short len = 0;
            IntPtr data = IntPtr.Zero;
            IntPtr rowdata = col.pData;
            if (col.info.IS_VAR_DATA_TYPE())
            {
                offset = col.varmeta.offset[_index];
                if (offset >= 0)
                {
                    IntPtr rowptr = IntPtr.Add(rowdata, offset);
                    len = Marshal.ReadInt16(rowptr, 0);
                    data = IntPtr.Add(rowptr, sizeof(Int16));
                }
                else
                {
                    len = 0;
                    data = IntPtr.Zero;
                }

            }
            else
            {
                offset = col.info.bytes * _index;
                len = (short)col.info.bytes;
                data = IntPtr.Add(rowdata, offset);
            }
            return new WSDataRow((TDengineDataType)col.info.type, data, len, col.info.precision);
        }

        private DateTime GetDateTimeFrom(IntPtr data, TSDB_TIME_PRECISION _dateTimePrecision)
        {
            var val = Marshal.ReadInt64(data);
            //double tsp;
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
                    val /= 100;
                    break;

                case TSDB_TIME_PRECISION.TSDB_TIME_PRECISION_MICRO:
                    val *= 10;
                    break;

                case TSDB_TIME_PRECISION.TSDB_TIME_PRECISION_MILLI:
                default:
                    val *= 10000;
                    break;
            }
            var v9 = _dt1970.AddTicks(val);
            return v9.ToLocalTime();
        }
        public TaosException LastException()
        {
            return new TaosException(new TaosErrorResult() { Code = _meta.code, Error = _meta.message });
        }

        public bool Read()
        {
            _index++;
            return _index < _rows;
        }
    }
}