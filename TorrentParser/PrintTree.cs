using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TorrentParser
{
    /// <summary>
    /// Provides methods for displaying tree model of a string representation of <see cref="Dictionary{,}"/> or <see cref="List{}"/> objects.
    /// </summary>
    public class PrintTree
    {
        /// <summary>
        /// Writes to the console's output string representation of source object, expanding it as a tree if it is a <see cref="Dictionary{,}"/> or a <see cref="List{}"/> type.
        /// </summary>
        /// <param name="source">The object to display.</param>
        /// <param name="indentSize">A size of space indent.</param>
        /// <param name="step">An initial indent amount.</param>
        static public void PrintTreeSimple(object source, int indentSize, int step)
        {
            if (source.GetType().GetInterface(typeof(System.Collections.IDictionary).FullName) != null)
            {
                foreach (System.Collections.DictionaryEntry item in ((System.Collections.IDictionary)source))
                {
                    Console.WriteLine(new string(' ', indentSize * step) + item.Key.ToString() + ":K");
                    if (item.Key.ToString() != "pieces")
                        PrintTreeSimple(item.Value, indentSize * step, step + 1);
                }
            }
            else if (source.GetType().GetInterface(typeof(System.Collections.IList).FullName) != null)
            {
                foreach (object item in ((System.Collections.IList)source))
                {
                    PrintTreeSimple(item, indentSize * step, step + 1);
                }
            }
            else
            {
                Console.WriteLine(new string(' ', indentSize * step) + source.ToString());
            }
        }
        /// <summary>
        /// Writes to the console's output string representation of source object, expanding it as a tree if it is a <see cref="Dictionary{,}"/> or a <see cref="List{}"/> type.
        /// </summary>
        /// <param name="source">The object to display.</param>
        /// <param name="indent">An initial indent amount.</param>
        /// <param name="encoding">An encoding to be used for byte array translation.</param>
        static void PrintTreeDetailed(object source, int indent, Encoding encoding)
        {
            if (source is byte[])
            {
                Console.WriteLine(new string(' ', indent) + new string(encoding.GetChars((byte[])source)));
                /*case "System.Char[]":
                Console.WriteLine(new string(' ', indent) + new string((char[]) o));
                break;*/
            }
            else if (source is long || source is string)
            {
                Console.WriteLine(new string(' ', indent) + source);
            }
            else if (source is List<object>)
            {
                Console.WriteLine(new string(' ', indent) + "[List]");
                foreach (object nestedO in (List<Object>)source)
                {
                    PrintTreeDetailed(nestedO, indent + 1, encoding);
                }
                Console.WriteLine();
            }
            //'source' is supposed to be a Dictionary<byte[], object> . This will confuse users.
            else if (source is Dictionary<object, object>)
            {
                Console.WriteLine(new string(' ', indent) + "[Dictionary]");
                foreach (KeyValuePair<byte[], object> nestedO in (Dictionary<byte[], object>)source)
                {
                    string decodedKey = encoding.GetString(nestedO.Key);
                    Console.WriteLine(new string(' ', indent) + decodedKey + ":K");
                    if (decodedKey == "pieces")
                    {
                        //Console.WriteLine(new string(' ', indent + 1) + BitConverter.ToString((byte[]) nestedO.Value));
                    }
                    else
                        PrintTreeDetailed(nestedO.Value, indent + 1, encoding);
                }
                Console.WriteLine();
            }
            else
            {
                throw new Exception("Unknown type!");
            }
        }
    }
}
