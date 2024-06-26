﻿using System;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using Sigil;
using Thomas.Database.Cache;
using Label = Sigil.Label;

namespace Thomas.Database
{
    internal partial class DatabaseCommand
    {
        #region delegate builder

        private static MethodInfo GetMethodInfo(Type type, in SqlProvider provider)
        {
            return type.Name switch
            {
                "Int16" => GetInt16,
                "Int32" => GetInt32,
                "Int64" => GetInt64,
                "UInt16" => GetInt16,
                "UInt32" => GetInt32,
                "UInt64" => GetInt64,
                "Byte" => GetByte,
                "SByte" => GetByte,
                "Float" => GetFloat,
                "Double" => GetDouble,
                "Decimal" => GetDecimal,
                "Boolean" => GetBoolean,
                "String" => GetString,
                "DateTime" => GetDateTime,
                "Guid" => GetGuid,
                "Char" => GetChar,
                "Byte[]" => GetBytes,

                //specific handlers
                "BitArray" when provider == SqlProvider.PostgreSql => GetBoolean,
                "Object" when provider == SqlProvider.PostgreSql => GetBoolean,
                _ => null!
            };
        }

        private static readonly Type ParserType = Type.GetType("Thomas.Database.DatabaseCommand, Thomas.Database")!;
        private static readonly MethodInfo ReadStreamMethod = ParserType.GetMethod(nameof(ReadStream), BindingFlags.NonPublic | BindingFlags.Static)!;

        internal static readonly Type ConvertType = typeof(Convert);
        private static readonly MethodInfo ChangeTypeMethod = ConvertType.GetMethod("ChangeType", new[] { typeof(object), typeof(Type) })!;
        private static readonly ConstructorInfo GuidConstructorByteArray = typeof(Guid).GetConstructor(new Type[] { typeof(byte[]) })!;
        private static readonly ConstructorInfo GuidConstructorString = typeof(Guid).GetConstructor(new Type[] { typeof(string) })!;

        private static readonly Type DataReaderType = Type.GetType("System.Data.Common.DbDataReader, System.Data.Common")!;
        private static readonly MethodInfo IsDBNull = DataReaderType.GetMethod("IsDBNull")!;
        private static readonly MethodInfo GetInt16 = DataReaderType.GetMethod("GetInt16")!;
        private static readonly MethodInfo GetInt32 = DataReaderType.GetMethod("GetInt32")!;
        private static readonly MethodInfo GetInt64 = DataReaderType.GetMethod("GetInt64")!;
        private static readonly MethodInfo GetString = DataReaderType.GetMethod("GetString")!;
        private static readonly MethodInfo GetByte = DataReaderType.GetMethod("GetByte")!;
        private static readonly MethodInfo GetFloat = DataReaderType.GetMethod("GetFloat")!;
        private static readonly MethodInfo GetDouble = DataReaderType.GetMethod("GetDouble")!;
        private static readonly MethodInfo GetDecimal = DataReaderType.GetMethod("GetDecimal")!;
        private static readonly MethodInfo GetBoolean = DataReaderType.GetMethod("GetBoolean")!;
        private static readonly MethodInfo GetDateTime = DataReaderType.GetMethod("GetDateTime")!;
        private static readonly MethodInfo GetGuid = DataReaderType.GetMethod("GetGuid")!;
        private static readonly MethodInfo GetChar = DataReaderType.GetMethod("GetChar")!;
        private static readonly MethodInfo GetBytes = DataReaderType.GetMethod("GetBytes")!;

        private static Func<DbDataReader, T> GetParserTypeDelegate<T>(in DbDataReader reader, in int queryKey, in SqlProvider provider, in int bufferSize = 0)
        {
            var typeResult = typeof(T);
            var key = (queryKey * 23) + typeResult.GetHashCode();

            if (CacheTypeParser<T>.TryGet(in key, out Func<DbDataReader, T> parser))
                return parser;

            Span<PropertyTypeInfo> columnInfoCollection = GetColumnMap(typeResult, reader);

            Func<DbDataReader, T> @delegate = null;

            if (IsReadonlyRecordType(in typeResult))
                @delegate = GenerateEmitterForReadonlyRecord<T>(in typeResult, in provider, in bufferSize, in columnInfoCollection);
            else
                @delegate = GenerateEmitterBySetters<T>(in typeResult, in provider, in bufferSize, in columnInfoCollection);

            CacheTypeParser<T>.Set(key, @delegate);
            return @delegate;
        }

        //support only class with public setters and parameterless constructor 
        private static Func<DbDataReader, T> GenerateEmitterBySetters<T>(in Type typeResult, in SqlProvider provider, in int bufferSize, in Span<PropertyTypeInfo> columnInfoCollection)
        {
            if (typeResult.GetConstructor(Type.EmptyTypes) == null)
                throw new InvalidOperationException($"Cannot find parameterless constructor for {typeResult.Name}");

            Emit<Func<DbDataReader, T>> emitter = Emit<Func<DbDataReader, T>>.NewDynamicMethod(typeResult, $"ParseReaderRowTo{typeResult.FullName!.Replace('.', '_')}Class", true, true);

            using Local instance = emitter.DeclareLocal<T>("objectValue", false); // Declare local variable to hold the new instance of T

            emitter.NewObject<T>().StoreLocal(instance); // Store the new instance in the local variable

            int index = 0;

            foreach (var column in columnInfoCollection)
            {
                if (column == null)
                    break;

                Label? notNullLabel = null;
                Label? endLabel = null;

                if (column.AllowNull)
                {
                    emitter.LoadArgument(0)
                                  .LoadConstant(index)
                                  .CallVirtual(IsDBNull);

                    //check if not null
                    notNullLabel = emitter.DefineLabel("notNull" + index);
                    endLabel = emitter.DefineLabel("end" + index);

                    emitter.BranchIfFalse(notNullLabel);

                    // If !IsDBNull returned false
                    emitter.Branch(endLabel);

                    //If !IsDBNull returned true
                    emitter.MarkLabel(notNullLabel);
                }

                emitter.LoadLocal(instance);

                var getMethod = GetMethodInfo(column.Source, in provider) ?? throw new NotSupportedException($"Unsupported type {column.Source.Name}");

                //byte[] To Guid - Oracle
                if (getMethod.Name.Equals("GetBytes", StringComparison.Ordinal) && column.PropertyInfo.PropertyType.Name.Equals("Guid", StringComparison.Ordinal))
                {
                    // Define a local for the byte array
                    using var bufferLocal = emitter.DeclareLocal<byte[]>();

                    // Create a new byte array to hold the GUID
                    emitter.LoadConstant(16) // GUID size
                                        .NewArray<byte>()
                                        .StoreLocal(bufferLocal);

                    // Assume the data is already in the correct position in the reader, and fill the byte array
                    emitter.LoadArgument(0)
                                        .LoadConstant(index)
                                        .LoadConstant(0L)
                                        .LoadLocal(bufferLocal)
                                        .LoadConstant(0)
                                        .LoadConstant(16)
                                        .CallVirtual(GetBytes)
                                        .Pop();

                    emitter.LoadLocal(instance)
                            .LoadLocal(bufferLocal)
                            .NewObject(GuidConstructorByteArray)
                            .Call(column.PropertyInfo!.GetSetMethod(true))
                            .Pop();

                    if (column.AllowNull)
                    {
                        emitter.MarkLabel(endLabel);
                    }

                    index++;
                    continue;
                }
                else if (column.IsLargeObject && getMethod.Name.Equals("GetBytes", StringComparison.Ordinal))
                {
                    emitter.LoadArgument(0)
                        .LoadConstant(index)
                        .LoadConstant(bufferSize > 0 ? bufferSize : 8192)
                        .Call(ReadStreamMethod);
                }
                else
                {
                    emitter.LoadArgument(0)
                                        .LoadConstant(index)
                                        .CallVirtual(getMethod);
                }

                if (column.RequiredConversion)
                {
                    Type propertyType = column.PropertyInfo.PropertyType;
                    Type sourceType = column.Source;
                    Type underlyingType = Nullable.GetUnderlyingType(propertyType);

                    if (IsPrimitiveTypeConversion(propertyType, sourceType))
                    {
                        emitter.Convert(propertyType);
                    }
                    else if (underlyingType == sourceType)
                    {
                        //EmitConvertToNullable<T>(emitter, sourceType);
                        emitter.NewObject(typeof(Nullable<>).MakeGenericType(underlyingType).GetConstructor(new[] { underlyingType }));
                    }
                    else if (propertyType == typeof(Guid) && sourceType == typeof(string))
                    {
                        emitter.NewObject(GuidConstructorString);
                    }
                    else if (column.DataTypeName == "bit" && underlyingType == typeof(bool)) //postgresql
                    {
                        //EmitConvertToNullable<T>(emitter, typeof(bool));
                        emitter.NewObject(typeof(Nullable<>).MakeGenericType(typeof(bool)).GetConstructor(new[] { typeof(bool) }));
                    }
                    else if (column.DataTypeName == "bit" && propertyType == typeof(bool)) //postgresql
                    {
                    }
                    else if (underlyingType != null)
                    {
                        EmitConversionForNullableType(emitter, underlyingType, sourceType, column);
                    }
                    else
                    {
                        EmitConversionForNonNullableType(emitter, propertyType, sourceType, column);
                    }
                }

                if (column.PropertyInfo.PropertyType.IsValueType)
                {
                    emitter.Call(column.PropertyInfo!.GetSetMethod(true));
                }
                else
                {
                    emitter.CallVirtual(column.PropertyInfo!.GetSetMethod(true));
                }

                if (column.AllowNull)
                {
                    emitter.MarkLabel(endLabel);
                }

                index++;
            }

            return emitter.LoadLocal(instance).Return().CreateDelegate();
        }

        private static Func<DbDataReader, T> GenerateEmitterForReadonlyRecord<T>(in Type type, in SqlProvider provider, in int bufferSize, in Span<PropertyTypeInfo> columnInfoCollection)
        {
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).ToArray();
            var constructor = type.GetConstructor(fields.Select(x => x.FieldType).ToArray());

            if (constructor == null)
                throw new InvalidOperationException($"Cannot find readonly record constructor for {type.Name}");

            var emitter = Emit<Func<DbDataReader, T>>.NewDynamicMethod(type, $"ParseReaderRowTo{type.FullName!.Replace('.', '_')}Record", true, true);

            var columns = columnInfoCollection.ToArray();
            var locals = fields.Select(f => emitter.DeclareLocal(f.FieldType)).ToList();

            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                var local = locals[i];

                var column = columns.FirstOrDefault(x => field.Name.Contains($"<{x.Name}>", StringComparison.InvariantCultureIgnoreCase));

                //when column not found we need to set default value
                Label? notNullLabel = emitter.DefineLabel("notNull" + i);
                Label? endLabel = emitter.DefineLabel("end" + i);

                Type underlyingType = Nullable.GetUnderlyingType(field.FieldType);

                if (column.AllowNull)
                {
                    emitter.LoadArgument(0)
                                    .LoadConstant(i)
                                    .CallVirtual(IsDBNull);

                    //check if not null
                    emitter.BranchIfFalse(notNullLabel);

                    //ELSE - handling default value and avoid store default value on null-able types
                    if (underlyingType == null)
                    {
                        EmitDefaultValue(emitter, underlyingType, field);
                        emitter.StoreLocal(local);
                    }

                    //if column is null then skip to end
                    emitter.Branch(endLabel);
                    emitter.MarkLabel(notNullLabel);
                }

                var getMethod = GetMethodInfo(column.Source, in provider) ?? throw new NotSupportedException($"Unsupported type {column.Source.Name}");

                //byte[] To Guid - Oracle
                if (getMethod.Name.Equals("GetBytes", StringComparison.Ordinal) && column.PropertyInfo.PropertyType.Name.Equals("Guid", StringComparison.Ordinal))
                {
                    // Define a local for the byte array
                    using var bufferLocal = emitter.DeclareLocal<byte[]>();

                    // Create a new byte array to hold the GUID
                    emitter.LoadConstant(16) // GUID size
                                        .NewArray<byte>()
                                        .StoreLocal(bufferLocal);

                    // Assume the data is already in the correct position in the reader, and fill the byte array
                    emitter.LoadArgument(0)
                                        .LoadConstant(i)
                                        .LoadConstant(0L)
                                        .LoadLocal(bufferLocal)
                                        .LoadConstant(0)
                                        .LoadConstant(16)
                                        .CallVirtual(GetBytes)
                                        .Pop();

                    emitter.LoadLocal(bufferLocal);
                    emitter.NewObject(GuidConstructorByteArray);

                    emitter.StoreLocal(local);

                    if (column.AllowNull)
                    {
                        emitter.MarkLabel(endLabel);
                    }

                    continue;
                }
                else if (column.IsLargeObject && getMethod.Name.Equals("GetBytes", StringComparison.Ordinal))
                {
                    emitter.LoadArgument(0)
                        .LoadConstant(i)
                        .LoadConstant(bufferSize > 0 ? bufferSize : 8192)
                        .Call(ReadStreamMethod);
                }
                else
                {
                    emitter.LoadArgument(0)
                                        .LoadConstant(i)
                                        .CallVirtual(getMethod);
                }

                if (column.RequiredConversion)
                {

                    Type propertyType = column.PropertyInfo.PropertyType;
                    Type sourceType = column.Source;

                    if (IsPrimitiveTypeConversion(propertyType, sourceType))
                    {
                        emitter.Convert(propertyType);
                    }
                    else if (underlyingType == sourceType)
                    {
                        emitter.NewObject(typeof(Nullable<>).MakeGenericType(underlyingType).GetConstructor(new[] { underlyingType }));
                        //EmitConvertToNullable<T>(emitter, sourceType);
                    }
                    else if (propertyType == typeof(Guid) && sourceType == typeof(string))
                    {
                        emitter.NewObject(GuidConstructorString);
                    }
                    else if (column.DataTypeName == "bit" && underlyingType == typeof(bool)) //postgresql
                    {
                        emitter.NewObject(typeof(Nullable<>).MakeGenericType(typeof(bool)).GetConstructor(new[] { typeof(bool) }));
                        //EmitConvertToNullable<T>(emitter, typeof(bool));
                    }
                    else if (column.DataTypeName == "bit" && propertyType == typeof(bool)) //postgresql
                    {
                    }
                    else if (underlyingType != null)
                    {
                        EmitConversionForNullableType(emitter, underlyingType, sourceType, column);
                    }
                    else
                    {
                        EmitConversionForNonNullableType(emitter, propertyType, sourceType, column);
                    }

                }

                emitter.StoreLocal(local);

                if (column.AllowNull)
                {
                    emitter.MarkLabel(endLabel);
                }
            }
               
            foreach (var local in locals)
                emitter.LoadLocal(local);

            return emitter.NewObject(constructor)
                    .Return()
                    .CreateDelegate();
        }

        private static void EmitDefaultValue<T>(Emit<Func<DbDataReader, T>> emitter, Type? underlyingType, FieldInfo field)
        {
            if (field.FieldType.IsValueType)
            {
                if (field.FieldType == typeof(int) || field.FieldType == typeof(double) ||
                    field.FieldType == typeof(float) || field.FieldType == typeof(long) ||
                    field.FieldType == typeof(short) || field.FieldType == typeof(byte) ||
                    field.FieldType == typeof(sbyte) ||
                    field.FieldType == typeof(uint) || field.FieldType == typeof(ushort) ||
                    field.FieldType == typeof(ulong))
                {
                    emitter.LoadConstant(0);
                }
                else if (field.FieldType == typeof(bool))
                {
                    emitter.LoadConstant(false);
                }
                else if (field.FieldType == typeof(decimal))
                {
                    // Decimal constants are created by loading the value onto the stack and then calling the constructor
                    emitter.LoadConstant(0);     // lo
                    emitter.LoadConstant(0);     // mid
                    emitter.LoadConstant(0);     // hi
                    emitter.LoadConstant(false); // isNegative
                    emitter.LoadConstant(0);     // scale
                    emitter.NewObject<decimal, int, int, int, bool, byte>();
                }
                else if (field.FieldType == typeof(Guid))
                {
                    emitter.LoadField(typeof(Guid).GetField("Empty"));
                }
                else if (field.FieldType == typeof(DateTime))
                {
                    emitter.LoadField(typeof(DateTime).GetField("MinValue"));
                }
                else if (field.FieldType == typeof(TimeSpan))
                {
                    emitter.LoadField(typeof(TimeSpan).GetField("Zero"));
                }
                else if (field.FieldType == typeof(DateOnly))
                {
                    emitter.LoadField(typeof(DateOnly).GetField("MinValue"));
                }
                else if (field.FieldType == typeof(TimeOnly))
                {
                    emitter.LoadField(typeof(TimeOnly).GetField("Midnight"));
                }
                else
                {
                    throw new NotSupportedException($"Unsupported type {field.FieldType.FullName}");
                }
            }
            else
            {
                emitter.LoadNull();
            }
        }

        private static Span<PropertyTypeInfo> GetColumnMap(in Type type, in DbDataReader reader)
        {
            ReadOnlySpan<PropertyInfo> properties = type.GetProperties();
            ReadOnlySpan<DbColumn> columnSchema = reader.GetColumnSchema().ToArray();
            Span<PropertyTypeInfo> columnInfoCollection = new PropertyTypeInfo[properties.Length];

            //TODO: use configurate db name to match with column name
            byte counter = 0;
            foreach (var property in properties)
            {
                for (int i = 0; i < columnSchema.Length; i++)
                {
                    if (columnSchema[i].ColumnName.Equals(property.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        var dataType = columnSchema[i].DataType;
                        columnInfoCollection[counter++] = new PropertyTypeInfo
                        {
                            DataTypeName = columnSchema[i].DataTypeName,
                            Source = dataType,
                            PropertyInfo = property,
                            Name = property.Name,
                            RequiredConversion = !columnSchema[i].DataType.Equals(property.PropertyType),
                            IsLargeObject = columnSchema[i].IsLong == true || columnSchema[i].DataTypeName == "BLOB" || columnSchema[i].DataTypeName == "CLOB",
                            AllowNull = true,
                        };
                        break;
                    }
                }
            }

            return columnInfoCollection;
        }

        private static bool IsReadonlyRecordType(in Type type)
        {
            var allFieldsInitOnly = type.GetRuntimeFields().All(x => x.Attributes.HasFlag(FieldAttributes.InitOnly));
            Type equatableType = typeof(IEquatable<>).MakeGenericType(type);
            return allFieldsInitOnly && equatableType.IsAssignableFrom(type) && (type.GetMethods().Any(x => x.Name == "<Clone>$") || type.IsValueType);
        }

        private static bool IsPrimitiveTypeConversion(Type propertyType, Type sourceType)
        {
            return propertyType.IsValueType && propertyType.IsPrimitive &&
                   sourceType.IsValueType && sourceType.IsPrimitive;
        }

        private static void EmitConversionForNullableType<T>(Emit<Func<DbDataReader, T>> emitter, Type targetType, Type sourceType, PropertyTypeInfo column)
        {
            var converterMethod = ConvertType.GetMethod($"To{targetType.Name}", new[] { sourceType });

            if (converterMethod != null)
            {
                emitter.Call(converterMethod);
                emitter.NewObject(typeof(Nullable<>).MakeGenericType(targetType).GetConstructor(new[] { targetType }));
                //EmitConvertToNullable(emitter, targetType);
            }
            else
            {
                throw new InvalidCastException($"Cannot convert field {column.PropertyInfo.DeclaringType.Name}.{column.Name}, {sourceType.Name} to {targetType.Name}");
            }
        }

        private static void EmitConversionForNonNullableType<T>(Emit<Func<DbDataReader, T>> emitter, Type targetType, Type sourceType, PropertyTypeInfo column)
        {
            var converterMethod = ConvertType.GetMethod($"To{targetType.Name}", new[] { sourceType });

            if (converterMethod != null)
            {
                emitter.Call(converterMethod);
            }
            else
            {
                throw new InvalidCastException($"Cannot convert field {column.PropertyInfo.DeclaringType.Name}.{column.Name}, {sourceType.Name} to {targetType.Name}");
            }
        }

        //private static void EmitConvertToNullable<T>(Emit<Func<DbDataReader, T>> emitter, Type source)
        //{
        //    switch (source.Name)
        //    {
        //        case "Boolean":
        //            emitter.NewObject(typeof(bool?).GetConstructor(new[] { typeof(bool) })!);
        //            break;
        //        case "Byte":
        //            emitter.NewObject(typeof(byte?).GetConstructor(new[] { typeof(byte) })!);
        //            break;
        //        case "Int16":
        //            emitter.NewObject(typeof(short?).GetConstructor(new[] { typeof(short) })!);
        //            break;
        //        case "Int32":
        //            emitter.NewObject(typeof(int?).GetConstructor(new[] { typeof(int) })!);
        //            break;
        //        case "Int64":
        //            emitter.NewObject(typeof(long?).GetConstructor(new[] { typeof(long) })!);
        //            break;
        //        case "UInt16":
        //            emitter.NewObject(typeof(ushort?).GetConstructor(new[] { typeof(ushort) })!);
        //            break;
        //        case "UInt32":
        //            emitter.NewObject(typeof(uint?).GetConstructor(new[] { typeof(uint) })!);
        //            break;
        //        case "UInt64":
        //            emitter.NewObject(typeof(ulong?).GetConstructor(new[] { typeof(ulong) })!);
        //            break;
        //        case "Single":
        //            emitter.NewObject(typeof(float?).GetConstructor(new[] { typeof(float) })!);
        //            break;
        //        case "Double":
        //            emitter.NewObject(typeof(double?).GetConstructor(new[] { typeof(double) })!);
        //            break;
        //        case "Decimal":
        //            emitter.NewObject(typeof(decimal?).GetConstructor(new[] { typeof(decimal) })!);
        //            break;
        //        case "DateTime":
        //            emitter.NewObject(typeof(DateTime?).GetConstructor(new[] { typeof(DateTime) })!);
        //            break;
        //        case "Guid":
        //            emitter.NewObject(typeof(Guid?).GetConstructor(new[] { typeof(Guid) })!);
        //            break;
        //        case "Char":
        //            emitter.NewObject(typeof(char?).GetConstructor(new[] { typeof(char) })!);
        //            break;
        //        default:
        //            throw new InvalidOperationException($"Compatible nullable type was not found for {source.Name}");
        //    }
        //}

        private class PropertyTypeInfo
        {
            public Type Source { get; set; }
            public PropertyInfo PropertyInfo { get; set; }
            public bool RequiredConversion { get; set; }
            public string Name { get; set; }
            public bool IsLargeObject { get; set; }
            public bool AllowNull { get; set; }
            public string DataTypeName { get; set; }
        }

        #endregion delegate builder

        #region specific readers
        private static byte[] ReadStream(DbDataReader reader, int index, int bufferSize) //default 8KB
        {
            byte[] buffer = new byte[bufferSize];
            long dataIndex = 0;
            long bytesRead;

            using MemoryStream memoryStream = new MemoryStream();

            bytesRead = reader.GetBytes(index, dataIndex, buffer, 0, bufferSize);

            while (bytesRead == bufferSize)
            {
                memoryStream.Write(buffer, 0, (int)bytesRead);
                dataIndex += bytesRead;
                bytesRead = reader.GetBytes(index, dataIndex, buffer, 0, bufferSize);
            }

            memoryStream.Write(buffer, 0, (int)bytesRead);

            return memoryStream.ToArray();
        }
        #endregion specific readers

    }
}
