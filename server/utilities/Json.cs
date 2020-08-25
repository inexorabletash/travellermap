#nullable enable
using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Json
{
    [AttributeUsage( AttributeTargets.All )]
    internal sealed class JsonNameAttribute : Attribute
    {
        public string Name { get; }

        public JsonNameAttribute( string name )
        {
            Name = name;
        }
    }

    [AttributeUsage( AttributeTargets.All )]
    internal sealed class JsonIgnoreAttribute : Attribute
    {
    }

    internal static class JsonConstants
    {
        public const string MediaType = "application/json";
        public const string StartObject = "{";
        public const string EndObject = "}";
        public const string StartArray = "[";
        public const string EndArray = "]";
        public const string NameSeparator = ":";
        public const string FieldDelimiter = ",";
    }

    internal class JsonSerializer
    {
        public bool SerializeCollectionsAsArrays { get; set; }
        
        public void Serialize( Stream stream, object item )
        {
            using (var sw = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                Serialize(sw, item);
                sw.Flush();
            }
        }

        public void Serialize( TextWriter writer, object item )
        {
            if(SerializeCollectionsAsArrays && item is IEnumerable )
                SerializeArray( writer, (IEnumerable)item );
            else
                SerializeValue( writer, item );
        }

        private static string GetName( object item ) => item.GetType().GetCustomAttributes(typeof(JsonNameAttribute), inherit: true)
            .OfType<JsonNameAttribute>().Select(jn => jn.Name).FirstOrDefault();

        private static string GetName(PropertyInfo pi)
        {
            JsonNameAttribute jn = pi.GetCustomAttributes(typeof(JsonNameAttribute), inherit: true).OfType<JsonNameAttribute>().FirstOrDefault();
            return jn?.Name ?? pi.Name;
        }

        private static object GetDefaultValue(PropertyInfo pi) => pi.GetCustomAttributes(typeof(DefaultValueAttribute), inherit: true)
            .OfType<DefaultValueAttribute>().Select(dva => dva.Value).FirstOrDefault();

        private static bool Ignore(PropertyInfo info) => info.GetCustomAttributes(typeof(JsonIgnoreAttribute), inherit: true).Any();

        private void SerializeObject( TextWriter writer, object item )
        {
            string name = GetName( item );
            if( name != null )
            {
                writer.Write( JsonConstants.StartObject );
                writer.Write( Enquote( name ) );
                writer.Write( JsonConstants.NameSeparator );
            }

            writer.Write( JsonConstants.StartObject );
            bool first = true;

            foreach (PropertyInfo pi in item.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(pi => pi.CanRead && pi.GetIndexParameters().Length == 0 && !Ignore(pi)))
            {
                var value = pi.GetValue(item, null);
                var default_value = GetDefaultValue(pi);

                // TODO: allow null? Currently being automagically suppressed
                if (value != null && !value.Equals(default_value))
                {
                    if (!first)
                        writer.Write(JsonConstants.FieldDelimiter);
                    else
                        first = false;

                    writer.Write(Enquote(GetName(pi)));
                    writer.Write(JsonConstants.NameSeparator);
                    SerializeValue(writer, value);
                }

            }
            writer.Write( JsonConstants.EndObject );

            if( name != null )
                writer.Write( JsonConstants.EndObject );
        }

        private void SerializeArray( TextWriter writer, IEnumerable enumerable )
        {
            string name = GetName( enumerable );
            if( name != null )
            {
                writer.Write( JsonConstants.StartObject );
                writer.Write( Enquote( name ) );
                writer.Write( JsonConstants.NameSeparator );
            }

            writer.Write( JsonConstants.StartArray );
            bool first = true;
            foreach( object o in enumerable )
            {
                if( !first )
                    writer.Write( JsonConstants.FieldDelimiter );
                else
                    first = false;

                SerializeValue( writer, o );
            }
            writer.Write( JsonConstants.EndArray );

            if( name != null )
                writer.Write( JsonConstants.EndObject );
        }

        private void SerializeValue(TextWriter writer, object o)
        {
            switch (o)
            {
                case null:
                    writer.Write("null");
                    break;
                case bool b:
                    writer.Write(b ? "true" : "false");
                    break;
                case byte n:
                    writer.Write(((double)n).ToString(CultureInfo.InvariantCulture));
                    break;
                case short n:
                    writer.Write(((double)n).ToString(CultureInfo.InvariantCulture));
                    break;
                case int n:
                    writer.Write(((double)n).ToString(CultureInfo.InvariantCulture));
                    break;
                case long n:
                    writer.Write(((double)n).ToString(CultureInfo.InvariantCulture));
                    break;
                case float n:
                    writer.Write(((double)n).ToString(CultureInfo.InvariantCulture));
                    break;
                case double n:
                    writer.Write(n.ToString(CultureInfo.InvariantCulture));
                    break;
                case string s:
                    writer.Write(Enquote(s));
                    break;
                case IEnumerable enumerable:
                    SerializeArray(writer, enumerable);
                    break;
                default:
                    SerializeObject(writer, o);
                    break;
            }
        }

        private static string Enquote(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "\"\"";

            StringBuilder sb = new StringBuilder(s.Length);
            sb.Append('"');
            foreach (char c in s)
            {
                if ((c == '\\') || (c == '"') || (c == '/'))
                {
                    sb.Append('\\');
                    sb.Append(c);
                }
                else if (c == '\b')
                {
                    sb.Append("\\b");
                }
                else if (c == '\t')
                {
                    sb.Append("\\t");
                }
                else if (c == '\n')
                {
                    sb.Append("\\n");
                }
                else if (c == '\f')
                {
                    sb.Append("\\f");
                }
                else if (c == '\r')
                {
                    sb.Append("\\r");
                }
                else if (c < '\x20' || (c >= '\x7F'))
                {
                    sb.Append("\\u");
                    sb.Append(Convert.ToInt16(c).ToString("X4"));
                }
                else
                {
                    sb.Append(c);
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
