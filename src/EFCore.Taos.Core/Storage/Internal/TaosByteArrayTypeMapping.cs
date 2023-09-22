// Copyright (c)  Maikebing. All rights reserved.
// Licensed under the MIT License, See License.txt in the project root for license information.

using System.Data;
using System.Data.Common;

using Microsoft.EntityFrameworkCore.Storage;

namespace IoTSharp.EntityFrameworkCore.Taos.Storage.Internal
{
    internal class TaosByteArrayTypeMapping : ByteArrayTypeMapping
    {
        public TaosByteArrayTypeMapping(string storeType, DbType? dbType = System.Data.DbType.Binary, int? size = null) : base(storeType, dbType, size)
        {
        }
        public override DbParameter CreateParameter(DbCommand command, string name, object value, bool? nullable = null, ParameterDirection direction = ParameterDirection.Input)
        {
            var parameter = (Data.Taos.TaosParameter)base.CreateParameter(command, name, value, nullable, direction);
            parameter.TaosType = Data.Taos.TaosType.Text;
            return parameter;
        }
    }
}