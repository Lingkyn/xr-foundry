using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Lingkyn.Persistence.Core;
using UnityEngine;

namespace Lingkyn.Persistence.Unity
{
    public static class JsonUtilityDtoSupport
    {
        public static SaveResult ValidateDtoType<T>()
        {
            return ValidateType(typeof(T), new HashSet<Type>());
        }

        internal static SaveResult ValidateType(Type type, HashSet<Type> visiting)
        {
            if (type == null)
            {
                return SaveResult.Fail(SaveStage.Encode, SaveErrorCode.UnsupportedFormat, "DTO type is required.");
            }

            if (IsSupportedPrimitive(type) || type.IsEnum)
            {
                return SaveResult.Success();
            }

            if (type.IsArray)
            {
                return ValidateType(type.GetElementType(), visiting);
            }

            if (type.IsGenericType)
            {
                return SaveResult.Fail(
                    SaveStage.Encode,
                    SaveErrorCode.UnsupportedFormat,
                    $"Unsupported generic DTO shape: {type.FullName}.");
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return SaveResult.Fail(
                    SaveStage.Encode,
                    SaveErrorCode.UnsupportedFormat,
                    "UnityEngine.Object graphs are not supported DTO snapshots.");
            }

            if (typeof(Delegate).IsAssignableFrom(type))
            {
                return SaveResult.Fail(
                    SaveStage.Encode,
                    SaveErrorCode.UnsupportedFormat,
                    "Delegate fields are not supported in JsonUtility DTO snapshots.");
            }

            if (typeof(IDictionary).IsAssignableFrom(type))
            {
                return SaveResult.Fail(
                    SaveStage.Encode,
                    SaveErrorCode.UnsupportedFormat,
                    "Dictionary shapes are not supported by JsonUtility DTO snapshots.");
            }

            if (type.IsInterface || type.IsAbstract)
            {
                return SaveResult.Fail(
                    SaveStage.Encode,
                    SaveErrorCode.UnsupportedFormat,
                    "Polymorphic interface or abstract DTO roots are not supported.");
            }

            if (!type.IsClass && !type.IsValueType)
            {
                return SaveResult.Fail(
                    SaveStage.Encode,
                    SaveErrorCode.UnsupportedFormat,
                    $"Unsupported DTO type: {type.FullName}.");
            }

            if (!visiting.Add(type))
            {
                return SaveResult.Fail(
                    SaveStage.Encode,
                    SaveErrorCode.UnsupportedFormat,
                    "Cyclic DTO graphs are not supported.");
            }

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (var index = 0; index < fields.Length; index++)
            {
                var field = fields[index];
                if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null)
                {
                    continue;
                }

                if (field.IsInitOnly)
                {
                    continue;
                }

                var fieldValidation = ValidateType(field.FieldType, visiting);
                if (!fieldValidation.Succeeded)
                {
                    visiting.Remove(type);
                    return fieldValidation;
                }
            }

            visiting.Remove(type);
            return SaveResult.Success();
        }

        private static bool IsSupportedPrimitive(Type type)
        {
            return type == typeof(bool)
                || type == typeof(char)
                || type == typeof(byte)
                || type == typeof(sbyte)
                || type == typeof(short)
                || type == typeof(ushort)
                || type == typeof(int)
                || type == typeof(uint)
                || type == typeof(long)
                || type == typeof(ulong)
                || type == typeof(float)
                || type == typeof(double)
                || type == typeof(string);
        }
    }

    public sealed class JsonUtilitySaveCodec<T> : ISaveCodec<T>
    {
        public JsonUtilitySaveCodec()
        {
            var validation = JsonUtilityDtoSupport.ValidateDtoType<T>();
            if (!validation.Succeeded)
            {
                throw new InvalidOperationException(validation.Error.Message);
            }
        }

        public SaveResult<byte[]> Encode(T snapshot)
        {
            var validation = JsonUtilityDtoSupport.ValidateDtoType<T>();
            if (!validation.Succeeded)
            {
                return SaveResult<byte[]>.Fail(validation.Error.Stage, validation.Error.Code, validation.Error.Message);
            }

            if (snapshot == null)
            {
                return SaveResult<byte[]>.Fail(SaveStage.Encode, SaveErrorCode.UnsupportedFormat, "Snapshot cannot be null.");
            }

            try
            {
                var json = JsonUtility.ToJson(snapshot);
                return SaveResult<byte[]>.Success(System.Text.Encoding.UTF8.GetBytes(json));
            }
            catch (Exception exception)
            {
                return SaveResult<byte[]>.Fail(SaveStage.Encode, SaveErrorCode.ProviderFailure, $"JsonUtility encode failed: {exception.Message}");
            }
        }

        public SaveResult<T> Decode(int schemaVersion, ReadOnlySpan<byte> bytes)
        {
            var validation = JsonUtilityDtoSupport.ValidateDtoType<T>();
            if (!validation.Succeeded)
            {
                return SaveResult<T>.Fail(validation.Error.Stage, validation.Error.Code, validation.Error.Message);
            }

            try
            {
                var json = System.Text.Encoding.UTF8.GetString(bytes);
                var snapshot = JsonUtility.FromJson<T>(json);
                if (snapshot == null)
                {
                    return SaveResult<T>.Fail(SaveStage.Decode, SaveErrorCode.UnsupportedFormat, "JsonUtility decode returned null.");
                }

                return SaveResult<T>.Success(snapshot);
            }
            catch (Exception exception)
            {
                return SaveResult<T>.Fail(SaveStage.Decode, SaveErrorCode.ProviderFailure, $"JsonUtility decode failed: {exception.Message}");
            }
        }
    }
}
