// Copyright (c)  Maikebing. All rights reserved.
// Licensed under the MIT License, See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.Common;

using IoTSharp.Data.Taos;

using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;

namespace IoTSharp.EntityFrameworkCore.Taos.Storage.Internal
{
    internal class TaosStringTypeMapping : StringTypeMapping
    {
        public TaosStringTypeMapping(string storeType, DbType? dbType, bool unicode = false, int? size = null) : base(storeType, dbType, unicode, size)
        {
        }
        public override DbParameter CreateParameter(DbCommand command, string name, object value, bool? nullable = null, ParameterDirection direction = ParameterDirection.Input)
        {
            var cmd = command as IoTSharp.Data.Taos.TaosCommand;
            //var parameter = (Data.Taos.TaosParameter)base.CreateParameter(command, name, value, nullable, direction);

            var parameter = command.CreateParameter();
            parameter.Direction = direction;
            parameter.ParameterName = name;

            if (direction.HasFlag(ParameterDirection.Input))
            {
                value = NormalizeEnumValue(value);

                if (Converter != null)
                {
                    value = Converter.ConvertToProvider(value);
                }

                parameter.Value = value ?? DBNull.Value;
            }

            if (nullable.HasValue)
            {
                Check.DebugAssert(
                    nullable.Value
                    || !direction.HasFlag(ParameterDirection.Input)
                    || value != null,
                    "Null value in a non-nullable input parameter");

                parameter.IsNullable = nullable.Value;
            }

            if (DbType.HasValue)
            {
                parameter.DbType = DbType.Value;
            }

            ConfigureParameter(parameter);

            ((TaosParameter)parameter).TaosType = Data.Taos.TaosType.Text;
            return parameter;

        }
        private object? NormalizeEnumValue(object? value)
        {
            // When Enum column is compared to constant the C# compiler put a constant of integer there
            // In some unknown cases for parameter we also see integer value.
            // So if CLR type is enum we need to convert integer value to enum value
            if (value?.GetType().IsInteger() == true
                && ClrType.UnwrapNullableType().IsEnum)
            {
                return Enum.ToObject(ClrType.UnwrapNullableType(), value);
            }

            // When Enum is cast manually our logic of removing implicit convert gives us enum value here
            // So if CLR type is integer we need to convert enum value to integer value
            if (value?.GetType().IsEnum == true
                && ClrType.UnwrapNullableType().IsInteger())
            {
                return Convert.ChangeType(value, ClrType);
            }

            return value;
        }
    }
}