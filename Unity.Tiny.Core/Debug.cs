using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Mathematics;

namespace Unity.Tiny
{
    public static class Debug
    {
        internal static FixedString128 MessageObjectToString(object message)
        {
            FixedString128 result = default;

            if (message == null)
                result.Append("(null)");
            else if (message is string stringMessage)
                result.Append(stringMessage);
            else if (message is byte byteMessage)
                result.Append(byteMessage);
            else if (message is int intMessage)
                result.Append(intMessage);
            else if (message is short shortMessage)
                result.Append(shortMessage);
            else if (message is float floatMessage)
                result.Append(floatMessage);
            else if (message is double doubleMessage)
                // TODO need double formatting
                result.Append((float) doubleMessage);
            else if (message is bool boolMessage)
                result.Append(boolMessage ? "true" : "false");
            else if (message is char charMessage)
                result.Append(charMessage);
            else if (message is float2 f2Message)
            {
                result.Append("(");
                result.Append(f2Message.x);
                result.Append(", ");
                result.Append(f2Message.y);
                result.Append(")");
            }
            else if (message is float3 f3Message)
            {
                result.Append("(");
                result.Append(f3Message.x);
                result.Append(", ");
                result.Append(f3Message.y);
                result.Append(", ");
                result.Append(f3Message.z);
                result.Append(")");
            }
            else if (message is float4 f4Message)
            {
                result.Append("(");
                result.Append(f4Message.x);
                result.Append(", ");
                result.Append(f4Message.y);
                result.Append(", ");
                result.Append(f4Message.z);
                result.Append(", ");
                result.Append(f4Message.w);
                result.Append(")");
            }
            else if (message is Exception exc)
            {
                result.Append(exc.Message);
                result.Append("\n");
                result.Append(exc.StackTrace);
            }
            else if (message is IUTF8Bytes utf8BytesMessage)
            {
                INativeList<byte> nlb = (INativeList<byte>) utf8BytesMessage;
                unsafe
                {
                    result.Append(utf8BytesMessage.GetUnsafePtr(), nlb.Length);
                }
            }
            else
            {
                result.Append("[type not supported]");
            }

            return result;
        }

        public static unsafe FixedString4096 FormatGeneric(string format, object[] args)
        {
            FixedString128* fargs = stackalloc FixedString128[8];
            FixedString512 fformat = format;
            if (args != null)
            {
                for (int i = 0; i < args.Length; ++i)
                {
                    fargs[i] = MessageObjectToString(args[i]);
                }
            }

            FixedString4096 result = default;
            result.AppendFormat(fformat, fargs[0], fargs[1], fargs[2], fargs[3], fargs[4], fargs[5], fargs[6], fargs[7]);
            return result;
        }

        public static void LogRelease(object logObject)
        {
            var log = MessageObjectToString(logObject);
            LogRelease(log);
        }

        public static void LogRelease(FixedString4096 log)
        {
            LogOutputString(log);
        }

        public static void LogReleaseAlways(object logObject)
        {
            var log = MessageObjectToString(logObject);
            LogReleaseAlways(log);
        }

        public static void LogReleaseAlways(FixedString4096 log)
        {
            LogOutputString(log);
        }

        public static void LogFormatRelease(string format, params object[] args)
        {
            LogRelease(FormatGeneric(format, args).ToString());
        }

        public static void LogFormatReleaseAlways(string format, params object[] args)
        {
            LogReleaseAlways(FormatGeneric(format, args).ToString());
        }

        public static void LogReleaseException(Exception exception)
        {
            LogRelease(exception);
        }

        public static void LogReleaseExceptionAlways(Exception exception)
        {
            LogReleaseAlways(exception);
        }

        /// <summary>
        /// Writes an object's ToString to stdout as a error.
        /// </summary>
        [Conditional("DEBUG")]
        public static void LogError(object message)
        {
            LogRelease(message);
        }

        /// <summary>
        /// Writes an object's ToString to stdout as a warning.
        /// </summary>
        [Conditional("DEBUG")]
        public static void LogWarning(object message)
        {
            LogRelease(message);
        }

        /// <summary>
        /// Writes an object's ToString to stdout.
        /// </summary>
        [Conditional("DEBUG")]
        public static void Log(object logObject)
        {
            LogRelease(logObject);
        }

        /// <summary>
        /// Writes a formatted string to stdout.
        /// </summary>
        [Conditional("DEBUG")]
        public static void LogException(Exception exception)
        {
            LogReleaseException(exception);
        }

        /// <summary>
        /// Writes a formatted string to stdout.
        /// </summary>
        [Conditional("DEBUG")]
        public static void LogExceptionAlways(Exception exception)
        {
            LogReleaseExceptionAlways(exception);
        }

        /// <summary>
        /// Writes an object's ToString to stdout.
        /// </summary>
        [Conditional("DEBUG")]
        public static void LogAlways(object logObject)
        {
            LogReleaseAlways(logObject);
        }

        /// <summary>
        /// Writes a formatted string to stdout.
        /// </summary>
        [Conditional("DEBUG")]
        public static void LogFormat(string format, params object[] args)
        {
            LogFormatRelease(format, args);
        }

        /// <summary>
        /// Writes a formatted string to stdout.
        /// </summary>
        [Conditional("DEBUG")]
        public static void LogFormatAlways(string format, params object[] args)
        {
            LogFormatReleaseAlways(format, args);
        }

        // We just write everything to Console
        internal static void LogOutputString(FixedString4096 message)
        {
            // NB -- need ToString() here because UnityEngine.Debug API requires an object of a type it understands
            UnityEngine.Debug.Log(message.ToString());
        }
    }
}
